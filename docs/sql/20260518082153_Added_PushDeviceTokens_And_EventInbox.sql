START TRANSACTION;
CREATE TABLE "AbpEventInbox" (
    "Id" uuid NOT NULL,
    "ExtraProperties" text NOT NULL,
    "MessageId" text NOT NULL,
    "EventName" character varying(256) NOT NULL,
    "EventData" bytea NOT NULL,
    "CreationTime" timestamp without time zone NOT NULL,
    "Status" integer NOT NULL,
    "HandledTime" timestamp without time zone,
    "RetryCount" integer NOT NULL,
    "NextRetryTime" timestamp without time zone,
    CONSTRAINT "PK_AbpEventInbox" PRIMARY KEY ("Id")
);

CREATE TABLE "AppUserPushDeviceTokens" (
    "Id" uuid NOT NULL,
    "TenantId" uuid,
    "UserId" uuid NOT NULL,
    "FcmToken" character varying(512) NOT NULL,
    "Platform" character varying(32) NOT NULL,
    "DeviceId" character varying(128),
    "IsActive" boolean NOT NULL,
    "LastSeenTime" timestamp without time zone NOT NULL,
    "ExtraProperties" text NOT NULL,
    "ConcurrencyStamp" character varying(40) NOT NULL,
    "CreationTime" timestamp without time zone NOT NULL,
    "CreatorId" uuid,
    "LastModificationTime" timestamp without time zone,
    "LastModifierId" uuid,
    CONSTRAINT "PK_AppUserPushDeviceTokens" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_AbpEventInbox_MessageId" ON "AbpEventInbox" ("MessageId");

CREATE INDEX "IX_AbpEventInbox_Status_CreationTime" ON "AbpEventInbox" ("Status", "CreationTime");

CREATE INDEX "IX_AppUserPushDeviceTokens_FcmToken" ON "AppUserPushDeviceTokens" ("FcmToken");

CREATE INDEX "IX_AppUserPushDeviceTokens_TenantId_UserId_DeviceId" ON "AppUserPushDeviceTokens" ("TenantId", "UserId", "DeviceId");

CREATE INDEX "IX_AppUserPushDeviceTokens_TenantId_UserId_IsActive" ON "AppUserPushDeviceTokens" ("TenantId", "UserId", "IsActive");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260518082153_Added_PushDeviceTokens_And_EventInbox', '10.0.0');

COMMIT;


