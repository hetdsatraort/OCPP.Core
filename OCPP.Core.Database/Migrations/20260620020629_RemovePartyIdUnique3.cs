using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemovePartyIdUnique3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerCredential_Role_CountryCode_PartyId",
                table: "OcpiPartnerCredential",
                columns: new[] { "CountryCode", "PartyId", "Role" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OcpiPartnerCredential_Role_CountryCode_PartyId",
                table: "OcpiPartnerCredential");
        }
    }
}
