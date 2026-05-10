using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class OCPIHostedSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "OcpiPartnerSession");

            migrationBuilder.CreateTable(
                name: "OcpiHostedSession",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TransactionId = table.Column<int>(type: "int", nullable: true),
                    ChargePointId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConnectorNumber = table.Column<int>(type: "int", nullable: false),
                    EvseUid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    ConnectorId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    TokenUid = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    LocationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    PartnerCredentialId = table.Column<int>(type: "int", nullable: true),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalEnergy = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcpiHostedSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcpiHostedSession_PartnerCredential",
                        column: x => x.PartnerCredentialId,
                        principalTable: "OcpiPartnerCredential",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcpiHostedSession_PartnerCredentialId",
                table: "OcpiHostedSession",
                column: "PartnerCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiHostedSession_SessionId",
                table: "OcpiHostedSession",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OcpiHostedSession_Status",
                table: "OcpiHostedSession",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OcpiHostedSession_TransactionId",
                table: "OcpiHostedSession",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcpiHostedSession");

            migrationBuilder.AddColumn<int>(
                name: "TransactionId",
                table: "OcpiPartnerSession",
                type: "int",
                nullable: true);
        }
    }
}
