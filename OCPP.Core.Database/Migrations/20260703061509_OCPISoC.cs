using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class OCPISoC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentStateOfCharge",
                table: "OcpiPartnerSession",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StateOfChargeLastUpdate",
                table: "OcpiPartnerSession",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStateOfCharge",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "StateOfChargeLastUpdate",
                table: "OcpiPartnerSession");
        }
    }
}
