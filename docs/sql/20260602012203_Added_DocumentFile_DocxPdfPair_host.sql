START TRANSACTION;
DROP INDEX "IX_AppDocumentWorkflowInstances_DocumentId";

DROP INDEX "IX_AppDocumentFiles_DocumentId";

DROP INDEX "IX_AppDocumentAssignments_ReceiverUserId";

ALTER TABLE "AppDocumentWorkflowInstances" ADD "ExtensionCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "AppDocumentWorkflowInstances" ADD "OverdueAt" timestamp without time zone;

ALTER TABLE "AppDocumentWorkflowInstances" ADD "TotalExtensionBusinessDays" integer NOT NULL DEFAULT 0;

ALTER TABLE "AppDocumentFiles" ADD "DerivedPdfFileId" uuid;

ALTER TABLE "AppDocumentFiles" ADD "SourceDocxFileId" uuid;

CREATE TABLE "AppDocumentWorkflowInstanceExtensions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "DocumentWorkflowInstanceId" uuid NOT NULL,
    "ExtendedByUserId" uuid NOT NULL,
    "ExtensionBusinessDays" integer NOT NULL,
    "PreviousFinishedAt" timestamp without time zone NOT NULL,
    "NewFinishedAt" timestamp without time zone NOT NULL,
    "Reason" character varying(2000) NOT NULL,
    "PreviousStatus" character varying(64),
    "NewStatus" character varying(64),
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp without time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp without time zone,
    "LastModifierId" uuid,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeleterId" uuid,
    "DeletionTime" timestamp without time zone,
    CONSTRAINT "PK_AppDocumentWorkflowInstanceExtensions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppDocumentWorkflowInstanceExtensions_AppDocumentWorkflowIn~" FOREIGN KEY ("DocumentWorkflowInstanceId") REFERENCES "AppDocumentWorkflowInstances" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_AppDocumentWorkflowInstances_DocumentId_StartedAt" ON "AppDocumentWorkflowInstances" ("DocumentId", "StartedAt");

CREATE INDEX "IX_AppDocumentWorkflowInstances_Status_FinishedAt" ON "AppDocumentWorkflowInstances" ("Status", "FinishedAt") WHERE "Status" IN ('IN_PROGRESS', 'OVERDUE');

CREATE INDEX "IX_AppDocumentFiles_DerivedPdfFileId" ON "AppDocumentFiles" ("DerivedPdfFileId");

CREATE INDEX "IX_AppDocumentFiles_DocumentId_UploadedAt" ON "AppDocumentFiles" ("DocumentId", "UploadedAt");

CREATE INDEX "IX_AppDocumentFiles_SourceDocxFileId" ON "AppDocumentFiles" ("SourceDocxFileId");

CREATE INDEX "IX_AppDocumentAssignments_ReceiverUserId_Status_WorkflowStepTemplateId" ON "AppDocumentAssignments" ("ReceiverUserId", "Status", "WorkflowStepTemplateId");

CREATE INDEX "IX_AppDocumentWorkflowInstanceExtensions_DocumentWorkflowInsta~" ON "AppDocumentWorkflowInstanceExtensions" ("DocumentWorkflowInstanceId");

ALTER TABLE "AppDocumentFiles" ADD CONSTRAINT "FK_AppDocumentFiles_AppDocumentFiles_DerivedPdfFileId" FOREIGN KEY ("DerivedPdfFileId") REFERENCES "AppDocumentFiles" ("Id") ON DELETE SET NULL;

ALTER TABLE "AppDocumentFiles" ADD CONSTRAINT "FK_AppDocumentFiles_AppDocumentFiles_SourceDocxFileId" FOREIGN KEY ("SourceDocxFileId") REFERENCES "AppDocumentFiles" ("Id") ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260602012203_Added_DocumentFile_DocxPdfPair', '10.0.0');

COMMIT;

