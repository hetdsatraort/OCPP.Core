using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class OcpiPartnerLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BatteryIncreaseLimit",
                table: "OcpiPartnerSession",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CostLimit",
                table: "OcpiPartnerSession",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EnergyLimit",
                table: "OcpiPartnerSession",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LimitViolationHandled",
                table: "OcpiPartnerSession",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimit",
                table: "OcpiPartnerSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "OcpiPartnerSession",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryIncreaseLimit",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "CostLimit",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "EnergyLimit",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "LimitViolationHandled",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "TimeLimit",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "OcpiPartnerSession");
        }
    }
}
