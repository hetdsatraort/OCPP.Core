using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class Guns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChargingGuns",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChargingStationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConnectorId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargingHubId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargerTypeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargerTariff = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PowerOutput = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargerStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargerMeterReading = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdditionalInfo1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingGuns", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_ChargingGuns_ChargerTypeMaster",
                        column: x => x.ChargerTypeId,
                        principalTable: "ChargerTypeMasters",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargingGuns_ChargingHub",
                        column: x => x.ChargingHubId,
                        principalTable: "ChargingHubs",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargingGuns_ChargingStation",
                        column: x => x.ChargingStationId,
                        principalTable: "ChargingStations",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingGuns_ChargerTypeId",
                table: "ChargingGuns",
                column: "ChargerTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingGuns_ChargingHubId",
                table: "ChargingGuns",
                column: "ChargingHubId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingGuns_ChargingStationId",
                table: "ChargingGuns",
                column: "ChargingStationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargingGuns");
        }
    }
}
