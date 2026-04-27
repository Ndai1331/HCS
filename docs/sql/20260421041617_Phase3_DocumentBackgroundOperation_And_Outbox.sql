-- Manual script equivalent to EF migration: 20260421041617_Phase3_DocumentBackgroundOperation_And_Outbox
-- Target: PostgreSQL (public schema, quoted identifiers = same as Npgsql/EF)
--
-- Prerequisite: all migrations BEFORE this one (see chain up to 20260420100000_AddDocumentsTextSearchAndMiscIndexes) must already be applied.
-- If prod is behind, apply earlier migrations (or `dotnet ef database update` from a deploy tool) first.
--
-- After success, EF Core will see this migration as applied if you also insert the history row (section at bottom).

BEGIN;

-- 1) Remove single-column index on DocumentId (wider index added in a later performance migration may supersede; matches EF Up())
DROP INDEX IF EXISTS "IX_AppDocumentAssignments_DocumentId";

-- 2) Widen CommittedStepTemplateIdsJson to unlimited text (from varchar(8000))
ALTER TABLE "AppDocumentWorkflowInstances"
    ALTER COLUMN "CommittedStepTemplateIdsJson" TYPE text;

-- 3) Document background operation tracking
CREATE TABLE IF NOT EXISTS "AppDocumentBackgroundOperations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NULL,
    "UserId" uuid NOT NULL,
    "DocumentId" uuid NULL,
    "OperationType" character varying(64) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "Progress" integer NOT NULL,
    "Message" character varying(512) NULL,
    "ErrorMessage" text NULL,
    "InputJson" text NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp without time zone NOT NULL,
    "CreatorId" uuid NULL,
    "LastModificationTime" timestamp without time zone NULL,
    "LastModifierId" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeleterId" uuid NULL,
    "DeletionTime" timestamp without time zone NULL,
    CONSTRAINT "PK_AppDocumentBackgroundOperations" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_AppDocumentBackgroundOperations_Status"
    ON "AppDocumentBackgroundOperations" ("Status");

CREATE INDEX IF NOT EXISTS "IX_AppDocumentBackgroundOperations_TenantId_UserId_CreationTime"
    ON "AppDocumentBackgroundOperations" ("TenantId", "UserId", "CreationTime");

-- 4) Notification outbox
CREATE TABLE IF NOT EXISTS "AppNotificationOutboxes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NULL,
    "EventType" character varying(128) NOT NULL,
    "PayloadJson" text NOT NULL,
    "ProcessedTime" timestamp without time zone NULL,
    "RetryCount" integer NOT NULL,
    "ErrorMessage" text NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp without time zone NOT NULL,
    "CreatorId" uuid NULL,
    CONSTRAINT "PK_AppNotificationOutboxes" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_AppNotificationOutboxes_ProcessedTime_CreationTime"
    ON "AppNotificationOutboxes" ("ProcessedTime", "CreationTime");

-- 5) Mark migration as applied (so DbMigrator / `dotnet ef` will not re-run it)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260421041617_Phase3_DocumentBackgroundOperation_And_Outbox', '10.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260421041617_Phase3_DocumentBackgroundOperation_And_Outbox'
);

COMMIT;
