using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class StopReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StopReason",
                table: "OcpiPartnerSession",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StopReasonDetail",
                table: "OcpiPartnerSession",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StopReason",
                table: "OcpiHostedSession",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StopReasonDetail",
                table: "OcpiHostedSession",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StopReason",
                table: "ChargingSessions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StopReasonDetail",
                table: "ChargingSessions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StopReason",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "StopReasonDetail",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "StopReason",
                table: "OcpiHostedSession");

            migrationBuilder.DropColumn(
                name: "StopReasonDetail",
                table: "OcpiHostedSession");

            migrationBuilder.DropColumn(
                name: "StopReason",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "StopReasonDetail",
                table: "ChargingSessions");
        }
    }
}
