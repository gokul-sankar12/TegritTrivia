using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TegritTriviaFullStack.Migrations
{
    /// <inheritdoc />
    public partial class UserQuizTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserQuiz",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuizDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TriviaResponseId = table.Column<int>(type: "int", nullable: false),
                    IsSubmitted = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserQuiz", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserQuiz_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserQuiz_TriviaResponse_TriviaResponseId",
                        column: x => x.TriviaResponseId,
                        principalTable: "TriviaResponse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TriviaResultId = table.Column<int>(type: "int", nullable: false),
                    SelectedOption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserQuizId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAnswers_TriviaResults_TriviaResultId",
                        column: x => x.TriviaResultId,
                        principalTable: "TriviaResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserAnswers_UserQuiz_UserQuizId",
                        column: x => x.UserQuizId,
                        principalTable: "UserQuiz",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswers_TriviaResultId",
                table: "UserAnswers",
                column: "TriviaResultId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAnswers_UserQuizId",
                table: "UserAnswers",
                column: "UserQuizId");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuiz_TriviaResponseId",
                table: "UserQuiz",
                column: "TriviaResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuiz_UserId",
                table: "UserQuiz",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAnswers");

            migrationBuilder.DropTable(
                name: "UserQuiz");
        }
    }
}
