using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_DocumentWorkflowInstanceFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppDocumentWorkflowInstanceFiles",
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
                    DocumentFileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDocumentWorkflowInstanceFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppDocumentWorkflowInstanceFiles_AppDocumentFiles_DocumentF~",
                        column: x => x.DocumentFileId,
                        principalTable: "AppDocumentFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppDocumentWorkflowInstanceFiles_AppDocumentWorkflowInstanc~",
                        column: x => x.DocumentWorkflowInstanceId,
                        principalTable: "AppDocumentWorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstanceFiles_DocumentFileId",
                table: "AppDocumentWorkflowInstanceFiles",
                column: "DocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstanceFiles_DocumentWorkflowInstanceId",
                table: "AppDocumentWorkflowInstanceFiles",
                column: "DocumentWorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDocumentWorkflowInstanceFiles");
        }
    }
}
