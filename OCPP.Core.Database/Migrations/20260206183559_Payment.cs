using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class Payment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreditBalance",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentValidations",
                columns: table => new
                {
                    RecId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentSignature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentHistoryId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WalletTransactionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VerificationMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SecurityHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VerificationAttempts = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AdditionalInfo1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdditionalInfo3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Active = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentValidations", x => x.RecId);
                    table.ForeignKey(
                        name: "FK_PaymentValidation_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "RecId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentValidations_OrderId",
                table: "PaymentValidations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentValidations_PaymentId",
                table: "PaymentValidations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentValidations_Status",
                table: "PaymentValidations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentValidations_UserId",
                table: "PaymentValidations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentValidations");

            migrationBuilder.DropColumn(
                name: "CreditBalance",
                table: "Users");
        }
    }
}
