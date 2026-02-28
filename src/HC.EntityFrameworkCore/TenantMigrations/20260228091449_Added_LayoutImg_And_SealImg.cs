using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_LayoutImg_And_SealImg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SealImg",
                table: "AppUserSignatures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LayoutImg",
                table: "AppSignatureSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SealImg",
                table: "AppUserSignatures");

            migrationBuilder.DropColumn(
                name: "LayoutImg",
                table: "AppSignatureSettings");
        }
    }
}
