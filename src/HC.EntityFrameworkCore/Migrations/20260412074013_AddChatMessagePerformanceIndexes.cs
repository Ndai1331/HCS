using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessagePerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raw SQL: idempotent when indexes were created manually (e.g. CONCURRENTLY) before migration runs
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_ChatMessages_ConversationId";
                CREATE INDEX IF NOT EXISTS "IX_ChatMessages_ConversationId_CreationTime_Id"
                ON "ChatMessages" ("ConversationId", "CreationTime" DESC, "Id" DESC);
                """);

            // ILIKE '%keyword%' search; not expressible in Fluent API (PostgreSQL GIN + pg_trgm)
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_ChatMessages_Text_Trgm"
                ON "ChatMessages" USING GIN ("Text" gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ChatMessages_Text_Trgm"";");

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_ChatMessages_ConversationId_CreationTime_Id";
                CREATE INDEX IF NOT EXISTS "IX_ChatMessages_ConversationId" ON "ChatMessages" ("ConversationId");
                """);
        }
    }
}
