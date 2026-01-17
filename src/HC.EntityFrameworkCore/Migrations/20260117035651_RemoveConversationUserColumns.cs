using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConversationUserColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_UserId",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "LastMessageSide",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "UnreadMessageCount",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatConversations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "LastMessageSide",
                table: "ChatConversations",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetUserId",
                table: "ChatConversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnreadMessageCount",
                table: "ChatConversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ChatConversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId",
                table: "ChatConversations",
                column: "UserId");
        }
    }
}
