using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class Invoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionInvoice",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChargingSessionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BilledToName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BilledToPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BilledToEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChargingHubName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChargePointId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChargerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConnectorId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PowerOutput = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnergyConsumedKwh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SacCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PricePerUnit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxableValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cashback = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CgstRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SgstRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Active = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionInvoice", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_SessionInvoice_ChargingSession",
                        column: x => x.ChargingSessionId,
                        principalTable: "ChargingSessions",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionInvoice_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionInvoice_ChargingSessionId",
                table: "SessionInvoice",
                column: "ChargingSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionInvoice_InvoiceNumber",
                table: "SessionInvoice",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionInvoice_UserId",
                table: "SessionInvoice",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionInvoice");
        }
    }
}
