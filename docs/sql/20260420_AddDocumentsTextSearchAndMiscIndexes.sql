START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420100000_AddDocumentsTextSearchAndMiscIndexes') THEN
    CREATE EXTENSION IF NOT EXISTS pg_trgm;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420100000_AddDocumentsTextSearchAndMiscIndexes') THEN
    CREATE INDEX IF NOT EXISTS "IX_AppDocuments_No_Trgm"
    ON "AppDocuments" USING GIN ("No" gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420100000_AddDocumentsTextSearchAndMiscIndexes') THEN
    CREATE INDEX IF NOT EXISTS "IX_AppDocuments_Title_Trgm"
    ON "AppDocuments" USING GIN ("Title" gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420100000_AddDocumentsTextSearchAndMiscIndexes') THEN
    CREATE INDEX IF NOT EXISTS "IX_AppDocuments_StorageNumber_Trgm"
    ON "AppDocuments" USING GIN ("StorageNumber" gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420100000_AddDocumentsTextSearchAndMiscIndexes') THEN
    CREATE INDEX IF NOT EXISTS "IX_AppDocuments_CreatorId"
    ON "AppDocuments" ("CreatorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420100000_AddDocumentsTextSearchAndMiscIndexes') THEN
    CREATE INDEX IF NOT EXISTS "IX_AppUserSignatures_IdentityUserId_IsActive"
    ON "AppUserSignatures" ("IdentityUserId", "IsActive")
    WHERE "IsActive" = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420100000_AddDocumentsTextSearchAndMiscIndexes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260420100000_AddDocumentsTextSearchAndMiscIndexes', '10.0.0');
    END IF;
END $EF$;
COMMIT;

