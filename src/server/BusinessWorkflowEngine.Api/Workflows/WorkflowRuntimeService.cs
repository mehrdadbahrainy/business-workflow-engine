using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowEngine.Api.Workflows;

public sealed class WorkflowRuntimeService(WorkflowDbContext db, WorkflowIntegrationSecretProtector secretProtector)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(RuntimeWorkflowInstance Instance, bool Created)> StartAsync(string name, JsonNode? input, string idempotencyKey, string subject, CancellationToken cancellationToken)
    {
        if (input is not JsonObject) throw new WorkflowInputException("Workflow input must be a JSON object.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new WorkflowInputException("Provide an Idempotency-Key header (maximum 200 characters).");
        var definition = await db.WorkflowDefinitionRevisions.Where(x => x.Name == name && x.IsPublished).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(cancellationToken);
        if (definition is null) throw new WorkflowNotFoundException("Published workflow definition not found.");
        var document = Deserialize(definition.DocumentJson);
        var validation = WorkflowDataValidator.Validate(document.InputSchema, input);
        if (validation.Count > 0) throw new WorkflowInputException(string.Join(" ", validation));
        var inputJson = input.ToJsonString(JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { name, input = JsonNode.Parse(inputJson) }, JsonOptions)));
        var existing = await db.RuntimeWorkflowInstances.SingleOrDefaultAsync(x => x.DefinitionName == name && x.InitiatorSubject == subject && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new WorkflowConflictException("Idempotency key was already used with different workflow input.");
            return (existing, false);
        }
        var now = DateTimeOffset.UtcNow;
        var instance = new RuntimeWorkflowInstance
        {
            Id = Guid.NewGuid(), DefinitionName = definition.Name, DefinitionRevision = definition.Revision,
            InitiatorSubject = subject, Status = "ready", InputJson = inputJson, VariablesJson = "{}",
            IdempotencyKey = idempotencyKey, RequestHash = hash, ConcurrencyToken = Guid.NewGuid(),
            CurrentNodeId = document.Nodes.Single(x => x.Type == "start").Id, CreatedAt = now, UpdatedAt = now
        };
        db.RuntimeWorkflowInstances.Add(instance);
        AddEvent(instance, "workflow-started", subject, null, new { definition = name, revision = definition.Revision });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var raced = await db.RuntimeWorkflowInstances.SingleOrDefaultAsync(x => x.DefinitionName == name && x.InitiatorSubject == subject && x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (raced is null) throw;
            if (raced.RequestHash != hash) throw new WorkflowConflictException("Idempotency key was already used with different workflow input.");
            return (raced, false);
        }
        return (instance, true);
    }

    public async Task<bool> ProcessNextWorkflowAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = await db.RuntimeWorkflowInstances.AsNoTracking()
            .Where(x => x.Status == "ready" || x.Status == "running" && x.ExecutionLeaseUntil < now)
            .OrderBy(x => x.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return false;
        var instance = await db.RuntimeWorkflowInstances.SingleAsync(x => x.Id == candidate.Id, cancellationToken);
        if (instance.Status == "running" && instance.ExecutionLeaseUntil >= now) return false;
        instance.Status = "running"; instance.ExecutionLeaseUntil = now.AddMinutes(2); instance.ConcurrencyToken = Guid.NewGuid();
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return false; }
        await AdvanceAsync(instance.Id, "system:workflow-engine", cancellationToken);
        return true;
    }

    public async Task AdvanceAsync(Guid instanceId, string actor, CancellationToken cancellationToken)
    {
        var instance = await db.RuntimeWorkflowInstances.SingleOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance is null || instance.Status != "running") return;
        var storedDefinition = await db.WorkflowDefinitionRevisions.SingleAsync(x => x.Name == instance.DefinitionName && x.Revision == instance.DefinitionRevision, cancellationToken);
        var definition = Deserialize(storedDefinition.DocumentJson);
        var nodes = definition.Nodes.ToDictionary(x => x.Id, StringComparer.Ordinal);
        for (var count = 0; count < 100 && instance.Status == "running"; count++)
        {
            if (instance.CurrentNodeId is null || !nodes.TryGetValue(instance.CurrentNodeId, out var node))
            {
                Fail(instance, null, "Runtime position does not exist in the pinned definition.");
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            var now = DateTimeOffset.UtcNow;
            var step = new RuntimeStepExecution { Id = Guid.NewGuid(), WorkflowInstanceId = instance.Id, NodeId = node.Id, NodeType = node.Type, Status = "running", StartedAt = now };
            db.RuntimeStepExecutions.Add(step);
            AddEvent(instance, "step-started", actor, node.Id, new { type = node.Type });
            var context = await BuildContextAsync(instance, cancellationToken);
            switch (node.Type)
            {
                case "start":
                    await CompleteStepAndMoveAsync(instance, step, node.Transitions[0].To, actor, now, new { }, cancellationToken);
                    break;
                case "set":
                    ApplyMappings(instance, node.Config.GetProperty("values"), context);
                    step.OutputJson = instance.VariablesJson;
                    await CompleteStepAndMoveAsync(instance, step, node.Transitions[0].To, actor, now, new { variablesUpdated = true }, cancellationToken);
                    break;
                case "gateway":
                    var selected = node.Transitions.FirstOrDefault(x => x.When is not null && EvaluateCondition(x.When, context)) ?? node.Transitions.Single(x => x.When is null);
                    step.OutputJson = JsonSerializer.Serialize(new { selected.To }, JsonOptions);
                    await CompleteStepAndMoveAsync(instance, step, selected.To, actor, now, new { selectedRoute = selected.To }, cancellationToken);
                    break;
                case "userTask":
                    var role = WorkflowDefinitionValidator.GetString(node.Config, "role")!;
                    var title = WorkflowDefinitionValidator.GetString(node.Config, "title") ?? definition.Title;
                    var workInput = node.Config.TryGetProperty("input", out var taskInput) ? MapObject(taskInput, context).ToJsonString(JsonOptions) : "{}";
                    step.InputJson = workInput;
                    db.RuntimeWorkItems.Add(new RuntimeWorkItem { Id = Guid.NewGuid(), WorkflowInstanceId = instance.Id, StepExecutionId = step.Id, AssignedRole = role, Title = title, InputJson = workInput, Status = "pending", CreatedAt = now, ConcurrencyToken = Guid.NewGuid() });
                    step.Status = "waiting";
                    instance.Status = "waiting-task";
                    instance.ExecutionLeaseUntil = null;
                    instance.UpdatedAt = now;
                    instance.ConcurrencyToken = Guid.NewGuid();
                    AddEvent(instance, "work-assigned", "system:workflow-engine", node.Id, new { role, title });
                    await db.SaveChangesAsync(cancellationToken);
                    return;
                case "http":
                    db.RuntimeHttpActions.Add(new RuntimeHttpAction { Id = Guid.NewGuid(), WorkflowInstanceId = instance.Id, StepExecutionId = step.Id, NodeId = node.Id, Status = "pending", AttemptCount = 0, NextAttemptAt = now, CreatedAt = now, ConcurrencyToken = Guid.NewGuid() });
                    step.Status = "waiting";
                    instance.Status = "waiting-integration";
                    instance.ExecutionLeaseUntil = null;
                    instance.UpdatedAt = now;
                    instance.ConcurrencyToken = Guid.NewGuid();
                    AddEvent(instance, "integration-queued", "system:workflow-engine", node.Id, new { method = WorkflowDefinitionValidator.GetString(node.Config, "method") });
                    await db.SaveChangesAsync(cancellationToken);
                    return;
                case "end":
                    var output = node.Config.TryGetProperty("output", out var outputMapping) ? MapObject(outputMapping, context) : new JsonObject();
                    var outputErrors = WorkflowDataValidator.Validate(definition.OutputSchema, output);
                    if (outputErrors.Count > 0)
                    {
                        Fail(instance, node.Id, $"End output is invalid: {string.Join(" ", outputErrors)}");
                        step.Status = "failed"; step.Error = instance.Error;
                        await db.SaveChangesAsync(cancellationToken);
                        return;
                    }
                    instance.Status = "completed";
                    instance.Outcome = WorkflowDefinitionValidator.GetString(node.Config, "outcome") ?? "completed";
                    instance.OutputJson = output.ToJsonString(JsonOptions);
                    instance.CurrentNodeId = null;
                    instance.ExecutionLeaseUntil = null;
                    instance.UpdatedAt = now; instance.ConcurrencyToken = Guid.NewGuid();
                    step.Status = "completed"; step.OutputJson = instance.OutputJson; step.CompletedAt = now;
                    AddEvent(instance, "workflow-completed", actor, node.Id, new { outcome = instance.Outcome, output });
                    await db.SaveChangesAsync(cancellationToken);
                    return;
                default:
                    Fail(instance, node.Id, $"Unsupported node type '{node.Type}'.");
                    step.Status = "failed"; step.Error = instance.Error;
                    await db.SaveChangesAsync(cancellationToken);
                    return;
            }
        }
        if (instance.Status == "running")
        {
            Fail(instance, instance.CurrentNodeId, "The workflow exceeded the 100-step synchronous execution limit.");
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<RuntimeWorkflowInstance?> GetInstanceAsync(Guid id, CancellationToken cancellationToken) => await db.RuntimeWorkflowInstances.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RuntimeWorkflowInstance>> ListInstancesAsync(string subject, CancellationToken cancellationToken) =>
        await db.RuntimeWorkflowInstances.AsNoTracking().Where(x => x.InitiatorSubject == subject).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);

    public async Task<bool> ProcessNextHttpActionAsync(HttpClient httpClient, WorkflowIntegrationOptions options, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = await db.RuntimeHttpActions.AsNoTracking()
            .Where(x => (x.Status == "pending" || x.Status == "retry" || x.Status == "processing" && x.LockedUntil < now) && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return false;
        var action = await db.RuntimeHttpActions.SingleAsync(x => x.Id == candidate.Id, cancellationToken);
        action.Status = "processing"; action.AttemptCount++; action.LockedUntil = now.AddMinutes(2); action.ConcurrencyToken = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);

        var instance = await db.RuntimeWorkflowInstances.SingleAsync(x => x.Id == action.WorkflowInstanceId, cancellationToken);
        var storedDefinition = await db.WorkflowDefinitionRevisions.SingleAsync(x => x.Name == instance.DefinitionName && x.Revision == instance.DefinitionRevision, cancellationToken);
        var definition = Deserialize(storedDefinition.DocumentJson);
        var node = definition.Nodes.Single(x => x.Id == action.NodeId && x.Type == "http");
        var integrationKey = WorkflowDefinitionValidator.GetString(node.Config, "integrationKey")!;
        var integration = await db.WorkflowIntegrationConnections.SingleOrDefaultAsync(x => x.Key == integrationKey, cancellationToken);
        var path = WorkflowDefinitionValidator.GetString(node.Config, "path")!;
        if (integration is null || !Uri.TryCreate(integration.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps || !options.GetAllowedHosts().Contains(baseUri.Host, StringComparer.OrdinalIgnoreCase))
        {
            await FinishHttpActionAsync(action, instance, node.Id, "The referenced integration is missing or its host is not in Integrations:AllowedHosts.", false, null, cancellationToken);
            return true;
        }

        try
        {
            var context = await BuildContextAsync(instance, cancellationToken);
            var bodyConfig = node.Config.TryGetProperty("request", out var requestConfig) ? requestConfig : default;
            var body = bodyConfig.ValueKind == JsonValueKind.Object ? MapObject(bodyConfig, context) : new JsonObject();
            var method = WorkflowDefinitionValidator.GetString(node.Config, "method")!;
            var uri = new Uri(baseUri, path.TrimStart('/'));
            if (method == "GET" && node.Config.TryGetProperty("query", out var queryConfig) && queryConfig.ValueKind == JsonValueKind.Object)
            {
                var query = MapObject(queryConfig, context);
                var uriBuilder = new UriBuilder(uri) { Query = string.Join("&", query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value?.ToString() ?? string.Empty)}")) };
                uri = uriBuilder.Uri;
            }
            using var request = new HttpRequestMessage(new HttpMethod(method), uri);
            if (method is not ("GET" or "DELETE")) request.Content = new StringContent(body.ToJsonString(JsonOptions), System.Text.Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", $"{instance.Id}:{node.Id}");
            if (integration.ProtectedSecret is not null)
            {
                var secret = secretProtector.Unprotect(integration.ProtectedSecret);
                if (integration.AuthHeaderName?.Equals("Authorization", StringComparison.OrdinalIgnoreCase) == true)
                    request.Headers.Authorization = new AuthenticationHeaderValue(integration.AuthScheme!, secret);
                else if (integration.AuthHeaderName is not null) request.Headers.TryAddWithoutValidation(integration.AuthHeaderName, secret);
            }
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var responseBody = await ReadLimitedAsync(response.Content, 1024 * 1024, cancellationToken);
            JsonNode? parsedBody;
            try { parsedBody = JsonNode.Parse(responseBody); }
            catch (JsonException) { parsedBody = JsonValue.Create(responseBody); }
            var result = new JsonObject { ["statusCode"] = (int)response.StatusCode, ["body"] = parsedBody };
            if (response.IsSuccessStatusCode)
                await FinishHttpActionAsync(action, instance, node.Id, null, true, result.ToJsonString(JsonOptions), cancellationToken);
            else
                await FinishHttpActionAsync(action, instance, node.Id, $"External system returned HTTP {(int)response.StatusCode}.", (int)response.StatusCode == 429 || (int)response.StatusCode >= 500, result.ToJsonString(JsonOptions), cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await FinishHttpActionAsync(action, instance, node.Id, "External system request timed out.", true, null, cancellationToken);
        }
        catch (CryptographicException)
        {
            await FinishHttpActionAsync(action, instance, node.Id, "Integration credential could not be decrypted. Check the deployment encryption key.", false, null, cancellationToken);
        }
        catch (FormatException)
        {
            await FinishHttpActionAsync(action, instance, node.Id, "Stored integration credential is malformed.", false, null, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            await FinishHttpActionAsync(action, instance, node.Id, "Integration credential encryption is not configured correctly.", false, null, cancellationToken);
        }
        catch (HttpRequestException)
        {
            await FinishHttpActionAsync(action, instance, node.Id, "Could not connect to the external system.", true, null, cancellationToken);
        }
        return true;
    }

    public static string Subject(ClaimsPrincipal user) => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated principal has no subject.");

    private async Task CompleteStepAndMoveAsync(RuntimeWorkflowInstance instance, RuntimeStepExecution step, string next, string actor, DateTimeOffset now, object data, CancellationToken cancellationToken)
    {
        step.Status = "completed"; step.CompletedAt = now;
        instance.CurrentNodeId = next; instance.UpdatedAt = now; instance.ExecutionLeaseUntil = now.AddMinutes(2); instance.ConcurrencyToken = Guid.NewGuid();
        AddEvent(instance, "step-completed", actor, step.NodeId, data);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task FinishHttpActionAsync(RuntimeHttpAction action, RuntimeWorkflowInstance instance, string nodeId, string? error, bool retryable, string? resultJson, CancellationToken cancellationToken)
    {
        var step = await db.RuntimeStepExecutions.SingleAsync(x => x.Id == action.StepExecutionId, cancellationToken);
        action.LockedUntil = null; action.ConcurrencyToken = Guid.NewGuid();
        action.ResultJson = resultJson;
        action.Error = error;
        if (error is null)
        {
            action.Status = "succeeded"; action.CompletedAt = DateTimeOffset.UtcNow;
            step.Status = "completed"; step.OutputJson = resultJson; step.CompletedAt = action.CompletedAt;
            instance.Status = "ready";
            instance.ExecutionLeaseUntil = null;
            var node = Deserialize((await db.WorkflowDefinitionRevisions.SingleAsync(x => x.Name == instance.DefinitionName && x.Revision == instance.DefinitionRevision, cancellationToken)).DocumentJson).Nodes.Single(x => x.Id == nodeId);
            instance.CurrentNodeId = node.Transitions.Single().To;
            instance.UpdatedAt = action.CompletedAt.Value; instance.ConcurrencyToken = Guid.NewGuid();
            AddEvent(instance, "integration-succeeded", "system:workflow-engine", nodeId, new { result = resultJson is null ? null : JsonNode.Parse(resultJson) });
        }
        else if (retryable && action.AttemptCount < 3)
        {
            action.Status = "retry";
            action.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Pow(2, action.AttemptCount));
            step.Status = "waiting"; step.Error = error;
            AddEvent(instance, "integration-retry-scheduled", "system:workflow-engine", nodeId, new { action.AttemptCount, action.NextAttemptAt, error });
        }
        else
        {
            action.Status = "failed"; action.CompletedAt = DateTimeOffset.UtcNow;
            step.Status = "failed"; step.Error = error; step.CompletedAt = action.CompletedAt;
            Fail(instance, nodeId, $"HTTP action failed: {error}");
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string> ReadLimitedAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > maxBytes) throw new HttpRequestException("External response exceeded the size limit.");
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }

    private async Task<JsonObject> BuildContextAsync(RuntimeWorkflowInstance instance, CancellationToken cancellationToken)
    {
        var context = new JsonObject
        {
            ["input"] = JsonNode.Parse(instance.InputJson),
            ["variables"] = JsonNode.Parse(instance.VariablesJson),
        };
        var latestTask = await db.RuntimeWorkItems.AsNoTracking().Where(x => x.WorkflowInstanceId == instance.Id && x.Status == "completed").OrderByDescending(x => x.CompletedAt).FirstOrDefaultAsync(cancellationToken);
        context["lastTask"] = latestTask?.CompletionJson is null ? null : JsonNode.Parse(latestTask.CompletionJson);
        var actions = await db.RuntimeHttpActions.AsNoTracking().Where(x => x.WorkflowInstanceId == instance.Id && x.Status == "succeeded").ToListAsync(cancellationToken);
        var actionContext = new JsonObject();
        foreach (var action in actions) actionContext[action.NodeId] = JsonNode.Parse(action.ResultJson ?? "{}");
        context["actions"] = actionContext;
        return context;
    }

    private static bool EvaluateCondition(WorkflowConditionDefinition condition, JsonObject context)
    {
        var left = ResolvePath(context, condition.Path);
        var right = ResolveValue(condition.Value, context);
        var comparison = Compare(left, right);
        return condition.Operator switch
        {
            "eq" => AreEqual(left, right),
            "ne" => !AreEqual(left, right),
            "gt" => comparison is > 0,
            "gte" => comparison is >= 0,
            "lt" => comparison is < 0,
            "lte" => comparison is <= 0,
            "contains" => left is JsonArray array && array.Any(x => AreEqual(x, right)) || left is JsonValue text && text.TryGetValue<string>(out var s) && right is JsonValue query && query.TryGetValue<string>(out var q) && s.Contains(q, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool AreEqual(JsonNode? left, JsonNode? right)
    {
        if (TryNumber(left, out var a) && TryNumber(right, out var b)) return a == b;
        return JsonNode.DeepEquals(left, right);
    }

    private static int? Compare(JsonNode? left, JsonNode? right)
    {
        if (TryNumber(left, out var a) && TryNumber(right, out var b)) return a.CompareTo(b);
        if (left is JsonValue l && l.TryGetValue<string>(out var ls) && right is JsonValue r && r.TryGetValue<string>(out var rs)) return string.CompareOrdinal(ls, rs);
        return null;
    }

    private static bool TryNumber(JsonNode? node, out decimal value)
    {
        value = 0;
        return node is JsonValue scalar && decimal.TryParse(scalar.ToJsonString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static void ApplyMappings(RuntimeWorkflowInstance instance, JsonElement mappings, JsonObject context)
    {
        var variables = JsonNode.Parse(instance.VariablesJson) as JsonObject ?? new JsonObject();
        foreach (var mapping in mappings.EnumerateObject()) variables[mapping.Name] = ResolveValue(mapping.Value, context);
        instance.VariablesJson = variables.ToJsonString(JsonOptions);
    }

    private static JsonObject MapObject(JsonElement mappings, JsonObject context)
    {
        var result = new JsonObject();
        if (mappings.ValueKind != JsonValueKind.Object) return result;
        foreach (var mapping in mappings.EnumerateObject()) result[mapping.Name] = ResolveValue(mapping.Value, context);
        return result;
    }

    private static JsonNode? ResolveValue(JsonElement value, JsonObject context)
    {
        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } text && text.StartsWith("$.", StringComparison.Ordinal)) return ResolvePath(context, text)?.DeepClone();
        return JsonNode.Parse(value.GetRawText());
    }

    private static JsonNode? ResolvePath(JsonNode? root, string path)
    {
        if (!path.StartsWith("$.", StringComparison.Ordinal)) return null;
        JsonNode? current = root;
        foreach (var segment in path[2..].Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(segment, out var child)) current = child;
            else if (current is JsonArray array && int.TryParse(segment, out var index) && index >= 0 && index < array.Count) current = array[index];
            else return null;
        }
        return current;
    }

    private static WorkflowDefinitionDocument Deserialize(string json) => JsonSerializer.Deserialize<WorkflowDefinitionDocument>(json, JsonOptions) ?? throw new InvalidOperationException("Stored workflow definition is invalid.");
    private void AddEvent(RuntimeWorkflowInstance instance, string type, string actor, string? nodeId, object data) => db.RuntimeExecutionEvents.Add(new RuntimeExecutionEvent { WorkflowInstanceId = instance.Id, Type = type, ActorSubject = actor, NodeId = nodeId, DataJson = JsonSerializer.Serialize(data, JsonOptions), OccurredAt = DateTimeOffset.UtcNow });
    private void Fail(RuntimeWorkflowInstance instance, string? nodeId, string error)
    {
        instance.Status = "failed"; instance.Error = error; instance.CurrentNodeId = nodeId; instance.UpdatedAt = DateTimeOffset.UtcNow; instance.ConcurrencyToken = Guid.NewGuid();
        instance.ExecutionLeaseUntil = null;
        db.RuntimeExecutionEvents.Add(new RuntimeExecutionEvent { WorkflowInstanceId = instance.Id, NodeId = nodeId, Type = "workflow-failed", ActorSubject = "system:workflow-engine", DataJson = JsonSerializer.Serialize(new { error }, JsonOptions), OccurredAt = DateTimeOffset.UtcNow });
    }
}

public sealed class WorkflowInputException(string message) : Exception(message);
public sealed class WorkflowNotFoundException(string message) : Exception(message);
public sealed class WorkflowConflictException(string message) : Exception(message);
public sealed class WorkflowIntegrationOptions
{
    public string AllowedHosts { get; set; } = string.Empty;
    public string[] GetAllowedHosts() => AllowedHosts.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
