using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.TenantMigrations
{
    /// <inheritdoc />
    public partial class Updated_Document_26012810391917 : Migration
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

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ForwardedFromMessageId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "ChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedByUserId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedDate",
                table: "ChatMessages",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ChatConversations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ChatConversations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "ChatConversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskId",
                table: "ChatConversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ChatConversations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "IncommingDate",
                table: "AppDocuments",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "ChatConversationMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "MEMBER"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PinnedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversationMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatConversationMembers_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessageFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessageFiles_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId",
                table: "ChatMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ForwardedFromMessageId",
                table: "ChatMessages",
                column: "ForwardedFromMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ReplyToMessageId",
                table: "ChatMessages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_ProjectId",
                table: "ChatConversations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_TaskId",
                table: "ChatConversations",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_Type",
                table: "ChatConversations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversationMembers_ConversationId",
                table: "ChatConversationMembers",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversationMembers_ConversationId_UserId",
                table: "ChatConversationMembers",
                columns: new[] { "ConversationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversationMembers_UserId",
                table: "ChatConversationMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversationMembers_UserId_IsPinned",
                table: "ChatConversationMembers",
                columns: new[] { "UserId", "IsPinned" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageFiles_MessageId",
                table: "ChatMessageFiles",
                column: "MessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatMessages_ForwardedFromMessageId",
                table: "ChatMessages",
                column: "ForwardedFromMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatMessages_ReplyToMessageId",
                table: "ChatMessages",
                column: "ReplyToMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatMessages_ForwardedFromMessageId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatMessages_ReplyToMessageId",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatConversationMembers");

            migrationBuilder.DropTable(
                name: "ChatMessageFiles");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ForwardedFromMessageId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ReplyToMessageId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_ProjectId",
                table: "ChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_TaskId",
                table: "ChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_Type",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ForwardedFromMessageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "PinnedByUserId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "PinnedDate",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "IncommingDate",
                table: "AppDocuments");

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
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
