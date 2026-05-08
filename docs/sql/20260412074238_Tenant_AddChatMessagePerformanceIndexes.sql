START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        DROP INDEX IF EXISTS "IX_ChatMessages_ConversationId";
        CREATE INDEX IF NOT EXISTS "IX_ChatMessages_ConversationId_CreationTime_Id"
        ON "ChatMessages" ("ConversationId", "CreationTime" DESC, "Id" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        ALTER TABLE "AppDocuments" ADD COLUMN IF NOT EXISTS "DepartmentId" uuid;
        ALTER TABLE "AppDocuments" ADD COLUMN IF NOT EXISTS "FromUserId" uuid;
        ALTER TABLE "AppDocuments" ADD COLUMN IF NOT EXISTS "ParentDocumentId" uuid;
        ALTER TABLE "AppDocuments" ADD COLUMN IF NOT EXISTS "ReceiverUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        CREATE INDEX IF NOT EXISTS "IX_AppDocuments_DepartmentId" ON "AppDocuments" ("DepartmentId");
        CREATE INDEX IF NOT EXISTS "IX_AppDocuments_FromUserId" ON "AppDocuments" ("FromUserId");
        CREATE INDEX IF NOT EXISTS "IX_AppDocuments_ParentDocumentId" ON "AppDocuments" ("ParentDocumentId");
        CREATE INDEX IF NOT EXISTS "IX_AppDocuments_ReceiverUserId" ON "AppDocuments" ("ReceiverUserId");
        CREATE INDEX IF NOT EXISTS "IX_AppDocuments_SourceType" ON "AppDocuments" ("SourceType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        ALTER TABLE "AppDocuments"
            ADD CONSTRAINT "FK_AppDocuments_AbpUsers_FromUserId"
            FOREIGN KEY ("FromUserId") REFERENCES "AbpUsers" ("Id") ON DELETE SET NULL;
    END IF;
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        ALTER TABLE "AppDocuments"
            ADD CONSTRAINT "FK_AppDocuments_AbpUsers_ReceiverUserId"
            FOREIGN KEY ("ReceiverUserId") REFERENCES "AbpUsers" ("Id") ON DELETE SET NULL;
    END IF;
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        ALTER TABLE "AppDocuments"
            ADD CONSTRAINT "FK_AppDocuments_AppDepartments_DepartmentId"
            FOREIGN KEY ("DepartmentId") REFERENCES "AppDepartments" ("Id") ON DELETE SET NULL;
    END IF;
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        ALTER TABLE "AppDocuments"
            ADD CONSTRAINT "FK_AppDocuments_AppDocuments_ParentDocumentId"
            FOREIGN KEY ("ParentDocumentId") REFERENCES "AppDocuments" ("Id") ON DELETE RESTRICT;
    END IF;
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        CREATE EXTENSION IF NOT EXISTS pg_trgm;
        CREATE INDEX IF NOT EXISTS "IX_ChatMessages_Text_Trgm"
        ON "ChatMessages" USING GIN ("Text" gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074238_AddChatMessagePerformanceIndexes') THEN
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260412074238_AddChatMessagePerformanceIndexes', '10.0.0');
    END IF;
END $EF$;

COMMIT;
