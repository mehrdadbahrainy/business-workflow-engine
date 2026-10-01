using System.Text.Json;
using System.Text.Json.Nodes;

namespace BusinessWorkflowEngine.Api.Workflows;

public static class WorkflowDataValidator
{
    public static IReadOnlyList<string> Validate(JsonElement schema, JsonNode? data)
    {
        var errors = new List<string>();
        ValidateValue(schema, data, "$", errors);
        return errors;
    }

    private static void ValidateValue(JsonElement schema, JsonNode? value, string path, List<string> errors)
    {
        var type = WorkflowDefinitionValidator.GetString(schema, "type");
        var valid = type switch
        {
            "object" => value is JsonObject,
            "array" => value is JsonArray,
            "string" => value is JsonValue scalar && scalar.TryGetValue<string>(out _),
            "number" => value is JsonValue number && number.TryGetValue<decimal>(out _),
            "integer" => value is JsonValue integer && integer.TryGetValue<long>(out _),
            "boolean" => value is JsonValue boolean && boolean.TryGetValue<bool>(out _),
            _ => false
        };
        if (!valid) { errors.Add($"{path} must be {type ?? "a supported value"}."); return; }
        if (schema.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            var matches = enumValues.EnumerateArray().Any(candidate => JsonNode.DeepEquals(JsonNode.Parse(candidate.GetRawText()), value));
            if (!matches) errors.Add($"{path} must match one of the allowed values.");
        }
        if (value is JsonValue stringValue && stringValue.TryGetValue<string>(out var text))
        {
            if (schema.TryGetProperty("minLength", out var minLength) && minLength.TryGetInt32(out var minimum) && text.Length < minimum) errors.Add($"{path} is too short.");
            if (schema.TryGetProperty("maxLength", out var maxLength) && maxLength.TryGetInt32(out var maximum) && text.Length > maximum) errors.Add($"{path} is too long.");
        }
        if (value is JsonValue numericValue && decimal.TryParse(numericValue.ToJsonString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var numericValueDecimal))
        {
            if (schema.TryGetProperty("minimum", out var minimumValue) && minimumValue.TryGetDecimal(out var minimum) && numericValueDecimal < minimum) errors.Add($"{path} is below the minimum.");
            if (schema.TryGetProperty("maximum", out var maximumValue) && maximumValue.TryGetDecimal(out var maximum) && numericValueDecimal > maximum) errors.Add($"{path} is above the maximum.");
        }
        if (value is not JsonObject obj) return;
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            foreach (var property in required.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null))
                if (!obj.ContainsKey(property!)) errors.Add($"{path}.{property} is required.");
        if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in obj)
                if (properties.TryGetProperty(property.Key, out var childSchema)) ValidateValue(childSchema, property.Value, $"{path}.{property.Key}", errors);
                else if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False)
                    errors.Add($"{path}.{property.Key} is not allowed.");
        }
    }
}
