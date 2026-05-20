-- Migration: 20260520120000_Added_WorkflowStepAssignment_AssigneeType_RoleId
-- Feature: Workflow signing by AbpRole + OrganizationUnit (AssigneeType, RoleId on AppWorkflowStepAssignments)
-- Target: Host database (AxisHCS / HCDbContext)
-- Idempotent: safe to run multiple times on production

START TRANSACTION;

-- 1) AssigneeType column (existing rows default to SpecificUser = legacy behavior)
ALTER TABLE "AppWorkflowStepAssignments"
ADD COLUMN IF NOT EXISTS "AssigneeType" character varying(64) NOT NULL DEFAULT 'SpecificUser';

-- Ensure NOT NULL + default for rows added before DEFAULT was applied (defensive)
UPDATE "AppWorkflowStepAssignments"
SET "AssigneeType" = 'SpecificUser'
WHERE "AssigneeType" IS NULL OR BTRIM("AssigneeType") = '';

-- 2) RoleId column (FK to AbpRoles)
ALTER TABLE "AppWorkflowStepAssignments"
ADD COLUMN IF NOT EXISTS "RoleId" uuid NULL;

-- 3) Index on RoleId
CREATE INDEX IF NOT EXISTS "IX_AppWorkflowStepAssignments_RoleId"
ON "AppWorkflowStepAssignments" ("RoleId");

-- 4) Foreign key to AbpRoles (only if not exists)
DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_AppWorkflowStepAssignments_AbpRoles_RoleId'
    ) THEN
        ALTER TABLE "AppWorkflowStepAssignments"
        ADD CONSTRAINT "FK_AppWorkflowStepAssignments_AbpRoles_RoleId"
            FOREIGN KEY ("RoleId")
            REFERENCES "AbpRoles" ("Id")
            ON DELETE SET NULL;
    END IF;
END $EF$;

-- 5) EF migrations history
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260520120000_Added_WorkflowStepAssignment_AssigneeType_RoleId', '10.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260520120000_Added_WorkflowStepAssignment_AssigneeType_RoleId'
);

COMMIT;

-- Rollback (manual only — run outside transaction if needed):
-- ALTER TABLE "AppWorkflowStepAssignments" DROP CONSTRAINT IF EXISTS "FK_AppWorkflowStepAssignments_AbpRoles_RoleId";
-- DROP INDEX IF EXISTS "IX_AppWorkflowStepAssignments_RoleId";
-- ALTER TABLE "AppWorkflowStepAssignments" DROP COLUMN IF EXISTS "RoleId";
-- ALTER TABLE "AppWorkflowStepAssignments" DROP COLUMN IF EXISTS "AssigneeType";
-- DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520120000_Added_WorkflowStepAssignment_AssigneeType_RoleId';
