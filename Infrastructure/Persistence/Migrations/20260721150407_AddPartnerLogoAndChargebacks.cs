using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfferwallApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerLogoAndChargebacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartnerRefreshToken_Partners_PartnerId",
                table: "PartnerRefreshToken");

            migrationBuilder.DropForeignKey(
                name: "FK_Payout_Partners_PartnerId",
                table: "Payout");

            migrationBuilder.DropForeignKey(
                name: "FK_Wallet_User_UserId",
                table: "Wallet");

            migrationBuilder.DropTable(
                name: "Click");

            migrationBuilder.DropTable(
                name: "OfferCache");

            migrationBuilder.DropTable(
                name: "SupportTicket");

            migrationBuilder.DropTable(
                name: "WalletTransaction");

            migrationBuilder.DropTable(
                name: "Conversion");

            migrationBuilder.DropTable(
                name: "Coupon");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Payout",
                table: "Payout");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PartnerRefreshToken",
                table: "PartnerRefreshToken");

            migrationBuilder.RenameTable(
                name: "Payout",
                newName: "Payouts");

            migrationBuilder.RenameTable(
                name: "PartnerRefreshToken",
                newName: "PartnerRefreshTokens");

            migrationBuilder.RenameIndex(
                name: "IX_Payout_PartnerId",
                table: "Payouts",
                newName: "IX_Payouts_PartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_PartnerRefreshToken_Token",
                table: "PartnerRefreshTokens",
                newName: "IX_PartnerRefreshTokens_Token");

            migrationBuilder.RenameIndex(
                name: "IX_PartnerRefreshToken_PartnerId",
                table: "PartnerRefreshTokens",
                newName: "IX_PartnerRefreshTokens_PartnerId");

            migrationBuilder.AddColumn<bool>(
                name: "HasChargebacks",
                table: "Partners",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Payouts",
                table: "Payouts",
                column: "PayoutId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PartnerRefreshTokens",
                table: "PartnerRefreshTokens",
                column: "RefreshTokenId");

            migrationBuilder.CreateTable(
                name: "OfferCaches",
                columns: table => new
                {
                    OfferId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TotalReward = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTasks = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferCaches", x => x.OfferId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Xp = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "PartnerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Clicks",
                columns: table => new
                {
                    ClickId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clicks", x => x.ClickId);
                    table.ForeignKey(
                        name: "FK_Clicks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversions",
                columns: table => new
                {
                    ConversionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payout = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ReversedConversionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversions", x => x.ConversionId);
                    table.ForeignKey(
                        name: "FK_Conversions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CityCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.CouponId);
                    table.ForeignKey(
                        name: "FK_Coupons_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "PartnerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Coupons_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ScreenshotUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ConversionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Conversions_ConversionId",
                        column: x => x.ConversionId,
                        principalTable: "Conversions",
                        principalColumn: "ConversionId");
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupons",
                        principalColumn: "CouponId");
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clicks_UserId",
                table: "Clicks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversions_UserId",
                table: "Conversions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_PartnerId",
                table: "Coupons",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_UserId",
                table: "Coupons",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId",
                table: "SupportTickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PartnerId_ExternalUserId",
                table: "Users",
                columns: new[] { "PartnerId", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ConversionId",
                table: "WalletTransactions",
                column: "ConversionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_CouponId",
                table: "WalletTransactions",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserId",
                table: "WalletTransactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerRefreshTokens_Partners_PartnerId",
                table: "PartnerRefreshTokens",
                column: "PartnerId",
                principalTable: "Partners",
                principalColumn: "PartnerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payouts_Partners_PartnerId",
                table: "Payouts",
                column: "PartnerId",
                principalTable: "Partners",
                principalColumn: "PartnerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Wallet_Users_UserId",
                table: "Wallet",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartnerRefreshTokens_Partners_PartnerId",
                table: "PartnerRefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Payouts_Partners_PartnerId",
                table: "Payouts");

            migrationBuilder.DropForeignKey(
                name: "FK_Wallet_Users_UserId",
                table: "Wallet");

            migrationBuilder.DropTable(
                name: "Clicks");

            migrationBuilder.DropTable(
                name: "OfferCaches");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "Conversions");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Payouts",
                table: "Payouts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PartnerRefreshTokens",
                table: "PartnerRefreshTokens");

            migrationBuilder.DropColumn(
                name: "HasChargebacks",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Partners");

            migrationBuilder.RenameTable(
                name: "Payouts",
                newName: "Payout");

            migrationBuilder.RenameTable(
                name: "PartnerRefreshTokens",
                newName: "PartnerRefreshToken");

            migrationBuilder.RenameIndex(
                name: "IX_Payouts_PartnerId",
                table: "Payout",
                newName: "IX_Payout_PartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_PartnerRefreshTokens_Token",
                table: "PartnerRefreshToken",
                newName: "IX_PartnerRefreshToken_Token");

            migrationBuilder.RenameIndex(
                name: "IX_PartnerRefreshTokens_PartnerId",
                table: "PartnerRefreshToken",
                newName: "IX_PartnerRefreshToken_PartnerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Payout",
                table: "Payout",
                column: "PayoutId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PartnerRefreshToken",
                table: "PartnerRefreshToken",
                column: "RefreshTokenId");

            migrationBuilder.CreateTable(
                name: "OfferCache",
                columns: table => new
                {
                    OfferId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Logo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TotalReward = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTasks = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferCache", x => x.OfferId);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Xp = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_User_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "PartnerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Click",
                columns: table => new
                {
                    ClickId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OfferId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Click", x => x.ClickId);
                    table.ForeignKey(
                        name: "FK_Click_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversion",
                columns: table => new
                {
                    ConversionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OfferId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payout = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReversedConversionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversion", x => x.ConversionId);
                    table.ForeignKey(
                        name: "FK_Conversion_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Coupon",
                columns: table => new
                {
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CityCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupon", x => x.CouponId);
                    table.ForeignKey(
                        name: "FK_Coupon_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "PartnerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Coupon_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicket",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ScreenshotUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicket", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_SupportTicket_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransaction",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransaction", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_WalletTransaction_Conversion_ConversionId",
                        column: x => x.ConversionId,
                        principalTable: "Conversion",
                        principalColumn: "ConversionId");
                    table.ForeignKey(
                        name: "FK_WalletTransaction_Coupon_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupon",
                        principalColumn: "CouponId");
                    table.ForeignKey(
                        name: "FK_WalletTransaction_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Click_UserId",
                table: "Click",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversion_UserId",
                table: "Conversion",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Coupon_Code",
                table: "Coupon",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupon_PartnerId",
                table: "Coupon",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Coupon_UserId",
                table: "Coupon",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicket_UserId",
                table: "SupportTicket",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_User_PartnerId_ExternalUserId",
                table: "User",
                columns: new[] { "PartnerId", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransaction_ConversionId",
                table: "WalletTransaction",
                column: "ConversionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransaction_CouponId",
                table: "WalletTransaction",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransaction_UserId",
                table: "WalletTransaction",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerRefreshToken_Partners_PartnerId",
                table: "PartnerRefreshToken",
                column: "PartnerId",
                principalTable: "Partners",
                principalColumn: "PartnerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payout_Partners_PartnerId",
                table: "Payout",
                column: "PartnerId",
                principalTable: "Partners",
                principalColumn: "PartnerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Wallet_User_UserId",
                table: "Wallet",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
