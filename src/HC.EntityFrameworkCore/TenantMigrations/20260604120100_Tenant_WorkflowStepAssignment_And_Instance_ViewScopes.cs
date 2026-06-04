using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.TenantMigrations
{
    [DbContext(typeof(HCDbContext))]
    [Migration("20260604120100_Tenant_WorkflowStepAssignment_And_Instance_ViewScopes")]
    public partial class Tenant_WorkflowStepAssignment_And_Instance_ViewScopes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizationUnitIdsJson",
                table: "AppWorkflowStepAssignments",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultUserIdsJson",
                table: "AppWorkflowStepAssignments",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewStepScopesJson",
                table: "AppDocumentWorkflowInstances",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizationUnitIdsJson",
                table: "AppWorkflowStepAssignments");

            migrationBuilder.DropColumn(
                name: "DefaultUserIdsJson",
                table: "AppWorkflowStepAssignments");

            migrationBuilder.DropColumn(
                name: "ViewStepScopesJson",
                table: "AppDocumentWorkflowInstances");
        }
    }
}
