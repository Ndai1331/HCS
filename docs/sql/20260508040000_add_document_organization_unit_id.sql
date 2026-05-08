ALTER TABLE "AppDocuments"
ADD COLUMN IF NOT EXISTS "OrganizationUnitId" uuid NULL;

CREATE INDEX IF NOT EXISTS "IX_AppDocuments_OrganizationUnitId"
ON "AppDocuments" ("OrganizationUnitId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260508040000_AddDocumentOrganizationUnitId', '10.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260508040000_AddDocumentOrganizationUnitId'
);
