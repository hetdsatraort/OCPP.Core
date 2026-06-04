using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class EditPartnerCredentialIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OcpiPartnerCredential_CountryCode_PartyId",
                table: "OcpiPartnerCredential");

            migrationBuilder.DropIndex(
                name: "IX_OcpiPartnerCredential_Token",
                table: "OcpiPartnerCredential");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerCredential_CountryCode_PartyId",
                table: "OcpiPartnerCredential",
                columns: new[] { "CountryCode", "PartyId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerCredential_Token",
                table: "OcpiPartnerCredential",
                column: "Token",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OcpiPartnerCredential_CountryCode_PartyId",
                table: "OcpiPartnerCredential");

            migrationBuilder.DropIndex(
                name: "IX_OcpiPartnerCredential_Token",
                table: "OcpiPartnerCredential");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerCredential_CountryCode_PartyId",
                table: "OcpiPartnerCredential",
                columns: new[] { "CountryCode", "PartyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerCredential_Token",
                table: "OcpiPartnerCredential",
                column: "Token",
                unique: true);
        }
    }
}
