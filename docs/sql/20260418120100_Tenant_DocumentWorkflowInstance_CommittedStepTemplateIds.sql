START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418120100_Tenant_DocumentWorkflowInstance_CommittedStepTemplateIds') THEN
        ALTER TABLE "AppDocumentWorkflowInstances"
        ADD COLUMN IF NOT EXISTS "CommittedStepTemplateIdsJson" character varying(8000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418120100_Tenant_DocumentWorkflowInstance_CommittedStepTemplateIds') THEN
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260418120100_Tenant_DocumentWorkflowInstance_CommittedStepTemplateIds', '10.0.0');
    END IF;
END $EF$;

COMMIT;
