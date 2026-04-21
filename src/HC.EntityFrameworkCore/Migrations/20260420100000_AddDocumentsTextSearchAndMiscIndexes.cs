using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <summary>
    /// M6 + M7 (mid-term performance optimization):
    ///   - `pg_trgm` extension + GIN indexes on Documents(No) and Documents(Title) so
    ///     `ILIKE '%term%'` filters used by the list page fall back to an index scan
    ///     instead of a sequence scan (10–100× on large tables).
    ///   - B-tree index on Documents(CreatorId) for ArchiveByMe-like filters.
    ///   - Partial index on UserSignatures(IdentityUserId, IsActive) WHERE IsActive = true,
    ///     matching the signing modal's hot query `IdentityUserId = @id AND IsActive = true`.
    /// </summary>
    [DbContext(typeof(HCDbContext))]
    [Migration("20260420100000_AddDocumentsTextSearchAndMiscIndexes")]
    public partial class AddDocumentsTextSearchAndMiscIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pg_trgm ships with PostgreSQL contrib; the extension install is idempotent.
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocuments_No_Trgm"
                ON "AppDocuments" USING GIN ("No" gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocuments_Title_Trgm"
                ON "AppDocuments" USING GIN ("Title" gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocuments_StorageNumber_Trgm"
                ON "AppDocuments" USING GIN ("StorageNumber" gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocuments_CreatorId"
                ON "AppDocuments" ("CreatorId");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppUserSignatures_IdentityUserId_IsActive"
                ON "AppUserSignatures" ("IdentityUserId", "IsActive")
                WHERE "IsActive" = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppUserSignatures_IdentityUserId_IsActive"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocuments_CreatorId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocuments_StorageNumber_Trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocuments_Title_Trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocuments_No_Trgm"";");
            // Do NOT drop the pg_trgm extension — other tables may rely on it.
        }
    }
}
