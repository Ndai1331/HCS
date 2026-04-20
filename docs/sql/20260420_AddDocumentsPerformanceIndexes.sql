START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420090000_AddDocumentsPerformanceIndexes') THEN
    CREATE INDEX IF NOT EXISTS "IX_AppMasterDatas_Type_Code"
    ON "AppMasterDatas" ("Type", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420090000_AddDocumentsPerformanceIndexes') THEN
    CREATE INDEX IF NOT EXISTS "IX_AppDocumentAssignments_DocumentId_IsCurrent"
    ON "AppDocumentAssignments" ("DocumentId", "IsCurrent")
    WHERE "IsCurrent" = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420090000_AddDocumentsPerformanceIndexes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260420090000_AddDocumentsPerformanceIndexes', '10.0.0');
    END IF;
END $EF$;
COMMIT;

