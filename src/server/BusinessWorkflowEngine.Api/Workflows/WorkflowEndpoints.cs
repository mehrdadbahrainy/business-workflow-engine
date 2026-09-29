using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowEngine.Api.Workflows;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1").RequireAuthorization();
        api.MapPost("/purchase-requests", StartRequest);
        api.MapGet("/purchase-requests", ListRequests);
        api.MapGet("/purchase-requests/{id:guid}", GetRequest);
        api.MapGet("/approval-tasks", ListTasks);
        api.MapPost("/approval-tasks/{id:guid}/decision", Decide);
        return endpoints;
    }

    private static async Task<IResult> StartRequest(PurchaseRequestInput request, HttpContext http, WorkflowDbContext db, CancellationToken cancellationToken)
    {
        var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
            return Results.BadRequest(new { error = "Provide an Idempotency-Key header (maximum 200 characters)." });
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 500 || request.Amount <= 0 || request.Amount > 100_000_000 || !string.Equals(request.Currency, "USD", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(request.RequesterReference) || request.RequesterReference.Length > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Provide a description (up to 500 characters), requester reference (up to 100 characters), positive amount, and USD currency."] });

        var subject = Subject(http.User);
        var existing = await db.Instances.SingleOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.InitiatorSubject != subject) return Results.Conflict(new { error = "Idempotency key is already in use." });
            return Results.Ok(ToResponse(existing));
        }

        var definition = await db.Definitions.SingleOrDefaultAsync(x => x.Name == "purchase-approval" && x.Revision == 1, cancellationToken);
        if (definition is null) return Results.Problem("The seeded purchase-approval definition is missing.", statusCode: 503);
        var now = DateTimeOffset.UtcNow;
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(), DefinitionName = definition.Name, DefinitionRevision = definition.Revision,
            InitiatorSubject = subject, RequesterReference = request.RequesterReference.Trim(), Description = request.Description.Trim(),
            Amount = request.Amount, Currency = request.Currency.ToUpperInvariant(), Status = "completed",
            Outcome = "auto-approved", IdempotencyKey = key, ConcurrencyToken = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now
        };
        var events = new List<ExecutionEvent> { NewEvent(instance.Id, "request-started", subject, now, new { request.Amount, request.Currency }) };
        if (request.Amount > definition.ApprovalThreshold)
        {
            instance.Status = "awaiting-approval";
            instance.Outcome = null;
            db.ApprovalTasks.Add(new ApprovalTask { Id = Guid.NewGuid(), WorkflowInstanceId = instance.Id, AssignedSubject = definition.ApproverSubject, Status = "pending", CreatedAt = now });
            events.Add(NewEvent(instance.Id, "approval-requested", subject, now, new { assignedTo = definition.ApproverSubject }));
        }
        else
        {
            events.Add(NewEvent(instance.Id, "request-auto-approved", subject, now, new { threshold = definition.ApprovalThreshold }));
        }
        db.Instances.Add(instance);
        db.ExecutionEvents.AddRange(events);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var raced = await db.Instances.SingleOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
            if (raced is not null) return raced.InitiatorSubject == subject ? Results.Ok(ToResponse(raced)) : Results.Conflict();
            throw;
        }
        return Results.Created($"/api/v1/purchase-requests/{instance.Id}", ToResponse(instance));
    }

    private static async Task<IResult> ListRequests(WorkflowDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var subject = Subject(user);
        var items = await db.Instances.AsNoTracking().Where(x => x.InitiatorSubject == subject).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return Results.Ok(items.Select(ToResponse));
    }

    private static async Task<IResult> GetRequest(Guid id, WorkflowDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var subject = Subject(user);
        var instance = await db.Instances.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (instance is null) return Results.NotFound();
        if (instance.InitiatorSubject != subject && !await db.ApprovalTasks.AnyAsync(x => x.WorkflowInstanceId == id && x.AssignedSubject == subject, cancellationToken)) return Results.Forbid();
        var events = await db.ExecutionEvents.AsNoTracking().Where(x => x.WorkflowInstanceId == id).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Select(x => new { x.Type, x.ActorSubject, x.DataJson, x.OccurredAt }).ToListAsync(cancellationToken);
        return Results.Ok(new { request = ToResponse(instance), events });
    }

    private static async Task<IResult> ListTasks(WorkflowDbContext db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var subject = Subject(user);
        var tasks = await db.ApprovalTasks.AsNoTracking().Where(x => x.AssignedSubject == subject && x.Status == "pending").OrderBy(x => x.CreatedAt).Join(db.Instances.AsNoTracking(), task => task.WorkflowInstanceId, instance => instance.Id, (task, instance) => new { task.Id, task.WorkflowInstanceId, task.CreatedAt, request = ToResponse(instance) }).ToListAsync(cancellationToken);
        return Results.Ok(tasks);
    }

    private static async Task<IResult> Decide(Guid id, DecisionRequest request, ClaimsPrincipal user, WorkflowDbContext db, CancellationToken cancellationToken)
    {
        if (request.Decision is not ("approve" or "reject")) return Results.BadRequest(new { error = "Decision must be approve or reject." });
        if (request.Comment?.Length > 1000) return Results.BadRequest(new { error = "Comment must be at most 1000 characters." });
        var subject = Subject(user);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var task = await db.ApprovalTasks.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (task is null) return Results.NotFound();
        if (task.AssignedSubject != subject) return Results.Forbid();
        if (task.Status != "pending") return Results.Conflict(new { error = "This approval task has already been completed." });
        var instance = await db.Instances.SingleAsync(x => x.Id == task.WorkflowInstanceId, cancellationToken);
        if (instance.Status != "awaiting-approval") return Results.Conflict(new { error = "The request is not awaiting approval." });
        var now = DateTimeOffset.UtcNow;
        task.Status = request.Decision;
        task.DecisionComment = request.Comment?.Trim();
        task.CompletedAt = now;
        instance.Status = "completed";
        instance.Outcome = request.Decision == "approve" ? "approved" : "rejected";
        instance.UpdatedAt = now;
        instance.ConcurrencyToken = Guid.NewGuid();
        var eventType = request.Decision == "approve" ? "request-approved" : "request-rejected";
        db.ExecutionEvents.Add(NewEvent(instance.Id, eventType, subject, now, new { comment = task.DecisionComment }));
        try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { error = "The task was decided concurrently." }); }
        return Results.Ok(new { taskId = task.Id, request = ToResponse(instance) });
    }

    private static string Subject(ClaimsPrincipal user) => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated principal has no subject.");
    private static ExecutionEvent NewEvent(Guid id, string type, string actor, DateTimeOffset at, object data) => new() { WorkflowInstanceId = id, Type = type, ActorSubject = actor, OccurredAt = at, DataJson = JsonSerializer.Serialize(data) };
    private static object ToResponse(WorkflowInstance x) => new { x.Id, x.DefinitionName, x.DefinitionRevision, x.RequesterReference, x.Description, x.Amount, x.Currency, x.Status, x.Outcome, x.CreatedAt, x.UpdatedAt };

    public sealed record PurchaseRequestInput(string RequesterReference, string Description, decimal Amount, string Currency);
    public sealed record DecisionRequest(string Decision, string? Comment);
}
