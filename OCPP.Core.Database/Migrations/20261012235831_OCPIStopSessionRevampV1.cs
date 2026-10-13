using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class OCPIStopSessionRevampV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBilled",
                table: "OcpiPartnerSession",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStopAttemptAt",
                table: "OcpiPartnerSession",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StopAttempts",
                table: "OcpiPartnerSession",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE OcpiPartnerSession SET IsBilled = 1
                WHERE LimitViolationHandled = 1
                OR Status NOT IN ('ACTIVE', 'PENDING');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBilled",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "LastStopAttemptAt",
                table: "OcpiPartnerSession");

            migrationBuilder.DropColumn(
                name: "StopAttempts",
                table: "OcpiPartnerSession");
        }
    }
}
