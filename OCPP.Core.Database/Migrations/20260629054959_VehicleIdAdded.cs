using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class VehicleIdAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VehicleId",
                table: "ChargingSessions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargingSessions_VehicleId",
                table: "ChargingSessions",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargingSession_UserVehicle",
                table: "ChargingSessions",
                column: "VehicleId",
                principalTable: "UserVehicles",
                principalColumn: "RecId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargingSession_UserVehicle",
                table: "ChargingSessions");

            migrationBuilder.DropIndex(
                name: "IX_ChargingSessions_VehicleId",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "ChargingSessions");
        }
    }
}
