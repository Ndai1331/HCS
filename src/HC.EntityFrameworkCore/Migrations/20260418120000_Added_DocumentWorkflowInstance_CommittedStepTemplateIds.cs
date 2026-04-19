using System;
using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    [DbContext(typeof(HCDbContext))]
    [Migration("20260418120000_Added_DocumentWorkflowInstance_CommittedStepTemplateIds")]
    public partial class Added_DocumentWorkflowInstance_CommittedStepTemplateIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommittedStepTemplateIdsJson",
                table: "AppDocumentWorkflowInstances",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommittedStepTemplateIdsJson",
                table: "AppDocumentWorkflowInstances");
        }
    }
}
