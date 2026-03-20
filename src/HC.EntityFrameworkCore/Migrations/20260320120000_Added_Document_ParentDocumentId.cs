using System;
using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    [DbContext(typeof(HCDbContext))]
    [Migration("20260320120000_Added_Document_ParentDocumentId")]
    /// <inheritdoc />
    public partial class Added_Document_ParentDocumentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentDocumentId",
                table: "AppDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_ParentDocumentId",
                table: "AppDocuments",
                column: "ParentDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocuments_AppDocuments_ParentDocumentId",
                table: "AppDocuments",
                column: "ParentDocumentId",
                principalTable: "AppDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AppDocuments_ParentDocumentId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_ParentDocumentId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "ParentDocumentId",
                table: "AppDocuments");
        }
    }
}
