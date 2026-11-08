using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessWorkflowEngine.Api.Workflows;

namespace BusinessWorkflowEngine.Tests;

public sealed class WorkflowContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static WorkflowDefinitionDocument ReadDefinition(string name) =>
        JsonSerializer.Deserialize<WorkflowDefinitionDocument>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Definitions", name + ".json")),
            JsonOptions) ?? throw new InvalidOperationException("Definition was empty.");

    [Theory]
    [InlineData("purchase-approval")]
    [InlineData("employee-leave")]
    public void ShippedBusinessDefinitions_AreValid(string name)
    {
        Assert.Empty(WorkflowDefinitionValidator.Validate(ReadDefinition(name)));
    }

    [Fact]
    public void ShippedBusinessDefinitions_AreDistinctAndUseGenericNodes()
    {
        var purchase = ReadDefinition("purchase-approval");
        var leave = ReadDefinition("employee-leave");

        Assert.NotEqual(purchase.Name, leave.Name);
        Assert.Contains(purchase.Nodes, x => x.Type == "gateway");
        Assert.Contains(purchase.Nodes, x => x.Type == "userTask");
        Assert.Contains(leave.Nodes, x => x.Type == "gateway");
        Assert.Equal(3, leave.Roles.Count);
        Assert.True(leave.Nodes.Count > purchase.Nodes.Count);
        Assert.DoesNotContain(purchase.Nodes, x => x.Type is "purchase" or "approval");
        Assert.DoesNotContain(leave.Nodes, x => x.Type is "leave");
    }

    [Fact]
    public void DefinitionValidator_RejectsUndeclaredTaskRole()
    {
        var purchase = ReadDefinition("purchase-approval");
        var altered = purchase with { Roles = Array.Empty<WorkflowRoleDefinition>() };
        var errors = WorkflowDefinitionValidator.Validate(altered);

        Assert.Contains(errors, e => e.Contains("declared role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefinitionValidator_RejectsGraphCycle()
    {
        var purchase = ReadDefinition("purchase-approval");
        var altered = purchase with
        {
            Nodes = purchase.Nodes.Select(node => node.Id == "auto-approved"
                ? node with { Transitions = [new WorkflowTransitionDefinition("start", null)] }
                : node).ToArray()
        };

        Assert.Contains(WorkflowDefinitionValidator.Validate(altered),
            e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefinitionValidator_RejectsUnsafeHttpTarget()
    {
        var purchase = ReadDefinition("purchase-approval");
        var altered = purchase with
        {
            Nodes = purchase.Nodes.Select(node => node.Id == "manager-review"
                ? node with
                {
                    Type = "http",
                    Config = JsonSerializer.SerializeToElement(new
                    {
                        method = "POST",
                        integrationKey = "external-erp",
                        path = "https://untrusted.example/path"
                    })
                }
                : node).ToArray()
        };

        Assert.Contains(WorkflowDefinitionValidator.Validate(altered),
            e => e.Contains("relative", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InputValidator_RejectsMissingRequiredBusinessFields()
    {
        var purchase = ReadDefinition("purchase-approval");
        var input = JsonNode.Parse("""{"requesterReference":"R-1","amount":1250}""");

        var errors = WorkflowDataValidator.Validate(purchase.InputSchema, input);
        Assert.Contains(errors, e => e.Contains("description is required", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("currency is required", StringComparison.Ordinal));
    }

    [Fact]
    public void InputValidator_RejectsUnexpectedFields()
    {
        var purchase = ReadDefinition("purchase-approval");
        var input = JsonNode.Parse("""
            {"requesterReference":"R-1","description":"Laptop","amount":1250,"currency":"USD","adminOverride":true}
            """);

        Assert.Contains(WorkflowDataValidator.Validate(purchase.InputSchema, input),
            e => e.Contains("adminOverride is not allowed", StringComparison.Ordinal));
    }

    [Fact]
    public void CompletionValidator_RejectsUnknownManagerDecision()
    {
        var purchase = ReadDefinition("purchase-approval");
        var taskNode = Assert.Single(purchase.Nodes, node => node.Id == "manager-review");
        var schema = taskNode.Config.GetProperty("completionSchema");
        var badCompletion = JsonNode.Parse("""{"decision":"maybe"}""");

        Assert.Contains(WorkflowDataValidator.Validate(schema, badCompletion),
            e => e.Contains("allowed values", StringComparison.Ordinal));
    }

    [Fact]
    public void LeaveInputValidator_RequiresIntegerDays()
    {
        var leave = ReadDefinition("employee-leave");
        var invalid = JsonNode.Parse("""
            {"employeeId":"E-2","employeeName":"Sam","days":"three","reason":"Vacation"}
            """);

        Assert.Contains(WorkflowDataValidator.Validate(leave.InputSchema, invalid),
            e => e.Contains("$.days must be integer", StringComparison.Ordinal));
    }
}
