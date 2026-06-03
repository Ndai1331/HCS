using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class Added_DocumentFile_DocxPdfPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDocumentWorkflowInstances_DocumentId",
                table: "AppDocumentWorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentFiles_DocumentId",
                table: "AppDocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentAssignments_ReceiverUserId",
                table: "AppDocumentAssignments");

            migrationBuilder.AddColumn<int>(
                name: "ExtensionCount",
                table: "AppDocumentWorkflowInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueAt",
                table: "AppDocumentWorkflowInstances",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalExtensionBusinessDays",
                table: "AppDocumentWorkflowInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DerivedPdfFileId",
                table: "AppDocumentFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocxFileId",
                table: "AppDocumentFiles",
                type: "uuid",
                nullable: true);

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
                name: "IX_AppDocumentWorkflowInstances_DocumentId_StartedAt",
                table: "AppDocumentWorkflowInstances",
                columns: new[] { "DocumentId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstances_Status_FinishedAt",
                table: "AppDocumentWorkflowInstances",
                columns: new[] { "Status", "FinishedAt" },
                filter: "\"Status\" IN ('IN_PROGRESS', 'OVERDUE')");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentFiles_DerivedPdfFileId",
                table: "AppDocumentFiles",
                column: "DerivedPdfFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentFiles_DocumentId_UploadedAt",
                table: "AppDocumentFiles",
                columns: new[] { "DocumentId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentFiles_SourceDocxFileId",
                table: "AppDocumentFiles",
                column: "SourceDocxFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentAssignments_ReceiverUserId_Status_WorkflowStepTemplateId",
                table: "AppDocumentAssignments",
                columns: new[] { "ReceiverUserId", "Status", "WorkflowStepTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstanceExtensions_DocumentWorkflowInsta~",
                table: "AppDocumentWorkflowInstanceExtensions",
                column: "DocumentWorkflowInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocumentFiles_AppDocumentFiles_DerivedPdfFileId",
                table: "AppDocumentFiles",
                column: "DerivedPdfFileId",
                principalTable: "AppDocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocumentFiles_AppDocumentFiles_SourceDocxFileId",
                table: "AppDocumentFiles",
                column: "SourceDocxFileId",
                principalTable: "AppDocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocumentFiles_AppDocumentFiles_DerivedPdfFileId",
                table: "AppDocumentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_AppDocumentFiles_AppDocumentFiles_SourceDocxFileId",
                table: "AppDocumentFiles");

            migrationBuilder.DropTable(
                name: "AppDocumentWorkflowInstanceExtensions");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentWorkflowInstances_DocumentId_StartedAt",
                table: "AppDocumentWorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentWorkflowInstances_Status_FinishedAt",
                table: "AppDocumentWorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentFiles_DerivedPdfFileId",
                table: "AppDocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentFiles_DocumentId_UploadedAt",
                table: "AppDocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentFiles_SourceDocxFileId",
                table: "AppDocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentAssignments_ReceiverUserId_Status_WorkflowStepTemplateId",
                table: "AppDocumentAssignments");

            migrationBuilder.DropColumn(
                name: "ExtensionCount",
                table: "AppDocumentWorkflowInstances");

            migrationBuilder.DropColumn(
                name: "OverdueAt",
                table: "AppDocumentWorkflowInstances");

            migrationBuilder.DropColumn(
                name: "TotalExtensionBusinessDays",
                table: "AppDocumentWorkflowInstances");

            migrationBuilder.DropColumn(
                name: "DerivedPdfFileId",
                table: "AppDocumentFiles");

            migrationBuilder.DropColumn(
                name: "SourceDocxFileId",
                table: "AppDocumentFiles");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentWorkflowInstances_DocumentId",
                table: "AppDocumentWorkflowInstances",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentFiles_DocumentId",
                table: "AppDocumentFiles",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentAssignments_ReceiverUserId",
                table: "AppDocumentAssignments",
                column: "ReceiverUserId");
        }
    }
}
