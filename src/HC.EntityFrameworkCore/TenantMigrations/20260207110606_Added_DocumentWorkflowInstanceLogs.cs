using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_DocumentWorkflowInstanceLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentFileResultId",
                table: "AppDocumentAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppDocumentWorkflowInstanceLogss",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DocumentWorkflowInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    DocumentAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDocumentWorkflowInstanceLogss", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppDocumentWorkflowInstanceLogss_AbpUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppDocumentWorkflowInstanceLogss_AppDocumentAssignments_Doc~",
                        column: x => x.DocumentAssignmentId,
                        principalTable: "AppDocumentAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppDocumentWorkflowInstanceLogss_AppDocumentWorkflowInstanc~",
                        column: x => x.DocumentWorkflowInstanceId,
                        principalTable: "AppDocumentWorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentAssignments_DocumentFileResultId",
                table: "AppDocumentAssignments",
                column: "DocumentFileResultId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstanceLogss_ActorUserId",
                table: "AppDocumentWorkflowInstanceLogss",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstanceLogss_DocumentAssignmentId",
                table: "AppDocumentWorkflowInstanceLogss",
                column: "DocumentAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstanceLogss_DocumentWorkflowInstanceId",
                table: "AppDocumentWorkflowInstanceLogss",
                column: "DocumentWorkflowInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocumentAssignments_AppDocumentFiles_DocumentFileResultId",
                table: "AppDocumentAssignments",
                column: "DocumentFileResultId",
                principalTable: "AppDocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocumentAssignments_AppDocumentFiles_DocumentFileResultId",
                table: "AppDocumentAssignments");

            migrationBuilder.DropTable(
                name: "AppDocumentWorkflowInstanceLogss");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentAssignments_DocumentFileResultId",
                table: "AppDocumentAssignments");

            migrationBuilder.DropColumn(
                name: "DocumentFileResultId",
                table: "AppDocumentAssignments");
        }
    }
}
