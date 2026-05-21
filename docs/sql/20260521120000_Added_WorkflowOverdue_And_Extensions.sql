-- Migration: 20260521120000_Added_WorkflowOverdue_And_Extensions
-- Feature: Workflow OVERDUE status + extension audit (OverdueAt, ExtensionCount, AppDocumentWorkflowInstanceExtensions)
-- Target: Host database (AxisHCS / HCDbContext)
-- Idempotent: safe to run multiple times on production

START TRANSACTION;

-- 1) Columns on AppDocumentWorkflowInstances
ALTER TABLE "AppDocumentWorkflowInstances"
    ADD COLUMN IF NOT EXISTS "OverdueAt" timestamp without time zone NULL;

ALTER TABLE "AppDocumentWorkflowInstances"
    ADD COLUMN IF NOT EXISTS "ExtensionCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "AppDocumentWorkflowInstances"
    ADD COLUMN IF NOT EXISTS "TotalExtensionBusinessDays" integer NOT NULL DEFAULT 0;

-- Backfill defaults for rows created before NOT NULL columns existed (defensive)
UPDATE "AppDocumentWorkflowInstances"
SET "ExtensionCount" = 0
WHERE "ExtensionCount" IS NULL;

UPDATE "AppDocumentWorkflowInstances"
SET "TotalExtensionBusinessDays" = 0
WHERE "TotalExtensionBusinessDays" IS NULL;

-- 2) Extension audit table
CREATE TABLE IF NOT EXISTS "AppDocumentWorkflowInstanceExtensions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NULL,
    "DocumentWorkflowInstanceId" uuid NOT NULL,
    "ExtendedByUserId" uuid NOT NULL,
    "ExtensionBusinessDays" integer NOT NULL,
    "PreviousFinishedAt" timestamp without time zone NOT NULL,
    "NewFinishedAt" timestamp without time zone NOT NULL,
    "Reason" character varying(2000) NOT NULL,
    "PreviousStatus" character varying(64) NULL,
    "NewStatus" character varying(64) NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp without time zone NOT NULL,
    "CreatorId" uuid NULL,
    "LastModificationTime" timestamp without time zone NULL,
    "LastModifierId" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeleterId" uuid NULL,
    "DeletionTime" timestamp without time zone NULL,
    CONSTRAINT "PK_AppDocumentWorkflowInstanceExtensions" PRIMARY KEY ("Id")
);

-- 3) Foreign key to AppDocumentWorkflowInstances (only if not exists)
DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_AppDocumentWorkflowInstanceExtensions_AppDocumentWorkflowInstances'
    ) THEN
        ALTER TABLE "AppDocumentWorkflowInstanceExtensions"
        ADD CONSTRAINT "FK_AppDocumentWorkflowInstanceExtensions_AppDocumentWorkflowInstances"
            FOREIGN KEY ("DocumentWorkflowInstanceId")
            REFERENCES "AppDocumentWorkflowInstances" ("Id")
            ON DELETE CASCADE;
    END IF;
END $EF$;

-- 4) Index on DocumentWorkflowInstanceId
CREATE INDEX IF NOT EXISTS "IX_AppDocumentWorkflowInstanceExtensions_DocumentWorkflowInstanceId"
    ON "AppDocumentWorkflowInstanceExtensions" ("DocumentWorkflowInstanceId");

-- 5) EF migrations history
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260521120000_Added_WorkflowOverdue_And_Extensions', '10.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260521120000_Added_WorkflowOverdue_And_Extensions'
);

COMMIT;

-- Rollback (manual only — run outside transaction if needed):
-- DROP INDEX IF EXISTS "IX_AppDocumentWorkflowInstanceExtensions_DocumentWorkflowInstanceId";
-- ALTER TABLE "AppDocumentWorkflowInstanceExtensions"
--     DROP CONSTRAINT IF EXISTS "FK_AppDocumentWorkflowInstanceExtensions_AppDocumentWorkflowInstances";
-- DROP TABLE IF EXISTS "AppDocumentWorkflowInstanceExtensions";
-- ALTER TABLE "AppDocumentWorkflowInstances" DROP COLUMN IF EXISTS "TotalExtensionBusinessDays";
-- ALTER TABLE "AppDocumentWorkflowInstances" DROP COLUMN IF EXISTS "ExtensionCount";
-- ALTER TABLE "AppDocumentWorkflowInstances" DROP COLUMN IF EXISTS "OverdueAt";
-- DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521120000_Added_WorkflowOverdue_And_Extensions';
