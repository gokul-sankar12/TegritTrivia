using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TegritTriviaFullStack.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuizForm",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TriviaResponseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizForm", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriviaResponse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseCode = table.Column<int>(type: "int", nullable: false),
                    QuizDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriviaResponse", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriviaResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TriviaResponseId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Difficulty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Correct_Answer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Incorrect_Answers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Options = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IncorrectAnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SelectedOption = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriviaResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriviaResults_TriviaResponse_TriviaResponseId",
                        column: x => x.TriviaResponseId,
                        principalTable: "TriviaResponse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TriviaResults_TriviaResponseId",
                table: "TriviaResults",
                column: "TriviaResponseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizForm");

            migrationBuilder.DropTable(
                name: "TriviaResults");

            migrationBuilder.DropTable(
                name: "TriviaResponse");
        }
    }
}
