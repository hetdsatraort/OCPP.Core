using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChargingHubLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BatteryIncreaseLimit",
                table: "ChargingSessions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CostLimit",
                table: "ChargingSessions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EnergyLimit",
                table: "ChargingSessions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimit",
                table: "ChargingSessions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryIncreaseLimit",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "CostLimit",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "EnergyLimit",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "TimeLimit",
                table: "ChargingSessions");
        }
    }
}
