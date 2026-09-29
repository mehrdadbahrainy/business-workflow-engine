using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BusinessWorkflowEngine.Api.Workflows;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        await db.Database.MigrateAsync();
        if (!await db.Definitions.AnyAsync(x => x.Name == "purchase-approval" && x.Revision == 1))
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Definitions", "purchase-approval-v1.json");
            await using var stream = File.OpenRead(path);
            var definition = await JsonSerializer.DeserializeAsync<PurchaseApprovalDefinition>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("The purchase-approval definition is empty.");
            if (definition.Name != "purchase-approval" || definition.Revision != 1 || definition.ApprovalThreshold < 0 || string.IsNullOrWhiteSpace(definition.ApproverSubject))
                throw new InvalidOperationException("The purchase-approval definition is invalid.");
            db.Definitions.Add(new WorkflowDefinition
            {
                Name = definition.Name, Revision = definition.Revision, ApprovalThreshold = definition.ApprovalThreshold,
                ApproverSubject = app.Configuration["Workflow:PurchaseApproverSubject"] ?? definition.ApproverSubject,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private sealed record PurchaseApprovalDefinition(string Name, int Revision, decimal ApprovalThreshold, string ApproverSubject);
}
