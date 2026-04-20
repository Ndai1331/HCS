using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    [DbContext(typeof(HCDbContext))]
    [Migration("20260420090000_AddDocumentsPerformanceIndexes")]
    /// <summary>
    /// Adds indexes that support hot Documents-module queries:
    ///   - AppMasterDatas (Type, Code): used by UpdateDocumentStatusAsync (status code → MasterData.Id)
    ///     and dropdown filtering by master data type.
    ///   - AppDocumentAssignments (DocumentId, IsCurrent) WHERE IsCurrent = true:
    ///     used by ApplyPendingApprovalFlagsAsync, GetSentToMeDocumentIdsAsync,
    ///     ApplySubmitSigningButtonVisibilityAsync, RevokeDocumentAsync.
    /// </summary>
    /// <inheritdoc />
    public partial class AddDocumentsPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent so the migration is safe even if indexes were created manually
            // (e.g. CONCURRENTLY in production) before the migration runs.
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppMasterDatas_Type_Code"
                ON "AppMasterDatas" ("Type", "Code");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocumentAssignments_DocumentId_IsCurrent"
                ON "AppDocumentAssignments" ("DocumentId", "IsCurrent")
                WHERE "IsCurrent" = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocumentAssignments_DocumentId_IsCurrent"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppMasterDatas_Type_Code"";");
        }
    }
}
