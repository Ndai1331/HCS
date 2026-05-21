-- Workflow OVERDUE status support + extension audit table
-- Run on production PostgreSQL after deploy

ALTER TABLE "AppDocumentWorkflowInstances"
    ADD COLUMN IF NOT EXISTS "OverdueAt" timestamp without time zone NULL,
    ADD COLUMN IF NOT EXISTS "ExtensionCount" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "TotalExtensionBusinessDays" integer NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS "AppDocumentWorkflowInstanceExtensions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NULL,
    "DocumentWorkflowInstanceId" uuid NOT NULL,
    "ExtendedByUserId" uuid NOT NULL,
    "ExtensionBusinessDays" integer NOT NULL,
    "PreviousFinishedAt" timestamp without time zone NOT NULL,
    "NewFinishedAt" timestamp without time zone NOT NULL,
    "Reason" character varying(2000) NOT NULL,
    "PreviousStatus" character varying(64) NULL,
    "NewStatus" character varying(64) NULL,
    "ExtraProperties" text NOT NULL DEFAULT '',
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp without time zone NOT NULL,
    "CreatorId" uuid NULL,
    "LastModificationTime" timestamp without time zone NULL,
    "LastModifierId" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeleterId" uuid NULL,
    "DeletionTime" timestamp without time zone NULL,
    CONSTRAINT "PK_AppDocumentWorkflowInstanceExtensions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppDocumentWorkflowInstanceExtensions_AppDocumentWorkflowInstances"
        FOREIGN KEY ("DocumentWorkflowInstanceId") REFERENCES "AppDocumentWorkflowInstances" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_AppDocumentWorkflowInstanceExtensions_DocumentWorkflowInstanceId"
    ON "AppDocumentWorkflowInstanceExtensions" ("DocumentWorkflowInstanceId");
