-- Chat message search/jump performance indexes (PostgreSQL)
-- Run this script manually in production during low traffic.

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Anchor jump and keyset pagination support
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ChatMessages_ConversationId_CreationTime_Id"
ON "ChatMessages" ("ConversationId", "CreationTime" DESC, "Id" DESC);

-- Search by keyword with ILIKE '%keyword%'
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ChatMessages_Text_Trgm"
ON "ChatMessages" USING GIN ("Text" gin_trgm_ops);
