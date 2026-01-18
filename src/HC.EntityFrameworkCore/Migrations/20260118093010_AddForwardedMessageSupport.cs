using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class AddForwardedMessageSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ForwardedFromMessageId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ForwardedFromMessageId",
                table: "ChatMessages",
                column: "ForwardedFromMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatMessages_ForwardedFromMessageId",
                table: "ChatMessages",
                column: "ForwardedFromMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatMessages_ForwardedFromMessageId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ForwardedFromMessageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ForwardedFromMessageId",
                table: "ChatMessages");
        }
    }
}
