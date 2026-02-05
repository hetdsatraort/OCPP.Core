using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class UserIdForReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ChargingHubReviews",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargingHubReviews_UserId",
                table: "ChargingHubReviews",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargingHubReview_Users",
                table: "ChargingHubReviews",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "RecId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargingHubReview_Users",
                table: "ChargingHubReviews");

            migrationBuilder.DropIndex(
                name: "IX_ChargingHubReviews_UserId",
                table: "ChargingHubReviews");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChargingHubReviews");
        }
    }
}
