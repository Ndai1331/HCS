using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <summary>
    /// Chat list query + pinned messages: composite member index for (UserId, IsActive) filtering,
    /// partial index for pinned messages per conversation, optional sort helper on conversations.
    /// Uses IF NOT EXISTS for idempotent deploys.
    /// </summary>
    [DbContext(typeof(HCDbContext))]
    [Migration("20260517120000_AddChatListAndPinnedMessageIndexes")]
    public partial class AddChatListAndPinnedMessageIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_ChatConversationMembers_UserId_IsActive"
                ON "ChatConversationMembers" ("UserId", "IsActive");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_ChatMessages_ConversationId_IsPinned_Partial"
                ON "ChatMessages" ("ConversationId", "IsPinned")
                WHERE "IsPinned" = true;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_ChatConversations_LastMessageDate"
                ON "ChatConversations" ("LastMessageDate" DESC);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ChatConversations_LastMessageDate"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ChatMessages_ConversationId_IsPinned_Partial"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ChatConversationMembers_UserId_IsActive"";");
        }
    }
}
