START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074013_AddChatMessagePerformanceIndexes') THEN
        DROP INDEX IF EXISTS "IX_ChatMessages_ConversationId";
        CREATE INDEX IF NOT EXISTS "IX_ChatMessages_ConversationId_CreationTime_Id"
        ON "ChatMessages" ("ConversationId", "CreationTime" DESC, "Id" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074013_AddChatMessagePerformanceIndexes') THEN
        CREATE EXTENSION IF NOT EXISTS pg_trgm;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074013_AddChatMessagePerformanceIndexes') THEN
        CREATE INDEX IF NOT EXISTS "IX_ChatMessages_Text_Trgm"
        ON "ChatMessages" USING GIN ("Text" gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412074013_AddChatMessagePerformanceIndexes') THEN
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260412074013_AddChatMessagePerformanceIndexes', '10.0.0');
    END IF;
END $EF$;

COMMIT;
