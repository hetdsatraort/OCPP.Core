using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class OCPISoC2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentStateOfCharge",
                table: "OcpiHostedSession",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StateOfChargeLastUpdate",
                table: "OcpiHostedSession",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStateOfCharge",
                table: "OcpiHostedSession");

            migrationBuilder.DropColumn(
                name: "StateOfChargeLastUpdate",
                table: "OcpiHostedSession");
        }
    }
}
