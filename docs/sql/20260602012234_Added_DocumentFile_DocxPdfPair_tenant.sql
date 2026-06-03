START TRANSACTION;
ALTER TABLE "AppWorkflowStepAssignments" ADD "AssigneeType" text NOT NULL DEFAULT '';

ALTER TABLE "AppWorkflowStepAssignments" ADD "RoleId" uuid;

ALTER TABLE "AppDocumentWorkflowInstances" ALTER COLUMN "CommittedStepTemplateIdsJson" TYPE text;

ALTER TABLE "AppDocumentWorkflowInstances" ADD "ExtensionCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "AppDocumentWorkflowInstances" ADD "OverdueAt" timestamp without time zone;

ALTER TABLE "AppDocumentWorkflowInstances" ADD "TotalExtensionBusinessDays" integer NOT NULL DEFAULT 0;

ALTER TABLE "AppDocuments" ADD "OrganizationUnitId" uuid;

ALTER TABLE "AppDocumentFiles" ADD "DerivedPdfFileId" uuid;

ALTER TABLE "AppDocumentFiles" ADD "SourceDocxFileId" uuid;

CREATE INDEX "IX_AppDocumentFiles_DerivedPdfFileId" ON "AppDocumentFiles" ("DerivedPdfFileId");

CREATE INDEX "IX_AppDocumentFiles_SourceDocxFileId" ON "AppDocumentFiles" ("SourceDocxFileId");

ALTER TABLE "AppDocumentFiles" ADD CONSTRAINT "FK_AppDocumentFiles_AppDocumentFiles_DerivedPdfFileId" FOREIGN KEY ("DerivedPdfFileId") REFERENCES "AppDocumentFiles" ("Id") ON DELETE SET NULL;

ALTER TABLE "AppDocumentFiles" ADD CONSTRAINT "FK_AppDocumentFiles_AppDocumentFiles_SourceDocxFileId" FOREIGN KEY ("SourceDocxFileId") REFERENCES "AppDocumentFiles" ("Id") ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260602012234_Added_DocumentFile_DocxPdfPair', '10.0.0');

COMMIT;

