using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowEngine.Api.Workflows;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<WorkflowDefinition> Definitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowInstance> Instances => Set<WorkflowInstance>();
    public DbSet<ApprovalTask> ApprovalTasks => Set<ApprovalTask>();
    public DbSet<ExecutionEvent> ExecutionEvents => Set<ExecutionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinition>().HasKey(x => new { x.Name, x.Revision });
        modelBuilder.Entity<WorkflowInstance>().HasIndex(x => new { x.InitiatorSubject, x.CreatedAt });
        modelBuilder.Entity<WorkflowInstance>().HasIndex(x => x.IdempotencyKey).IsUnique();
        modelBuilder.Entity<WorkflowInstance>().Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        modelBuilder.Entity<ApprovalTask>().HasIndex(x => new { x.AssignedSubject, x.Status, x.CreatedAt });
        modelBuilder.Entity<ApprovalTask>().HasOne<WorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId);
        modelBuilder.Entity<ExecutionEvent>().HasIndex(x => new { x.WorkflowInstanceId, x.OccurredAt });
        modelBuilder.Entity<ExecutionEvent>().HasOne<WorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId);
    }
}

public sealed class WorkflowDefinition
{
    public required string Name { get; set; }
    public int Revision { get; set; }
    public decimal ApprovalThreshold { get; set; }
    public required string ApproverSubject { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class WorkflowInstance
{
    public Guid Id { get; set; }
    public required string DefinitionName { get; set; }
    public int DefinitionRevision { get; set; }
    public required string InitiatorSubject { get; set; }
    public required string RequesterReference { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public string? Outcome { get; set; }
    public required string IdempotencyKey { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ApprovalTask
{
    public Guid Id { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public required string AssignedSubject { get; set; }
    public required string Status { get; set; }
    public string? DecisionComment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ExecutionEvent
{
    public long Id { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public required string Type { get; set; }
    public required string ActorSubject { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
