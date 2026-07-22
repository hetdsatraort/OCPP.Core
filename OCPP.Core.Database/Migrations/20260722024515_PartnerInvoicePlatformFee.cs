using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class PartnerInvoicePlatformFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OcpiPartnerPlatformFee",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerCredentialId = table.Column<int>(type: "int", nullable: false),
                    FeePerKwh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiPartnerPlatformFee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcpiPartnerPlatformFee_PartnerCredential",
                        column: x => x.PartnerCredentialId,
                        principalTable: "OcpiPartnerCredential",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcpiPartnerSessionInvoice",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OcpiPartnerSessionId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PartnerCredentialId = table.Column<int>(type: "int", nullable: true),
                    PartnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BilledToName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BilledToPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BilledToEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnergyConsumedKwh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    PartnerCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SacCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PricePerUnit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxableValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CgstRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SgstRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Active = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiPartnerSessionInvoice", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_OcpiPartnerSessionInvoice_OcpiPartnerSession",
                        column: x => x.OcpiPartnerSessionId,
                        principalTable: "OcpiPartnerSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OcpiPartnerSessionInvoice_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerPlatformFee_PartnerCredentialId",
                table: "OcpiPartnerPlatformFee",
                column: "PartnerCredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerSessionInvoice_InvoiceNumber",
                table: "OcpiPartnerSessionInvoice",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerSessionInvoice_OcpiPartnerSessionId",
                table: "OcpiPartnerSessionInvoice",
                column: "OcpiPartnerSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerSessionInvoice_UserId",
                table: "OcpiPartnerSessionInvoice",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcpiPartnerPlatformFee");

            migrationBuilder.DropTable(
                name: "OcpiPartnerSessionInvoice");
        }
    }
}
