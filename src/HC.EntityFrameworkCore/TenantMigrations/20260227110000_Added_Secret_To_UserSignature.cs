using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace HC.TenantMigrations
{
    [DbContext(typeof(HCTenantDbContext))]
    [Migration("20260227110000_Added_Secret_To_UserSignature")]
    /// <inheritdoc />
    public partial class Added_Secret_To_UserSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Secret",
                table: "AppUserSignatures",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Secret",
                table: "AppUserSignatures");
        }
    }
}
