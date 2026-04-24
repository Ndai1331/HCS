
-- 1) AppMasterDatas (Type, Code) — used by UpdateDocumentStatusAsync + dropdowns.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_AppMasterDatas_Type_Code"
    ON "AppMasterDatas" ("Type", "Code");

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