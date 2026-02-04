using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class SoC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "SoCEnd",
                table: "ChargingSessions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoCLastUpdate",
                table: "ChargingSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SoCStart",
                table: "ChargingSessions",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoCEnd",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "SoCLastUpdate",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "SoCStart",
                table: "ChargingSessions");
        }
    }
}
