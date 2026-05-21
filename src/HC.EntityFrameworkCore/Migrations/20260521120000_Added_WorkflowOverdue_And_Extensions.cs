using System;
using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    [DbContext(typeof(HCDbContext))]
    [Migration("20260521120000_Added_WorkflowOverdue_And_Extensions")]
    public partial class Added_WorkflowOverdue_And_Extensions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueAt",
                table: "AppDocumentWorkflowInstances",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtensionCount",
                table: "AppDocumentWorkflowInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalExtensionBusinessDays",
                table: "AppDocumentWorkflowInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppDocumentWorkflowInstanceExtensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentWorkflowInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtendedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtensionBusinessDays = table.Column<int>(type: "integer", nullable: false),
                    PreviousFinishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NewFinishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDocumentWorkflowInstanceExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppDocumentWorkflowInstanceExtensions_AppDocumentWorkflowIn~",
                        column: x => x.DocumentWorkflowInstanceId,
                        principalTable: "AppDocumentWorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstanceExtensions_DocumentWorkflowInsta~",
                table: "AppDocumentWorkflowInstanceExtensions",
                column: "DocumentWorkflowInstanceId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDocumentWorkflowInstanceExtensions");

            migrationBuilder.DropColumn(
                name: "OverdueAt",
                table: "AppDocumentWorkflowInstances");

            migrationBuilder.DropColumn(
                name: "ExtensionCount",
                table: "AppDocumentWorkflowInstances");

            migrationBuilder.DropColumn(
                name: "TotalExtensionBusinessDays",
                table: "AppDocumentWorkflowInstances");
        }
    }
}
