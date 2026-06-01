-- Workflow signing performance indexes (PostgreSQL)
-- Idempotent: safe to run multiple times.
-- Also applied via EF migration: 20260531120000_AddWorkflowSigningPerformanceIndexes
--
-- Production: run against AxisHCS after deploy if DbMigrator has not applied this migration yet.
-- Verify: SELECT indexname FROM pg_indexes WHERE indexname LIKE 'IX_AppDocumentWorkflowInstances_DocumentId_StartedAt';

CREATE INDEX IF NOT EXISTS "IX_AppDocumentWorkflowInstances_DocumentId_StartedAt"
    ON "AppDocumentWorkflowInstances" ("DocumentId", "StartedAt" DESC);

CREATE INDEX IF NOT EXISTS "IX_AppDocumentWorkflowInstances_Status_FinishedAt"
    ON "AppDocumentWorkflowInstances" ("Status", "FinishedAt")
    WHERE "Status" IN ('IN_PROGRESS', 'OVERDUE');

CREATE INDEX IF NOT EXISTS "IX_AppDocumentAssignments_ReceiverUserId_Status_WorkflowStepTemplateId"
    ON "AppDocumentAssignments" ("ReceiverUserId", "Status", "WorkflowStepTemplateId");

CREATE INDEX IF NOT EXISTS "IX_AppDocumentFiles_DocumentId_UploadedAt"
    ON "AppDocumentFiles" ("DocumentId", "UploadedAt");



INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260531120000_AddWorkflowSigningPerformanceIndexes', '10.0.0');
