using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.TenantMigrations
{
    /// <inheritdoc />
    public partial class AddChatMessagePerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_ChatMessages_ConversationId";
                CREATE INDEX IF NOT EXISTS "IX_ChatMessages_ConversationId_CreationTime_Id"
                ON "ChatMessages" ("ConversationId", "CreationTime" DESC, "Id" DESC);
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "AppDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FromUserId",
                table: "AppDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentDocumentId",
                table: "AppDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiverUserId",
                table: "AppDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_DepartmentId",
                table: "AppDocuments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_FromUserId",
                table: "AppDocuments",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_ParentDocumentId",
                table: "AppDocuments",
                column: "ParentDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_ReceiverUserId",
                table: "AppDocuments",
                column: "ReceiverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_SourceType",
                table: "AppDocuments",
                column: "SourceType");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocuments_AbpUsers_FromUserId",
                table: "AppDocuments",
                column: "FromUserId",
                principalTable: "AbpUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocuments_AbpUsers_ReceiverUserId",
                table: "AppDocuments",
                column: "ReceiverUserId",
                principalTable: "AbpUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocuments_AppDepartments_DepartmentId",
                table: "AppDocuments",
                column: "DepartmentId",
                principalTable: "AppDepartments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocuments_AppDocuments_ParentDocumentId",
                table: "AppDocuments",
                column: "ParentDocumentId",
                principalTable: "AppDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_ChatMessages_Text_Trgm"
                ON "ChatMessages" USING GIN ("Text" gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ChatMessages_Text_Trgm"";");

            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AbpUsers_FromUserId",
                table: "AppDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AbpUsers_ReceiverUserId",
                table: "AppDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AppDepartments_DepartmentId",
                table: "AppDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AppDocuments_ParentDocumentId",
                table: "AppDocuments");

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_ChatMessages_ConversationId_CreationTime_Id";
                CREATE INDEX IF NOT EXISTS "IX_ChatMessages_ConversationId" ON "ChatMessages" ("ConversationId");
                """);

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_DepartmentId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_FromUserId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_ParentDocumentId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_ReceiverUserId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_SourceType",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "FromUserId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "ParentDocumentId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "ReceiverUserId",
                table: "AppDocuments");
        }
    }
}
