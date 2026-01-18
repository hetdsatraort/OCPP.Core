using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHardwareMasterTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Active",
                table: "ConnectorStatus",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "BatteryCapacityMasters",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BatteryCapcacity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BatteryCapcacityUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryCapacityMasters", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "BatteryTypeMasters",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BatteryType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Active = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryTypeMasters", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "CarManufacturerMasters",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManufacturerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ManufacturerLogoImage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarManufacturerMasters", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "ChargerTypeMasters",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChargerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChargerTypeImage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Additional_Info_1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargerTypeMasters", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "ChargingHubs",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChargingHubImage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Pincode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Latitude = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Longitude = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OpeningTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ClosingTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    TypeATariff = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TypeBTariff = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Amenities = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdditionalInfo1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingHubs", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "ChargingSessions",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChargingGunId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargingStationID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StartMeterReading = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EndMeterReading = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EnergyTransmitted = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChargingSpeed = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargingTariff = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargingTotalFee = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingSessions", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "EVModelMasters",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufacturerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Variant = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatterytypeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BatteryCapacityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CarModelImage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TypeASupport = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TypeBSupport = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ChadeMOSupport = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CCSSupport = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVModelMasters", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EMailID = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProfileImageID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PinCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProfileCompleted = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LastLogin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "UserVehicles",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EVManufacturerID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CarModelID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CarModelVariant = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CarRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultConfig = table.Column<int>(type: "int", nullable: false),
                    BatteryTypeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BatteryCapacityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVehicles", x => x.RecId);
                });

            migrationBuilder.CreateTable(
                name: "ChargingStations",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChargingPointId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChargingHubId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargingGunCount = table.Column<int>(type: "int", nullable: false),
                    ChargingStationImage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingStations", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_ChargingStation_ChargePoint",
                        column: x => x.ChargingPointId,
                        principalTable: "ChargePoint",
                        principalColumn: "ChargePointId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargingStation_ChargingHub",
                        column: x => x.ChargingHubId,
                        principalTable: "ChargingHubs",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FileMasters",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FileURL = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMasters", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_FileMaster_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReplacedByToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_RefreshToken_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactionLogs",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PreviousCreditBalance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrentCreditBalance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentRecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargingSessionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdditionalInfo1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactionLogs", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_WalletTransactionLog_ChargingSession",
                        column: x => x.ChargingSessionId,
                        principalTable: "ChargingSessions",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactionLog_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChargingHubReviews",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChargingHubId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargingStationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewImage1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReviewImage2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReviewImage3 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReviewImage4 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingHubReviews", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_ChargingHubReview_ChargingHub",
                        column: x => x.ChargingHubId,
                        principalTable: "ChargingHubs",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargingHubReview_ChargingStation",
                        column: x => x.ChargingStationId,
                        principalTable: "ChargingStations",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentHistories",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChargingStationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SessionDuration = table.Column<TimeSpan>(type: "time", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdditionalInfo1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentHistories", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_PaymentHistory_ChargingStation",
                        column: x => x.ChargingStationId,
                        principalTable: "ChargingStations",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentHistory_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryCapacityMasters_BatteryCapcacity",
                table: "BatteryCapacityMasters",
                column: "BatteryCapcacity");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryTypeMasters_BatteryType",
                table: "BatteryTypeMasters",
                column: "BatteryType");

            migrationBuilder.CreateIndex(
                name: "IX_CarManufacturerMasters_ManufacturerName",
                table: "CarManufacturerMasters",
                column: "ManufacturerName");

            migrationBuilder.CreateIndex(
                name: "IX_ChargerTypeMasters_ChargerType",
                table: "ChargerTypeMasters",
                column: "ChargerType");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingHubReviews_ChargingHubId",
                table: "ChargingHubReviews",
                column: "ChargingHubId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingHubReviews_ChargingStationId",
                table: "ChargingHubReviews",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingStations_ChargingHubId",
                table: "ChargingStations",
                column: "ChargingHubId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingStations_ChargingPointId",
                table: "ChargingStations",
                column: "ChargingPointId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMasters_UserId",
                table: "FileMasters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_ChargingStationId",
                table: "PaymentHistories",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHistories_UserId",
                table: "PaymentHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactionLogs_ChargingSessionId",
                table: "WalletTransactionLogs",
                column: "ChargingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactionLogs_UserId",
                table: "WalletTransactionLogs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectorStatus_ChargePoint_ChargePointId",
                table: "ConnectorStatus",
                column: "ChargePointId",
                principalTable: "ChargePoint",
                principalColumn: "ChargePointId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConnectorStatus_ChargePoint_ChargePointId",
                table: "ConnectorStatus");

            migrationBuilder.DropTable(
                name: "BatteryCapacityMasters");

            migrationBuilder.DropTable(
                name: "BatteryTypeMasters");

            migrationBuilder.DropTable(
                name: "CarManufacturerMasters");

            migrationBuilder.DropTable(
                name: "ChargerTypeMasters");

            migrationBuilder.DropTable(
                name: "ChargingHubReviews");

            migrationBuilder.DropTable(
                name: "EVModelMasters");

            migrationBuilder.DropTable(
                name: "FileMasters");

            migrationBuilder.DropTable(
                name: "PaymentHistories");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "UserVehicles");

            migrationBuilder.DropTable(
                name: "WalletTransactionLogs");

            migrationBuilder.DropTable(
                name: "ChargingStations");

            migrationBuilder.DropTable(
                name: "ChargingSessions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ChargingHubs");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "ConnectorStatus");
        }
    }
}
