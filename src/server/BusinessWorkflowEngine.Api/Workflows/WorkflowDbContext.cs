using Microsoft.EntityFrameworkCore;

namespace BusinessWorkflowEngine.Api.Workflows;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<WorkflowDefinition> Definitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowInstance> Instances => Set<WorkflowInstance>();
    public DbSet<ApprovalTask> ApprovalTasks => Set<ApprovalTask>();
    public DbSet<ExecutionEvent> ExecutionEvents => Set<ExecutionEvent>();
    public DbSet<WorkflowDefinitionRevision> WorkflowDefinitionRevisions => Set<WorkflowDefinitionRevision>();
    public DbSet<RuntimeWorkflowInstance> RuntimeWorkflowInstances => Set<RuntimeWorkflowInstance>();
    public DbSet<RuntimeStepExecution> RuntimeStepExecutions => Set<RuntimeStepExecution>();
    public DbSet<RuntimeWorkItem> RuntimeWorkItems => Set<RuntimeWorkItem>();
    public DbSet<RuntimeExecutionEvent> RuntimeExecutionEvents => Set<RuntimeExecutionEvent>();
    public DbSet<RuntimeHttpAction> RuntimeHttpActions => Set<RuntimeHttpAction>();
    public DbSet<WorkflowIntegrationConnection> WorkflowIntegrationConnections => Set<WorkflowIntegrationConnection>();

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

        modelBuilder.Entity<WorkflowDefinitionRevision>().HasKey(x => new { x.Name, x.Revision });
        modelBuilder.Entity<WorkflowDefinitionRevision>().HasIndex(x => new { x.Name, x.IsPublished });
        modelBuilder.Entity<RuntimeWorkflowInstance>().HasIndex(x => new { x.DefinitionName, x.InitiatorSubject, x.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<RuntimeWorkflowInstance>().HasIndex(x => new { x.InitiatorSubject, x.CreatedAt });
        modelBuilder.Entity<RuntimeWorkflowInstance>().HasIndex(x => new { x.Status, x.ExecutionLeaseUntil });
        modelBuilder.Entity<RuntimeWorkflowInstance>().Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        modelBuilder.Entity<RuntimeWorkflowInstance>().HasOne<WorkflowDefinitionRevision>().WithMany().HasForeignKey(x => new { x.DefinitionName, x.DefinitionRevision });
        modelBuilder.Entity<RuntimeStepExecution>().HasIndex(x => new { x.WorkflowInstanceId, x.StartedAt });
        modelBuilder.Entity<RuntimeStepExecution>().HasOne<RuntimeWorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId);
        modelBuilder.Entity<RuntimeWorkItem>().HasIndex(x => new { x.AssignedRole, x.Status, x.CreatedAt });
        modelBuilder.Entity<RuntimeWorkItem>().HasIndex(x => x.StepExecutionId).IsUnique();
        modelBuilder.Entity<RuntimeWorkItem>().HasOne<RuntimeWorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId);
        modelBuilder.Entity<RuntimeWorkItem>().HasOne<RuntimeStepExecution>().WithMany().HasForeignKey(x => x.StepExecutionId);
        modelBuilder.Entity<RuntimeWorkItem>().Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        modelBuilder.Entity<RuntimeExecutionEvent>().HasIndex(x => new { x.WorkflowInstanceId, x.OccurredAt });
        modelBuilder.Entity<RuntimeExecutionEvent>().HasOne<RuntimeWorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId);
        modelBuilder.Entity<RuntimeHttpAction>().HasIndex(x => new { x.Status, x.NextAttemptAt });
        modelBuilder.Entity<RuntimeHttpAction>().HasIndex(x => x.StepExecutionId).IsUnique();
        modelBuilder.Entity<RuntimeHttpAction>().HasOne<RuntimeWorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId);
        modelBuilder.Entity<RuntimeHttpAction>().HasOne<RuntimeStepExecution>().WithMany().HasForeignKey(x => x.StepExecutionId);
        modelBuilder.Entity<RuntimeHttpAction>().Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        modelBuilder.Entity<WorkflowIntegrationConnection>().HasKey(x => x.Key);
    }
}

public sealed class WorkflowDefinitionRevision
{
    public required string Name { get; set; }
    public int Revision { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string DocumentJson { get; set; }
    public required string Status { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class RuntimeWorkflowInstance
{
    public Guid Id { get; set; }
    public required string DefinitionName { get; set; }
    public int DefinitionRevision { get; set; }
    public required string InitiatorSubject { get; set; }
    public required string Status { get; set; }
    public string? Outcome { get; set; }
    public required string InputJson { get; set; }
    public required string VariablesJson { get; set; }
    public string? OutputJson { get; set; }
    public string? Error { get; set; }
    public string? CurrentNodeId { get; set; }
    public DateTimeOffset? ExecutionLeaseUntil { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestHash { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RuntimeStepExecution
{
    public Guid Id { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public required string NodeId { get; set; }
    public required string NodeType { get; set; }
    public required string Status { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class RuntimeWorkItem
{
    public Guid Id { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid StepExecutionId { get; set; }
    public required string AssignedRole { get; set; }
    public required string Title { get; set; }
    public string? InputJson { get; set; }
    public string? CompletionJson { get; set; }
    public required string Status { get; set; }
    public string? CompletedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

public sealed class RuntimeExecutionEvent
{
    public long Id { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public string? NodeId { get; set; }
    public required string Type { get; set; }
    public required string ActorSubject { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class RuntimeHttpAction
{
    public Guid Id { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid StepExecutionId { get; set; }
    public required string NodeId { get; set; }
    public required string Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

public sealed class WorkflowIntegrationConnection
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public string? AuthHeaderName { get; set; }
    public string? AuthScheme { get; set; }
    public string? ProtectedSecret { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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
    public string? RequestHash { get; set; }
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
