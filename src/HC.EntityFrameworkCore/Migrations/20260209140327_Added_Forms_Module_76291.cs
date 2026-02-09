using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HC.Migrations
{
    /// <inheritdoc />
    public partial class Added_Forms_Module_76291 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FrmFormResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FormId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrmFormResponses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FrmForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CanEditResponse = table.Column<bool>(type: "boolean", nullable: false),
                    IsCollectingEmail = table.Column<bool>(type: "boolean", nullable: false),
                    HasLimitOneResponsePerUser = table.Column<bool>(type: "boolean", nullable: false),
                    IsAcceptingResponses = table.Column<bool>(type: "boolean", nullable: false),
                    IsQuiz = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresLogin = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrmForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FrmQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FormId = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: true),
                    HasOtherOption = table.Column<bool>(type: "boolean", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrmQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FrmAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrmAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrmAnswers_FrmFormResponses_FormResponseId",
                        column: x => x.FormResponseId,
                        principalTable: "FrmFormResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FrmChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChoosableQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrmChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrmChoices_FrmQuestions_ChoosableQuestionId",
                        column: x => x.ChoosableQuestionId,
                        principalTable: "FrmQuestions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FrmAnswers_FormResponseId",
                table: "FrmAnswers",
                column: "FormResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_FrmAnswers_Id_QuestionId_FormResponseId_TenantId",
                table: "FrmAnswers",
                columns: new[] { "Id", "QuestionId", "FormResponseId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FrmChoices_ChoosableQuestionId",
                table: "FrmChoices",
                column: "ChoosableQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_FrmChoices_Id_ChoosableQuestionId_TenantId",
                table: "FrmChoices",
                columns: new[] { "Id", "ChoosableQuestionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FrmFormResponses_Id_FormId_UserId_TenantId",
                table: "FrmFormResponses",
                columns: new[] { "Id", "FormId", "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FrmForms_Id_TenantId",
                table: "FrmForms",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_FrmQuestions_Id_FormId_TenantId",
                table: "FrmQuestions",
                columns: new[] { "Id", "FormId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FrmAnswers");

            migrationBuilder.DropTable(
                name: "FrmChoices");

            migrationBuilder.DropTable(
                name: "FrmForms");

            migrationBuilder.DropTable(
                name: "FrmFormResponses");

            migrationBuilder.DropTable(
                name: "FrmQuestions");
        }
    }
}
