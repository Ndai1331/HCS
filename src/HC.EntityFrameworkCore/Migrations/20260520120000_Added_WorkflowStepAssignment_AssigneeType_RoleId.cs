using System;
using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(HCDbContext))]
    [Migration("20260520120000_Added_WorkflowStepAssignment_AssigneeType_RoleId")]
    public partial class Added_WorkflowStepAssignment_AssigneeType_RoleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssigneeType",
                table: "AppWorkflowStepAssignments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "SpecificUser");

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "AppWorkflowStepAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppWorkflowStepAssignments_RoleId",
                table: "AppWorkflowStepAssignments",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppWorkflowStepAssignments_AbpRoles_RoleId",
                table: "AppWorkflowStepAssignments",
                column: "RoleId",
                principalTable: "AbpRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppWorkflowStepAssignments_AbpRoles_RoleId",
                table: "AppWorkflowStepAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AppWorkflowStepAssignments_RoleId",
                table: "AppWorkflowStepAssignments");

            migrationBuilder.DropColumn(
                name: "AssigneeType",
                table: "AppWorkflowStepAssignments");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "AppWorkflowStepAssignments");
        }
    }
}
