using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BusinessWorkflowEngine.Api.Workflows.Migrations
{
    /// <inheritdoc />
    public partial class AddGenericWorkflowRuntimeAndConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionRevisions",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionRevisions", x => new { x.Name, x.Revision });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowIntegrationConnections",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: false),
                    AuthHeaderName = table.Column<string>(type: "text", nullable: true),
                    AuthScheme = table.Column<string>(type: "text", nullable: true),
                    ProtectedSecret = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowIntegrationConnections", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeWorkflowInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionName = table.Column<string>(type: "text", nullable: false),
                    DefinitionRevision = table.Column<int>(type: "integer", nullable: false),
                    InitiatorSubject = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    VariablesJson = table.Column<string>(type: "text", nullable: false),
                    OutputJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CurrentNodeId = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeWorkflowInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuntimeWorkflowInstances_WorkflowDefinitionRevisions_Defini~",
                        columns: x => new { x.DefinitionName, x.DefinitionRevision },
                        principalTable: "WorkflowDefinitionRevisions",
                        principalColumns: new[] { "Name", "Revision" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeExecutionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ActorSubject = table.Column<string>(type: "text", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeExecutionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuntimeExecutionEvents_RuntimeWorkflowInstances_WorkflowIns~",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "RuntimeWorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeStepExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    NodeType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: true),
                    OutputJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeStepExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuntimeStepExecutions_RuntimeWorkflowInstances_WorkflowInst~",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "RuntimeWorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeHttpActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeHttpActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuntimeHttpActions_RuntimeStepExecutions_StepExecutionId",
                        column: x => x.StepExecutionId,
                        principalTable: "RuntimeStepExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RuntimeHttpActions_RuntimeWorkflowInstances_WorkflowInstanc~",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "RuntimeWorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedRole = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: true),
                    CompletionJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CompletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuntimeWorkItems_RuntimeStepExecutions_StepExecutionId",
                        column: x => x.StepExecutionId,
                        principalTable: "RuntimeStepExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RuntimeWorkItems_RuntimeWorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "RuntimeWorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeExecutionEvents_WorkflowInstanceId_OccurredAt",
                table: "RuntimeExecutionEvents",
                columns: new[] { "WorkflowInstanceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeHttpActions_Status_NextAttemptAt",
                table: "RuntimeHttpActions",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeHttpActions_StepExecutionId",
                table: "RuntimeHttpActions",
                column: "StepExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeHttpActions_WorkflowInstanceId",
                table: "RuntimeHttpActions",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeStepExecutions_WorkflowInstanceId_StartedAt",
                table: "RuntimeStepExecutions",
                columns: new[] { "WorkflowInstanceId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeWorkflowInstances_DefinitionName_DefinitionRevision",
                table: "RuntimeWorkflowInstances",
                columns: new[] { "DefinitionName", "DefinitionRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeWorkflowInstances_DefinitionName_InitiatorSubject_Id~",
                table: "RuntimeWorkflowInstances",
                columns: new[] { "DefinitionName", "InitiatorSubject", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeWorkflowInstances_InitiatorSubject_CreatedAt",
                table: "RuntimeWorkflowInstances",
                columns: new[] { "InitiatorSubject", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeWorkItems_AssignedRole_Status_CreatedAt",
                table: "RuntimeWorkItems",
                columns: new[] { "AssignedRole", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeWorkItems_StepExecutionId",
                table: "RuntimeWorkItems",
                column: "StepExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeWorkItems_WorkflowInstanceId",
                table: "RuntimeWorkItems",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionRevisions_Name_IsPublished",
                table: "WorkflowDefinitionRevisions",
                columns: new[] { "Name", "IsPublished" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuntimeExecutionEvents");

            migrationBuilder.DropTable(
                name: "RuntimeHttpActions");

            migrationBuilder.DropTable(
                name: "RuntimeWorkItems");

            migrationBuilder.DropTable(
                name: "WorkflowIntegrationConnections");

            migrationBuilder.DropTable(
                name: "RuntimeStepExecutions");

            migrationBuilder.DropTable(
                name: "RuntimeWorkflowInstances");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionRevisions");
        }
    }
}
