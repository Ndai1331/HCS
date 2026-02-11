using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfPathToWorkflowTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PdfTemplatePath",
                table: "AppWorkflowTemplates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfTemplatePath",
                table: "AppWorkflowTemplates");
        }
    }
}
