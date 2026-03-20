using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class Added_Document_From_Receiver_Department : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "IX_AppDocuments_ReceiverUserId",
                table: "AppDocuments",
                column: "ReceiverUserId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AbpUsers_FromUserId",
                table: "AppDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AbpUsers_ReceiverUserId",
                table: "AppDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AppDepartments_DepartmentId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_DepartmentId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_FromUserId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_ReceiverUserId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "FromUserId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "ReceiverUserId",
                table: "AppDocuments");
        }
    }
}
