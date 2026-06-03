-- Migration: 20260603130100_Tenant_DocumentWorkflowInstance_UnlockedViewStepTemplateIds
-- VIEW step unlock list (Tenant DB)

ALTER TABLE "AppDocumentWorkflowInstances"
    ADD COLUMN IF NOT EXISTS "UnlockedViewStepTemplateIdsJson" character varying(8000) NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260603130100_Tenant_DocumentWorkflowInstance_UnlockedViewStepTemplateIds', '10.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260603130100_Tenant_DocumentWorkflowInstance_UnlockedViewStepTemplateIds'
);
