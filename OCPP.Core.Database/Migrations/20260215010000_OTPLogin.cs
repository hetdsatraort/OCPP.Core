using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class OTPLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OtpValidations",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AuthId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    OtpCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RequestIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VerifyIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpValidations", x => x.RecId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtpValidations_AuthId",
                table: "OtpValidations",
                column: "AuthId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpValidations_PhoneNumber",
                table: "OtpValidations",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_OtpValidations_PhoneNumber_CreatedAt",
                table: "OtpValidations",
                columns: new[] { "PhoneNumber", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtpValidations");
        }
    }
}
