using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessWorkflowEngine.Api.Workflows.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowExecutionLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExecutionLeaseUntil",
                table: "RuntimeWorkflowInstances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeWorkflowInstances_Status_ExecutionLeaseUntil",
                table: "RuntimeWorkflowInstances",
                columns: new[] { "Status", "ExecutionLeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeWorkflowInstances_Status_ExecutionLeaseUntil",
                table: "RuntimeWorkflowInstances");

            migrationBuilder.DropColumn(
                name: "ExecutionLeaseUntil",
                table: "RuntimeWorkflowInstances");
        }
    }
}
