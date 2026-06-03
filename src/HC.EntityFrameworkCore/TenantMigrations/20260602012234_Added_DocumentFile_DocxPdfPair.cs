using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_DocumentFile_DocxPdfPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssigneeType",
                table: "AppWorkflowStepAssignments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "AppWorkflowStepAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CommittedStepTemplateIdsJson",
                table: "AppDocumentWorkflowInstances",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000,
                oldNullable: true);

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
                name: "OrganizationUnitId",
                table: "AppDocuments",
                type: "uuid",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentFiles_DerivedPdfFileId",
                table: "AppDocumentFiles",
                column: "DerivedPdfFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentFiles_SourceDocxFileId",
                table: "AppDocumentFiles",
                column: "SourceDocxFileId");

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

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentFiles_DerivedPdfFileId",
                table: "AppDocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentFiles_SourceDocxFileId",
                table: "AppDocumentFiles");

            migrationBuilder.DropColumn(
                name: "AssigneeType",
                table: "AppWorkflowStepAssignments");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "AppWorkflowStepAssignments");

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
                name: "OrganizationUnitId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "DerivedPdfFileId",
                table: "AppDocumentFiles");

            migrationBuilder.DropColumn(
                name: "SourceDocxFileId",
                table: "AppDocumentFiles");

            migrationBuilder.AlterColumn<string>(
                name: "CommittedStepTemplateIdsJson",
                table: "AppDocumentWorkflowInstances",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
