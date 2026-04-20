-- ============================================================================
-- Documents performance indexes — CONCURRENT version (for production DBs)
-- ============================================================================
-- CREATE INDEX CONCURRENTLY must run OUTSIDE a transaction block (no BEGIN/COMMIT).
-- Run each statement separately (psql \i will execute them one-by-one just fine).
-- If the server crashes during a CONCURRENT build, the index may be left INVALID
-- — drop it and re-run the CREATE.
--
-- After the indexes are live, register the migration so EF will not try to
-- re-create them. The INSERT at the bottom is idempotent.
-- ============================================================================

-- 1) AppMasterDatas (Type, Code) — used by UpdateDocumentStatusAsync + dropdowns.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_AppMasterDatas_Type_Code"
    ON "AppMasterDatas" ("Type", "Code");

-- 2) AppDocumentAssignments (DocumentId, IsCurrent) — filtered, current-only.
--    Used by ApplyPendingApprovalFlagsAsync, GetSentToMeDocumentIdsAsync,
--    ApplySubmitSigningButtonVisibilityAsync, RevokeDocumentAsync.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_AppDocumentAssignments_DocumentId_IsCurrent"
    ON "AppDocumentAssignments" ("DocumentId", "IsCurrent")
    WHERE "IsCurrent" = true;

-- 3) Register the migration in EF history so `dotnet ef database update` / the
--    DbMigrator host won't try to re-apply it on this DB.
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260420090000_AddDocumentsPerformanceIndexes', '10.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260420090000_AddDocumentsPerformanceIndexes'
);

-- ============================================================================
-- Verification (run manually):
--   SELECT indexname, indexdef FROM pg_indexes
--   WHERE indexname IN (
--       'IX_AppMasterDatas_Type_Code',
--       'IX_AppDocumentAssignments_DocumentId_IsCurrent'
--   );
-- ============================================================================
