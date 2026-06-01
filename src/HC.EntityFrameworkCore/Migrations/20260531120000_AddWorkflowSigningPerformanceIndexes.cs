using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <summary>
    /// Signing list, document detail, overdue worker, and file list hot-path indexes.
    /// Uses IF NOT EXISTS for idempotent deploys.
    /// </summary>
    [DbContext(typeof(HCDbContext))]
    [Migration("20260531120000_AddWorkflowSigningPerformanceIndexes")]
    public partial class AddWorkflowSigningPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocumentWorkflowInstances_DocumentId_StartedAt"
                ON "AppDocumentWorkflowInstances" ("DocumentId", "StartedAt" DESC);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocumentWorkflowInstances_Status_FinishedAt"
                ON "AppDocumentWorkflowInstances" ("Status", "FinishedAt")
                WHERE "Status" IN ('IN_PROGRESS', 'OVERDUE');
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocumentAssignments_ReceiverUserId_Status_WorkflowStepTemplateId"
                ON "AppDocumentAssignments" ("ReceiverUserId", "Status", "WorkflowStepTemplateId");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AppDocumentFiles_DocumentId_UploadedAt"
                ON "AppDocumentFiles" ("DocumentId", "UploadedAt");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocumentFiles_DocumentId_UploadedAt"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocumentAssignments_ReceiverUserId_Status_WorkflowStepTemplateId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocumentWorkflowInstances_Status_FinishedAt"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AppDocumentWorkflowInstances_DocumentId_StartedAt"";");
        }
    }
}
