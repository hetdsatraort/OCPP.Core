using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemovePartyIdUnique2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OcpiPartnerCredential_CountryCode_PartyId",
                table: "OcpiPartnerCredential");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerCredential_CountryCode_PartyId",
                table: "OcpiPartnerCredential",
                columns: new[] { "CountryCode", "PartyId" },
                unique: true,
                filter: "[IsActive] = 1");
        }
    }
}
