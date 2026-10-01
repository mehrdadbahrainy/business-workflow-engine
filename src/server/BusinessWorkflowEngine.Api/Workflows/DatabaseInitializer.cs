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
        await SeedRuntimeDefinitionAsync(db, Path.Combine(AppContext.BaseDirectory, "Definitions", "purchase-approval-workflow-v1.json"));
        await SeedRuntimeDefinitionAsync(db, Path.Combine(AppContext.BaseDirectory, "Definitions", "employee-leave-workflow-v1.json"));
    }

    private static async Task SeedRuntimeDefinitionAsync(WorkflowDbContext db, string path)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<WorkflowDefinitionDocument>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Workflow definition at '{path}' is empty.");
        var errors = WorkflowDefinitionValidator.Validate(document);
        if (errors.Count > 0) throw new InvalidOperationException($"Workflow definition '{document.Name}' is invalid: {string.Join(" ", errors)}");
        if (await db.WorkflowDefinitionRevisions.AnyAsync(x => x.Name == document.Name && x.Revision == document.Revision)) return;
        var now = DateTimeOffset.UtcNow;
        db.WorkflowDefinitionRevisions.Add(new WorkflowDefinitionRevision
        {
            Name = document.Name, Revision = document.Revision, Title = document.Title, Description = document.Description,
            DocumentJson = JsonSerializer.Serialize(document, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Status = "published", IsPublished = true, CreatedBy = "system:seed", CreatedAt = now, PublishedAt = now
        });
        await db.SaveChangesAsync();
    }

    private sealed record PurchaseApprovalDefinition(string Name, int Revision, decimal ApprovalThreshold, string ApproverSubject);
}
