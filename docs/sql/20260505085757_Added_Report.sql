START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260505085757_Added_Report') THEN
        CREATE TABLE IF NOT EXISTS "AppReports" (
            "Id" uuid NOT NULL,
            "ExtraProperties" text NOT NULL,
            "ConcurrencyStamp" character varying(40) NOT NULL,
            "CreationTime" timestamp without time zone NOT NULL,
            "CreatorId" uuid NULL,
            "LastModificationTime" timestamp without time zone NULL,
            "LastModifierId" uuid NULL,
            "IsDeleted" boolean NOT NULL DEFAULT false,
            "DeleterId" uuid NULL,
            "DeletionTime" timestamp without time zone NULL,
            "Name" character varying(255) NOT NULL,
            "Url" character varying(1000) NOT NULL,
            "SortOrder" integer NOT NULL,
            "Image" character varying(255) NULL,
            CONSTRAINT "PK_AppReports" PRIMARY KEY ("Id")
        );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260505085757_Added_Report') THEN
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260505085757_Added_Report', '10.0.0');
    END IF;
END $EF$;

COMMIT;
