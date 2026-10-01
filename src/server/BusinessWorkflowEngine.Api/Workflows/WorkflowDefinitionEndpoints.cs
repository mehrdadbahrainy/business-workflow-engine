using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowEngine.Api.Workflows;

public static class WorkflowDefinitionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapWorkflowDefinitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1/workflow-definitions").RequireAuthorization();
        api.MapGet("", List).WithName("ListWorkflowDefinitions").WithTags("Workflow definitions");
        api.MapGet("/{name}/{revision:int}", Get).WithName("GetWorkflowDefinition").WithTags("Workflow definitions");
        api.MapPut("/{name}/{revision:int}", SaveDraft).WithName("SaveWorkflowDefinitionDraft").WithTags("Workflow definitions").RequireAuthorization("WorkflowAuthor");
        api.MapPost("/{name}/{revision:int}/publish", Publish).WithName("PublishWorkflowDefinition").WithTags("Workflow definitions").RequireAuthorization("WorkflowAuthor");
        return endpoints;
    }

    private static async Task<IResult> List(ClaimsPrincipal user, WorkflowDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var query = db.WorkflowDefinitionRevisions.AsNoTracking().AsQueryable();
        if (!UserRoles(user).Contains(configuration["Workflow:AdminRole"] ?? "workflow-admin", StringComparer.OrdinalIgnoreCase)) query = query.Where(x => x.IsPublished);
        var revisions = await query.OrderBy(x => x.Name).ThenByDescending(x => x.Revision).ToListAsync(cancellationToken);
        return Results.Ok(revisions.GroupBy(x => x.Name).Select(group => ToSummary(group.First())));
    }

    private static async Task<IResult> Get(string name, int revision, ClaimsPrincipal user, WorkflowDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var record = await db.WorkflowDefinitionRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Name == name && x.Revision == revision, cancellationToken);
        if (record is { IsPublished: false } && !UserRoles(user).Contains(configuration["Workflow:AdminRole"] ?? "workflow-admin", StringComparer.OrdinalIgnoreCase)) return Results.Forbid();
        return record is null ? Results.NotFound() : Results.Content(record.DocumentJson, "application/json");
    }

    private static async Task<IResult> SaveDraft(string name, int revision, WorkflowDefinitionDocument document, ClaimsPrincipal user, WorkflowDbContext db, CancellationToken cancellationToken)
    {
        if (document.Name != name || document.Revision != revision) return Results.BadRequest(new { error = "Route name and revision must match the definition document." });
        var errors = WorkflowDefinitionValidator.Validate(document);
        if (errors.Count != 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["definition"] = errors.ToArray() });
        var existing = await db.WorkflowDefinitionRevisions.SingleOrDefaultAsync(x => x.Name == name && x.Revision == revision, cancellationToken);
        if (existing?.IsPublished == true) return Results.Conflict(new { error = "Published definition revisions are immutable. Save a new revision instead." });
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            existing = new WorkflowDefinitionRevision { Name = name, Revision = revision, Title = document.Title, Description = document.Description, DocumentJson = JsonSerializer.Serialize(document, JsonOptions), Status = "draft", CreatedBy = Subject(user), CreatedAt = now };
            db.WorkflowDefinitionRevisions.Add(existing);
        }
        else
        {
            existing.Title = document.Title;
            existing.Description = document.Description;
            existing.DocumentJson = JsonSerializer.Serialize(document, JsonOptions);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToSummary(existing));
    }

    private static async Task<IResult> Publish(string name, int revision, ClaimsPrincipal user, WorkflowDbContext db, CancellationToken cancellationToken)
    {
        var record = await db.WorkflowDefinitionRevisions.SingleOrDefaultAsync(x => x.Name == name && x.Revision == revision, cancellationToken);
        if (record is null) return Results.NotFound();
        if (record.IsPublished) return Results.Ok(ToSummary(record));
        var document = JsonSerializer.Deserialize<WorkflowDefinitionDocument>(record.DocumentJson, JsonOptions);
        if (document is null) return Results.Problem("The stored workflow definition is invalid.", statusCode: StatusCodes.Status500InternalServerError);
        var errors = WorkflowDefinitionValidator.Validate(document);
        if (errors.Count != 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["definition"] = errors.ToArray() });
        var higherRevisionExists = await db.WorkflowDefinitionRevisions.AnyAsync(x => x.Name == name && x.Revision > revision && x.IsPublished, cancellationToken);
        if (higherRevisionExists) return Results.Conflict(new { error = "A newer revision is already published." });
        record.Status = "published";
        record.IsPublished = true;
        record.PublishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToSummary(record));
    }

    private static object ToSummary(WorkflowDefinitionRevision x) => new { x.Name, x.Revision, x.Title, x.Description, x.Status, x.CreatedAt, x.PublishedAt };
    private static string Subject(ClaimsPrincipal user) => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
    private static string[] UserRoles(ClaimsPrincipal user) => user.Identities.SelectMany(identity => identity.Claims.Where(claim => claim.Type == identity.RoleClaimType).Select(claim => claim.Value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
