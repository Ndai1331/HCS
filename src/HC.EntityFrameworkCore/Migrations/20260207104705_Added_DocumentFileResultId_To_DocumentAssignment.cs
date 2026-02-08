using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class Added_DocumentFileResultId_To_DocumentAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentFileResultId",
                table: "AppDocumentAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentAssignments_DocumentFileResultId",
                table: "AppDocumentAssignments",
                column: "DocumentFileResultId");

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

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentAssignments_DocumentFileResultId",
                table: "AppDocumentAssignments");

            migrationBuilder.DropColumn(
                name: "DocumentFileResultId",
                table: "AppDocumentAssignments");
        }
    }
}
