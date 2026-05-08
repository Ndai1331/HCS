using System;
using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(HCDbContext))]
    [Migration("20260508040000_AddDocumentOrganizationUnitId")]
    public partial class AddDocumentOrganizationUnitId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationUnitId",
                table: "AppDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_OrganizationUnitId",
                table: "AppDocuments",
                column: "OrganizationUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_OrganizationUnitId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId",
                table: "AppDocuments");
        }
    }
}
