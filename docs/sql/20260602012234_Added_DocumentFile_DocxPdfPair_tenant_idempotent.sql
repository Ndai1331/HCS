-- Migration (idempotent subset): DocumentFile DOCX/PDF pair columns (tenant DB)
-- Full tenant migration: docs/sql/20260602012234_Added_DocumentFile_DocxPdfPair_tenant.sql
-- Target: Tenant database(s)
-- Safe to run multiple times per tenant schema

START TRANSACTION;

ALTER TABLE "AppDocumentFiles"
    ADD COLUMN IF NOT EXISTS "SourceDocxFileId" uuid NULL;

ALTER TABLE "AppDocumentFiles"
    ADD COLUMN IF NOT EXISTS "DerivedPdfFileId" uuid NULL;

CREATE INDEX IF NOT EXISTS "IX_AppDocumentFiles_SourceDocxFileId"
    ON "AppDocumentFiles" ("SourceDocxFileId");

CREATE INDEX IF NOT EXISTS "IX_AppDocumentFiles_DerivedPdfFileId"
    ON "AppDocumentFiles" ("DerivedPdfFileId");

DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_AppDocumentFiles_AppDocumentFiles_SourceDocxFileId'
    ) THEN
        ALTER TABLE "AppDocumentFiles"
            ADD CONSTRAINT "FK_AppDocumentFiles_AppDocumentFiles_SourceDocxFileId"
                FOREIGN KEY ("SourceDocxFileId")
                REFERENCES "AppDocumentFiles" ("Id")
                ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_AppDocumentFiles_AppDocumentFiles_DerivedPdfFileId'
    ) THEN
        ALTER TABLE "AppDocumentFiles"
            ADD CONSTRAINT "FK_AppDocumentFiles_AppDocumentFiles_DerivedPdfFileId"
                FOREIGN KEY ("DerivedPdfFileId")
                REFERENCES "AppDocumentFiles" ("Id")
                ON DELETE SET NULL;
    END IF;
END $EF$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260602012234_Added_DocumentFile_DocxPdfPair', '10.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260602012234_Added_DocumentFile_DocxPdfPair'
);

COMMIT;
