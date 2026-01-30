using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNullStepAtDocumentAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocumentAssignments_AppWorkflowStepTemplates_StepId",
                table: "AppDocumentAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentAssignments_StepId",
                table: "AppDocumentAssignments");

            migrationBuilder.DropColumn(
                name: "StepId",
                table: "AppDocumentAssignments");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStepTemplateId",
                table: "AppDocumentAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentAssignments_WorkflowStepTemplateId",
                table: "AppDocumentAssignments",
                column: "WorkflowStepTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocumentAssignments_AppWorkflowStepTemplates_WorkflowSte~",
                table: "AppDocumentAssignments",
                column: "WorkflowStepTemplateId",
                principalTable: "AppWorkflowStepTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocumentAssignments_AppWorkflowStepTemplates_WorkflowSte~",
                table: "AppDocumentAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentAssignments_WorkflowStepTemplateId",
                table: "AppDocumentAssignments");

            migrationBuilder.DropColumn(
                name: "WorkflowStepTemplateId",
                table: "AppDocumentAssignments");

            migrationBuilder.AddColumn<Guid>(
                name: "StepId",
                table: "AppDocumentAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentAssignments_StepId",
                table: "AppDocumentAssignments",
                column: "StepId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocumentAssignments_AppWorkflowStepTemplates_StepId",
                table: "AppDocumentAssignments",
                column: "StepId",
                principalTable: "AppWorkflowStepTemplates",
                principalColumn: "Id");
        }
    }
}
