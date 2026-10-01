using System.Text.Json;
using System.Text.RegularExpressions;

namespace BusinessWorkflowEngine.Api.Workflows;

public sealed record WorkflowDefinitionDocument(
    int SchemaVersion,
    string Name,
    int Revision,
    string Title,
    string? Description,
    JsonElement InputSchema,
    JsonElement OutputSchema,
    IReadOnlyList<WorkflowRoleDefinition> Roles,
    IReadOnlyList<WorkflowNodeDefinition> Nodes);

public sealed record WorkflowRoleDefinition(string Id, string Name);
public sealed record WorkflowNodeDefinition(string Id, string Type, JsonElement Config, IReadOnlyList<WorkflowTransitionDefinition> Transitions);
public sealed record WorkflowTransitionDefinition(string To, WorkflowConditionDefinition? When);
public sealed record WorkflowConditionDefinition(string Path, string Operator, JsonElement Value);

public static partial class WorkflowDefinitionValidator
{
    private static readonly HashSet<string> NodeTypes = ["start", "set", "gateway", "userTask", "http", "end"];
    private static readonly HashSet<string> ComparisonOperators = ["eq", "ne", "gt", "gte", "lt", "lte", "contains"];
    private static readonly HashSet<string> DataTypes = ["string", "number", "integer", "boolean"];

    public static IReadOnlyList<string> Validate(WorkflowDefinitionDocument definition)
    {
        var errors = new List<string>();
        if (definition.SchemaVersion != 1) errors.Add("schemaVersion must be 1.");
        if (!NamePattern().IsMatch(definition.Name ?? string.Empty)) errors.Add("name must contain lowercase letters, digits, and hyphens only.");
        if (definition.Revision < 1) errors.Add("revision must be a positive integer.");
        if (string.IsNullOrWhiteSpace(definition.Title) || definition.Title.Length > 160) errors.Add("title is required and must be at most 160 characters.");
        ValidateSchema(definition.InputSchema, "inputSchema", errors);
        ValidateSchema(definition.OutputSchema, "outputSchema", errors);
        if (definition.Roles is null) errors.Add("roles is required.");
        else if (definition.Roles.Count > 64) errors.Add("A definition may contain at most 64 roles.");
        else if (definition.Roles.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != definition.Roles.Count) errors.Add("role ids must be unique.");
        else foreach (var role in definition.Roles)
        {
            if (!NamePattern().IsMatch(role.Id ?? string.Empty) || string.IsNullOrWhiteSpace(role.Name)) errors.Add("Each role needs a valid id and a display name.");
        }
        if (definition.Nodes is null || definition.Nodes.Count == 0) { errors.Add("At least one node is required."); return errors; }
        if (definition.Nodes.Count > 500) errors.Add("A definition may contain at most 500 nodes.");
        if (definition.Nodes.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != definition.Nodes.Count) errors.Add("Node ids must be unique.");
        var nodes = definition.Nodes.GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var starts = definition.Nodes.Where(x => x.Type == "start").ToList();
        if (starts.Count != 1) errors.Add("Exactly one start node is required.");
        if (!definition.Nodes.Any(x => x.Type == "end")) errors.Add("At least one end node is required.");
        var roleIds = definition.Roles?.Select(x => x.Id).ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (var node in definition.Nodes)
        {
            if (!NamePattern().IsMatch(node.Id ?? string.Empty)) errors.Add($"Node id '{node.Id}' is invalid.");
            if (!NodeTypes.Contains(node.Type)) { errors.Add($"Node '{node.Id}' has unsupported type '{node.Type}'."); continue; }
            if (node.Config.ValueKind != JsonValueKind.Object) errors.Add($"Node '{node.Id}' config must be an object.");
            if (node.Type == "start" && node.Transitions.Count != 1) errors.Add($"Start node '{node.Id}' must have exactly one transition.");
            if (node.Type == "set" && (!HasObjectProperty(node.Config, "values") || node.Transitions.Count != 1)) errors.Add($"Set node '{node.Id}' needs a values object and exactly one transition.");
            if (node.Type == "gateway" && (node.Transitions.Count < 2 || node.Transitions.Count(x => x.When is null) != 1)) errors.Add($"Gateway '{node.Id}' needs at least one condition and exactly one fallback transition.");
            if (node.Type == "userTask")
            {
                var role = GetString(node.Config, "role");
                if (role is null || !roleIds.Contains(role)) errors.Add($"User task '{node.Id}' must reference a declared role.");
                if (node.Transitions.Count != 1) errors.Add($"User task '{node.Id}' must have exactly one transition.");
                if (node.Config.TryGetProperty("completionSchema", out var completionSchema)) ValidateSchema(completionSchema, $"node '{node.Id}' completionSchema", errors);
            }
            if (node.Type == "http")
            {
                var method = GetString(node.Config, "method");
                var integrationKey = GetString(node.Config, "integrationKey");
                var path = GetString(node.Config, "path");
                if (method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE")) errors.Add($"HTTP node '{node.Id}' has an unsupported method.");
                if (integrationKey is null || !NamePattern().IsMatch(integrationKey)) errors.Add($"HTTP node '{node.Id}' must reference a configured integration key.");
                if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') || path.Contains("://", StringComparison.Ordinal)) errors.Add($"HTTP node '{node.Id}' path must be relative to its configured integration base URL.");
                if (node.Transitions.Count != 1) errors.Add($"HTTP node '{node.Id}' must have exactly one transition.");
            }
            if (node.Type == "end" && node.Transitions.Count != 0) errors.Add($"End node '{node.Id}' cannot have transitions.");
            foreach (var transition in node.Transitions)
            {
                if (!nodes.ContainsKey(transition.To)) errors.Add($"Node '{node.Id}' points to missing node '{transition.To}'.");
                if (node.Type == "gateway" && transition.When is not null && (!PathPattern().IsMatch(transition.When.Path) || !ComparisonOperators.Contains(transition.When.Operator) || transition.When.Value.ValueKind == JsonValueKind.Undefined))
                    errors.Add($"Gateway '{node.Id}' has an invalid condition path or operator.");
                if (node.Type != "gateway" && transition.When is not null) errors.Add($"Only gateway transitions may have conditions ('{node.Id}').");
            }
        }
        if (starts.Count == 1)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(); pending.Push(starts[0].Id);
            while (pending.TryPop(out var id))
            {
                if (!nodes.TryGetValue(id, out var node) || !reachable.Add(id)) continue;
                foreach (var edge in node.Transitions) pending.Push(edge.To);
            }
            foreach (var unreachable in nodes.Keys.Except(reachable, StringComparer.Ordinal)) errors.Add($"Node '{unreachable}' cannot be reached from the start node.");
            if (!reachable.Any(id => nodes.TryGetValue(id, out var node) && node.Type == "end")) errors.Add("The start node cannot reach an end node.");
            var colors = new Dictionary<string, byte>(StringComparer.Ordinal);
            bool HasCycle(string id)
            {
                if (colors.TryGetValue(id, out var color)) return color == 1;
                colors[id] = 1;
                foreach (var target in nodes[id].Transitions.Select(x => x.To).Where(nodes.ContainsKey))
                    if (HasCycle(target)) return true;
                colors[id] = 2;
                return false;
            }
            if (nodes.Keys.Any(HasCycle)) errors.Add("Cycles are not supported in schema version 1.");
        }
        return errors;
    }

    private static void ValidateSchema(JsonElement schema, string label, List<string> errors)
    {
        if (schema.ValueKind != JsonValueKind.Object || GetString(schema, "type") != "object") { errors.Add($"{label} must be a JSON object schema with type 'object'."); return; }
        if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind != JsonValueKind.Object) errors.Add($"{label}.properties must be an object.");
        if (schema.TryGetProperty("required", out var required) && (required.ValueKind != JsonValueKind.Array || required.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String))) errors.Add($"{label}.required must be an array of strings.");
        if (properties.ValueKind == JsonValueKind.Object)
            foreach (var property in properties.EnumerateObject())
            {
                var propertyType = GetString(property.Value, "type");
                if (!DataTypes.Contains(propertyType ?? string.Empty)) errors.Add($"{label}.properties.{property.Name} has an unsupported type.");
                if (property.Value.TryGetProperty("enum", out var values) && values.ValueKind != JsonValueKind.Array) errors.Add($"{label}.properties.{property.Name}.enum must be an array.");
            }
        if (required.ValueKind == JsonValueKind.Array && properties.ValueKind == JsonValueKind.Object)
            foreach (var field in required.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null))
                if (!properties.TryGetProperty(field!, out _)) errors.Add($"{label}.required references undefined field '{field}'.");
    }

    private static bool HasObjectProperty(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Object;
    internal static string? GetString(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex("^\\$\\.[a-zA-Z0-9_-]+(?:\\.[a-zA-Z0-9_-]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PathPattern();
}
