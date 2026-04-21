-- M6 + M7 (documents-performance-optimization mid-term)
-- Production-safe version of 20260420100000_AddDocumentsTextSearchAndMiscIndexes.
--
-- NOTE:
--   - CREATE INDEX CONCURRENTLY cannot run inside a transaction block; run each
--     statement by itself (psql -c "..." or split the file before executing).
--   - CREATE EXTENSION is idempotent and can run in any context.
--   - Safe to re-run: every CREATE uses IF NOT EXISTS.
--   - After all indexes are in place, the final block inserts the migration row into
--     __EFMigrationsHistory so EF Core will not try to re-run the C# migration.

-- 1) pg_trgm extension (small, idempotent, no lock).
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- 2) GIN trigram index on Documents.No — speeds up `ILIKE '%term%'` filters
--    and the new exact-match duplicate check (M8) on very large tables.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_AppDocuments_No_Trgm"
    ON "AppDocuments" USING GIN ("No" gin_trgm_ops);

-- 3) GIN trigram index on Documents.Title — same rationale as above for Title search.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_AppDocuments_Title_Trgm"
    ON "AppDocuments" USING GIN ("Title" gin_trgm_ops);

-- 4) GIN trigram index on Documents.StorageNumber — speeds up duplicate check and
--    filter by storage number.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_AppDocuments_StorageNumber_Trgm"
    ON "AppDocuments" USING GIN ("StorageNumber" gin_trgm_ops);

-- 5) B-tree on Documents.CreatorId — most list queries filter by creator.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_AppDocuments_CreatorId"
    ON "AppDocuments" ("CreatorId");

-- 6) Partial composite index on UserSignatures(IdentityUserId, IsActive).
--    Matches the signing modal's hot path: WHERE IdentityUserId = $1 AND IsActive = true.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_AppUserSignatures_IdentityUserId_IsActive"
    ON "AppUserSignatures" ("IdentityUserId", "IsActive")
    WHERE "IsActive" = true;

-- 7) Record the migration so EF Core treats it as applied.
--    Run this in its own transaction (not inside the CONCURRENTLY batch above).
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260420100000_AddDocumentsTextSearchAndMiscIndexes', '10.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260420100000_AddDocumentsTextSearchAndMiscIndexes'
);
