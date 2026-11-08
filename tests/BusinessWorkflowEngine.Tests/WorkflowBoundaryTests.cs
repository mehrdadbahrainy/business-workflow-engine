using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessWorkflowEngine.Api.Workflows;
using Xunit;

namespace BusinessWorkflowEngine.Tests;

/// <summary>
/// Negative-path contract tests for data entering a generic business workflow.
/// These checks do not require PostgreSQL; end-to-end scenarios run in CI separately.
/// </summary>
public sealed class WorkflowBoundaryTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static WorkflowDefinitionDocument Definition(string name) =>
        JsonSerializer.Deserialize<WorkflowDefinitionDocument>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Definitions", name + ".json")),
            Options) ?? throw new InvalidOperationException("Missing definition.");

    [Theory]
    [InlineData("purchase-approval")]
    [InlineData("employee-leave")]
    public void PublishedExamples_RequireExactlyOneStartAndReachableEnd(string name)
    {
        var definition = Definition(name);
        Assert.Single(definition.Nodes, n => n.Type == "start");
        Assert.Contains(definition.Nodes, n => n.Type == "end");
        Assert.Empty(WorkflowDefinitionValidator.Validate(definition));
    }

    [Fact]
    public void DefinitionValidator_RejectsUnknownNodeType()
    {
        var source = Definition("purchase-approval");
        var changed = source with
        {
            Nodes = source.Nodes.Select(n => n.Id == "check-amount"
                ? n with { Type = "execute-arbitrary-script" }
                : n).ToArray()
        };
        Assert.Contains(WorkflowDefinitionValidator.Validate(changed),
            error => error.Contains("unsupported type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefinitionValidator_RejectsMissingTransitionDestination()
    {
        var source = Definition("purchase-approval");
        var changed = source with
        {
            Nodes = source.Nodes.Select(n => n.Type == "start"
                ? n with { Transitions = [new WorkflowTransitionDefinition("not-a-node", null)] }
                : n).ToArray()
        };
        Assert.Contains(WorkflowDefinitionValidator.Validate(changed),
            error => error.Contains("missing node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefinitionValidator_RejectsMultipleStartNodes()
    {
        var source = Definition("purchase-approval");
        var start = Assert.Single(source.Nodes, n => n.Type == "start");
        var changed = source with
        {
            Nodes = source.Nodes.Concat([start with { Id = "extra-start" }]).ToArray()
        };
        Assert.Contains(WorkflowDefinitionValidator.Validate(changed),
            error => error.Contains("Exactly one start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PurchaseInput_RejectsNonNumericAmount()
    {
        var definition = Definition("purchase-approval");
        var input = JsonNode.Parse("""{"requesterReference":"PO-1","description":"Laptop","amount":"not-a-number","currency":"USD"}""");
        Assert.Contains(WorkflowDataValidator.Validate(definition.InputSchema, input),
            error => error.Contains("amount must be number", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NumericBoundary_EnforcesConfiguredMinimum()
    {
        // The engine supports minimum constraints; the shipped purchase definition
        // currently does not declare a business minimum for amount.
        var schema = JsonSerializer.SerializeToElement(new { type = "object",
            properties = new { amount = new { type = "number", minimum = 1 } },
            required = new[] { "amount" }, additionalProperties = false });
        var input = JsonNode.Parse("""{"amount":-1}""");
        Assert.Contains(WorkflowDataValidator.Validate(schema, input),
            error => error.Contains("minimum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LeaveInput_RejectsMissingRequiredReason()
    {
        var definition = Definition("employee-leave");
        var input = JsonNode.Parse("""{"employeeId":"E-1","employeeName":"Sam","days":3}""");
        Assert.Contains(WorkflowDataValidator.Validate(definition.InputSchema, input),
            error => error.Contains("reason is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TaskCompletion_RejectsUnexpectedFields()
    {
        var definition = Definition("purchase-approval");
        var task = Assert.Single(definition.Nodes, n => n.Id == "manager-review");
        var schema = task.Config.GetProperty("completionSchema");
        var input = JsonNode.Parse("""{"decision":"approve","adminOverride":true}""");
        Assert.Contains(WorkflowDataValidator.Validate(schema, input),
            error => error.Contains("adminOverride is not allowed", StringComparison.Ordinal));
    }
}
