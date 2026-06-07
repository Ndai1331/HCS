using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    [DbContext(typeof(HCDbContext))]
    [Migration("20260603130000_Added_DocumentWorkflowInstance_UnlockedViewStepTemplateIds")]
    public partial class Added_DocumentWorkflowInstance_UnlockedViewStepTemplateIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnlockedViewStepTemplateIdsJson",
                table: "AppDocumentWorkflowInstances",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnlockedViewStepTemplateIdsJson",
                table: "AppDocumentWorkflowInstances");
        }
    }
}
