-- Chat message search/jump performance indexes (PostgreSQL)
-- Host DB: applied by EF migration AddChatMessagePerformanceIndexes (HCDbContext) via DbMigrator.
-- Tenant DB: same migration exists under TenantMigrations (HCTenantDbContext).
-- Use this script for CONCURRENTLY builds or when applying indexes outside EF (e.g. production DBA runbook).

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Anchor jump and keyset pagination support
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ChatMessages_ConversationId_CreationTime_Id"
ON "ChatMessages" ("ConversationId", "CreationTime" DESC, "Id" DESC);

-- Search by keyword with ILIKE '%keyword%'
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ChatMessages_Text_Trgm"
ON "ChatMessages" USING GIN ("Text" gin_trgm_ops);
