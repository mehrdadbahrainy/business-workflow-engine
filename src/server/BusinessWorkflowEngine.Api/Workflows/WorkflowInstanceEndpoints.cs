using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowEngine.Api.Workflows;

public static class WorkflowInstanceEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapWorkflowInstanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1").RequireAuthorization();
        api.MapPost("/workflows/{name}/instances", Start).WithName("StartWorkflowInstance").WithTags("Workflow instances").Produces(StatusCodes.Status202Accepted).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
        api.MapGet("/workflow-instances", List).WithName("ListWorkflowInstances").WithTags("Workflow instances");
        api.MapGet("/workflow-instances/{id:guid}", Get).WithName("GetWorkflowInstance").WithTags("Workflow instances").Produces(StatusCodes.Status403Forbidden);
        api.MapGet("/work-items", ListWorkItems).WithName("ListWorkflowWorkItems").WithTags("Workflow work items");
        api.MapPost("/work-items/{id:guid}/complete", CompleteWorkItem).WithName("CompleteWorkflowWorkItem").WithTags("Workflow work items").Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
        return endpoints;
    }

    private static async Task<IResult> Start(string name, JsonNode input, [FromHeader(Name = "Idempotency-Key")] string key, ClaimsPrincipal user, WorkflowRuntimeService runtime, CancellationToken cancellationToken)
    {
        try
        {
            var (instance, created) = await runtime.StartAsync(name, input, key, WorkflowRuntimeService.Subject(user), cancellationToken);
            return created ? Results.Accepted($"/api/v1/workflow-instances/{instance.Id}", ToResponse(instance)) : Results.Ok(ToResponse(instance));
        }
        catch (WorkflowInputException exception) { return Results.BadRequest(new { error = exception.Message }); }
        catch (WorkflowNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); }
        catch (WorkflowConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
    }

    private static async Task<IResult> List(ClaimsPrincipal user, WorkflowRuntimeService runtime, CancellationToken cancellationToken) =>
        Results.Ok((await runtime.ListInstancesAsync(WorkflowRuntimeService.Subject(user), cancellationToken)).Select(ToResponse));

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal user, WorkflowDbContext db, CancellationToken cancellationToken)
    {
        var instance = await db.RuntimeWorkflowInstances.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (instance.InitiatorSubject != WorkflowRuntimeService.Subject(user) &&
            !await db.RuntimeWorkItems.AnyAsync(x => x.WorkflowInstanceId == id && UserHasRole(user, x.AssignedRole), cancellationToken)) return Results.Forbid();
        var steps = await db.RuntimeStepExecutions.AsNoTracking().Where(x => x.WorkflowInstanceId == id).OrderBy(x => x.StartedAt).ToListAsync(cancellationToken);
        var events = await db.RuntimeExecutionEvents.AsNoTracking().Where(x => x.WorkflowInstanceId == id).OrderBy(x => x.OccurredAt).ToListAsync(cancellationToken);
        return Results.Ok(new
        {
            instance = ToResponse(instance),
            input = JsonNode.Parse(instance.InputJson),
            output = instance.OutputJson is null ? null : JsonNode.Parse(instance.OutputJson),
            steps = steps.Select(x => new { x.Id, x.NodeId, x.NodeType, x.Status, input = ParseNode(x.InputJson), output = ParseNode(x.OutputJson), x.Error, x.StartedAt, x.CompletedAt }),
            events = events.Select(x => new { x.Type, x.NodeId, x.ActorSubject, data = ParseNode(x.DataJson), x.OccurredAt })
        });
    }

    private static async Task<IResult> ListWorkItems(ClaimsPrincipal user, WorkflowDbContext db, CancellationToken cancellationToken)
    {
        var roles = UserRoles(user);
        var items = await db.RuntimeWorkItems.AsNoTracking().Where(x => x.Status == "pending" && roles.Contains(x.AssignedRole)).OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var workflowIds = items.Select(x => x.WorkflowInstanceId).Distinct().ToArray();
        var instances = await db.RuntimeWorkflowInstances.AsNoTracking().Where(x => workflowIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var definitionNames = instances.Values.Select(x => x.DefinitionName).Distinct().ToArray();
        var definitions = await db.WorkflowDefinitionRevisions.AsNoTracking().Where(x => definitionNames.Contains(x.Name)).ToListAsync(cancellationToken);
        var documents = definitions.ToDictionary(x => (x.Name, x.Revision), x => JsonSerializer.Deserialize<WorkflowDefinitionDocument>(x.DocumentJson, JsonOptions)!);
        return Results.Ok(items.Select(x =>
        {
            instances.TryGetValue(x.WorkflowInstanceId, out var instance);
            var document = instance is not null && documents.TryGetValue((instance.DefinitionName, instance.DefinitionRevision), out var found) ? found : null;
            var taskNode = document?.Nodes.FirstOrDefault(node => node.Id == instance?.CurrentNodeId);
            var completionSchema = taskNode is not null && taskNode.Config.TryGetProperty("completionSchema", out var schema) ? JsonNode.Parse(schema.GetRawText()) : null;
            return new { x.Id, x.WorkflowInstanceId, x.AssignedRole, x.Title, input = ParseNode(x.InputJson), completionSchema, x.CreatedAt, workflow = instance is not null ? ToResponse(instance) : null };
        }));
    }

    private static async Task<IResult> CompleteWorkItem(Guid id, JsonNode completion, ClaimsPrincipal user, WorkflowDbContext db, WorkflowRuntimeService runtime, CancellationToken cancellationToken)
    {
        if (completion is not JsonObject) return Results.BadRequest(new { error = "Completion data must be a JSON object." });
        var task = await db.RuntimeWorkItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (task is null) return Results.NotFound();
        if (!UserHasRole(user, task.AssignedRole)) return Results.Forbid();
        if (task.Status != "pending") return Results.Conflict(new { error = "This work item has already been completed." });
        var instance = await db.RuntimeWorkflowInstances.SingleAsync(x => x.Id == task.WorkflowInstanceId, cancellationToken);
        if (instance.Status != "waiting-task") return Results.Conflict(new { error = "The workflow instance is not waiting for a user task." });
        var revision = await db.WorkflowDefinitionRevisions.SingleAsync(x => x.Name == instance.DefinitionName && x.Revision == instance.DefinitionRevision, cancellationToken);
        var document = JsonSerializer.Deserialize<WorkflowDefinitionDocument>(revision.DocumentJson, JsonOptions)!;
        var node = document.Nodes.Single(x => x.Id == instance.CurrentNodeId && x.Type == "userTask");
        if (node.Config.TryGetProperty("completionSchema", out var schema))
        {
            var errors = WorkflowDataValidator.Validate(schema, completion);
            if (errors.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["completion"] = errors.ToArray() });
        }
        task.Status = "completed"; task.CompletionJson = completion.ToJsonString(JsonOptions); task.CompletedBy = WorkflowRuntimeService.Subject(user); task.CompletedAt = DateTimeOffset.UtcNow;
        task.ConcurrencyToken = Guid.NewGuid();
        var step = await db.RuntimeStepExecutions.SingleAsync(x => x.Id == task.StepExecutionId, cancellationToken);
        step.Status = "completed"; step.OutputJson = task.CompletionJson; step.CompletedAt = task.CompletedAt;
        var variables = JsonNode.Parse(instance.VariablesJson) as JsonObject ?? new JsonObject();
        variables["lastTask"] = JsonNode.Parse(task.CompletionJson);
        instance.VariablesJson = variables.ToJsonString(JsonOptions);
        instance.Status = "ready"; instance.ExecutionLeaseUntil = null; instance.CurrentNodeId = node.Transitions.Single().To; instance.UpdatedAt = DateTimeOffset.UtcNow; instance.ConcurrencyToken = Guid.NewGuid();
        db.RuntimeExecutionEvents.Add(new RuntimeExecutionEvent { WorkflowInstanceId = instance.Id, NodeId = node.Id, Type = "work-completed", ActorSubject = task.CompletedBy, DataJson = task.CompletionJson, OccurredAt = task.CompletedAt.Value });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "This work item was completed concurrently." }); }
        await runtime.AdvanceAsync(instance.Id, task.CompletedBy, cancellationToken);
        return Results.Ok(ToResponse(instance));
    }

    private static object ToResponse(RuntimeWorkflowInstance x) => new { x.Id, x.DefinitionName, x.DefinitionRevision, x.InitiatorSubject, x.Status, x.Outcome, output = ParseNode(x.OutputJson), x.CurrentNodeId, x.CreatedAt, x.UpdatedAt, x.Error };
    private static JsonNode? ParseNode(string? json) => json is null ? null : JsonNode.Parse(json);
    private static string[] UserRoles(ClaimsPrincipal user) => user.Identities.SelectMany(identity => identity.Claims.Where(claim => claim.Type == identity.RoleClaimType).Select(claim => claim.Value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static bool UserHasRole(ClaimsPrincipal user, string role) => UserRoles(user).Contains(role, StringComparer.OrdinalIgnoreCase);
}
