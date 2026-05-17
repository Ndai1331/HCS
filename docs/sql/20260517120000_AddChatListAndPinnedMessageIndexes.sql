-- Chat list + pinned messages indexes (same as migration 20260517120000_AddChatListAndPinnedMessageIndexes).
-- PostgreSQL. Idempotent (CREATE INDEX IF NOT EXISTS).
-- Run manually instead of DbMigrator if preferred; optionally register row in __EFMigrationsHistory (see bottom).

START TRANSACTION;

-- Composite index for conversation list filter: UserId + IsActive
CREATE INDEX IF NOT EXISTS "IX_ChatConversationMembers_UserId_IsActive"
ON "ChatConversationMembers" ("UserId", "IsActive");

-- Partial index for pinned messages per conversation
CREATE INDEX IF NOT EXISTS "IX_ChatMessages_ConversationId_IsPinned_Partial"
ON "ChatMessages" ("ConversationId", "IsPinned")
WHERE "IsPinned" = true;

-- Sort helper for conversation sidebar (LastMessageDate DESC)
CREATE INDEX IF NOT EXISTS "IX_ChatConversations_LastMessageDate"
ON "ChatConversations" ("LastMessageDate" DESC);

-- Keep EF migration history in sync (skip this block if you will run dotnet ef / DbMigrator for this migration anyway)
DO $EF$
BEGIN
    IF NOT EXISTS(
        SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20260517120000_AddChatListAndPinnedMessageIndexes'
    ) THEN
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260517120000_AddChatListAndPinnedMessageIndexes', '10.0.0');
    END IF;
END $EF$;

COMMIT;

-- Rollback (run separately if needed):
-- DROP INDEX IF EXISTS "IX_ChatConversations_LastMessageDate";
-- DROP INDEX IF EXISTS "IX_ChatMessages_ConversationId_IsPinned_Partial";
-- DROP INDEX IF EXISTS "IX_ChatConversationMembers_UserId_IsActive";
-- DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260517120000_AddChatListAndPinnedMessageIndexes';
