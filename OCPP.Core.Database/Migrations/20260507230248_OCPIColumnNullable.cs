using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class OCPIColumnNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OcpiPartnerSession_PartnerCredential",
                table: "OcpiPartnerSession");

            migrationBuilder.AlterColumn<int>(
                name: "PartnerCredentialId",
                table: "OcpiPartnerSession",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_OcpiPartnerSession_PartnerCredential",
                table: "OcpiPartnerSession",
                column: "PartnerCredentialId",
                principalTable: "OcpiPartnerCredential",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OcpiPartnerSession_PartnerCredential",
                table: "OcpiPartnerSession");

            migrationBuilder.AlterColumn<int>(
                name: "PartnerCredentialId",
                table: "OcpiPartnerSession",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OcpiPartnerSession_PartnerCredential",
                table: "OcpiPartnerSession",
                column: "PartnerCredentialId",
                principalTable: "OcpiPartnerCredential",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
