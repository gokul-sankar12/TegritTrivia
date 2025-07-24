using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TegritTriviaFullStack.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedUserQuizTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserOptions",
                table: "UserQuiz",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserOptions",
                table: "UserQuiz");
        }
    }
}
