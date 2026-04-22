using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class OCPIRoaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OcpiPartnerCredential",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PartyId = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiPartnerCredential", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OcpiTariff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PartyId = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TariffId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ElementsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnergyPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TimePrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    SessionFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MinKwh = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxKwh = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiTariff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OcpiCdr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PartyId = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CdrId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    AuthorizationReference = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    AuthMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LocationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    EvseUid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    ConnectorId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    MeterId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalEnergy = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalTime = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalParkingTime = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalCostExclVat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCostInclVat = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TokenUid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    PartnerCredentialId = table.Column<int>(type: "int", nullable: true),
                    LocalSessionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiCdr", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcpiCdr_PartnerCredential",
                        column: x => x.PartnerCredentialId,
                        principalTable: "OcpiPartnerCredential",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OcpiPartnerLocation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PartyId = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LocationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Latitude = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Longitude = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PartnerCredentialId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiPartnerLocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcpiPartnerLocation_PartnerCredential",
                        column: x => x.PartnerCredentialId,
                        principalTable: "OcpiPartnerCredential",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcpiPartnerSession",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PartyId = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalEnergy = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LocationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    EvseUid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    ConnectorId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    AuthorizationReference = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    TokenUid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PartnerCredentialId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiPartnerSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcpiPartnerSession_PartnerCredential",
                        column: x => x.PartnerCredentialId,
                        principalTable: "OcpiPartnerCredential",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcpiToken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PartyId = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TokenUid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VisualNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Issuer = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    GroupId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Valid = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Whitelist = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DefaultProfileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EnergyContract = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PartnerCredentialId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcpiToken_PartnerCredential",
                        column: x => x.PartnerCredentialId,
                        principalTable: "OcpiPartnerCredential",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcpiPartnerEvse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvseUid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    EvseId = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StatusDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FloorLevel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PhysicalReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PartnerLocationId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiPartnerEvse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcpiPartnerEvse_PartnerLocation",
                        column: x => x.PartnerLocationId,
                        principalTable: "OcpiPartnerLocation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OcpiPartnerConnector",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConnectorId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PowerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaxVoltage = table.Column<int>(type: "int", nullable: true),
                    MaxAmperage = table.Column<int>(type: "int", nullable: true),
                    MaxElectricPower = table.Column<int>(type: "int", nullable: true),
                    PartnerEvseId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiPartnerConnector", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcpiPartnerConnector_PartnerEvse",
                        column: x => x.PartnerEvseId,
                        principalTable: "OcpiPartnerEvse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcpiCdr_CountryCode_PartyId_CdrId",
                table: "OcpiCdr",
                columns: new[] { "CountryCode", "PartyId", "CdrId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiCdr_LocalSessionId",
                table: "OcpiCdr",
                column: "LocalSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiCdr_PartnerCredentialId",
                table: "OcpiCdr",
                column: "PartnerCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerConnector_EvseId_ConnectorId",
                table: "OcpiPartnerConnector",
                columns: new[] { "PartnerEvseId", "ConnectorId" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerEvse_EvseUid",
                table: "OcpiPartnerEvse",
                column: "EvseUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerEvse_PartnerLocationId",
                table: "OcpiPartnerEvse",
                column: "PartnerLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerLocation_CountryCode_PartyId_LocationId",
                table: "OcpiPartnerLocation",
                columns: new[] { "CountryCode", "PartyId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerLocation_PartnerCredentialId",
                table: "OcpiPartnerLocation",
                column: "PartnerCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerSession_CountryCode_PartyId_SessionId",
                table: "OcpiPartnerSession",
                columns: new[] { "CountryCode", "PartyId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiPartnerSession_PartnerCredentialId",
                table: "OcpiPartnerSession",
                column: "PartnerCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiTariff_CountryCode_PartyId_TariffId",
                table: "OcpiTariff",
                columns: new[] { "CountryCode", "PartyId", "TariffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiToken_CountryCode_PartyId_TokenUid",
                table: "OcpiToken",
                columns: new[] { "CountryCode", "PartyId", "TokenUid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiToken_PartnerCredentialId",
                table: "OcpiToken",
                column: "PartnerCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiToken_TokenUid",
                table: "OcpiToken",
                column: "TokenUid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcpiCdr");

            migrationBuilder.DropTable(
                name: "OcpiPartnerConnector");

            migrationBuilder.DropTable(
                name: "OcpiPartnerSession");

            migrationBuilder.DropTable(
                name: "OcpiTariff");

            migrationBuilder.DropTable(
                name: "OcpiToken");

            migrationBuilder.DropTable(
                name: "OcpiPartnerEvse");

            migrationBuilder.DropTable(
                name: "OcpiPartnerLocation");

            migrationBuilder.DropTable(
                name: "OcpiPartnerCredential");
        }
    }
}
