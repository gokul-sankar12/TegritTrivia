using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TegritTriviaFullStack.Migrations
{
    /// <inheritdoc />
    public partial class InitialUserStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageScore",
                table: "AspNetUsers",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<long>(
                name: "NumPerfectScores",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "QuizzesSubmitted",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageScore",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NumPerfectScores",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "QuizzesSubmitted",
                table: "AspNetUsers");
        }
    }
}
