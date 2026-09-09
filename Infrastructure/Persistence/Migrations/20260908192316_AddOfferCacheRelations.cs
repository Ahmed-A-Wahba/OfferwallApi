using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfferwallApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferCacheRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminRefreshToken_Admin_AdminId",
                table: "AdminRefreshToken");

            migrationBuilder.DropForeignKey(
                name: "FK_AdminVerificationCode_Admin_AdminId",
                table: "AdminVerificationCode");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminVerificationCode",
                table: "AdminVerificationCode");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminRefreshToken",
                table: "AdminRefreshToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Admin",
                table: "Admin");

            migrationBuilder.RenameTable(
                name: "AdminVerificationCode",
                newName: "AdminVerificationCodes");

            migrationBuilder.RenameTable(
                name: "AdminRefreshToken",
                newName: "AdminRefreshTokens");

            migrationBuilder.RenameTable(
                name: "Admin",
                newName: "Admins");

            migrationBuilder.RenameIndex(
                name: "IX_AdminVerificationCode_AdminId_Type",
                table: "AdminVerificationCodes",
                newName: "IX_AdminVerificationCodes_AdminId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_AdminVerificationCode_AdminId",
                table: "AdminVerificationCodes",
                newName: "IX_AdminVerificationCodes_AdminId");

            migrationBuilder.RenameIndex(
                name: "IX_AdminRefreshToken_Token",
                table: "AdminRefreshTokens",
                newName: "IX_AdminRefreshTokens_Token");

            migrationBuilder.RenameIndex(
                name: "IX_AdminRefreshToken_AdminId",
                table: "AdminRefreshTokens",
                newName: "IX_AdminRefreshTokens_AdminId");

            migrationBuilder.RenameIndex(
                name: "IX_Admin_Email",
                table: "Admins",
                newName: "IX_Admins_Email");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminVerificationCodes",
                table: "AdminVerificationCodes",
                column: "VerificationCodeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminRefreshTokens",
                table: "AdminRefreshTokens",
                column: "RefreshTokenId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Admins",
                table: "Admins",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversions_OfferId",
                table: "Conversions",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_Clicks_OfferId",
                table: "Clicks",
                column: "OfferId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdminRefreshTokens_Admins_AdminId",
                table: "AdminRefreshTokens",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "AdminId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminVerificationCodes_Admins_AdminId",
                table: "AdminVerificationCodes",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "AdminId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Clicks_OfferCaches_OfferId",
                table: "Clicks",
                column: "OfferId",
                principalTable: "OfferCaches",
                principalColumn: "OfferId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversions_OfferCaches_OfferId",
                table: "Conversions",
                column: "OfferId",
                principalTable: "OfferCaches",
                principalColumn: "OfferId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminRefreshTokens_Admins_AdminId",
                table: "AdminRefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_AdminVerificationCodes_Admins_AdminId",
                table: "AdminVerificationCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Clicks_OfferCaches_OfferId",
                table: "Clicks");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversions_OfferCaches_OfferId",
                table: "Conversions");

            migrationBuilder.DropIndex(
                name: "IX_Conversions_OfferId",
                table: "Conversions");

            migrationBuilder.DropIndex(
                name: "IX_Clicks_OfferId",
                table: "Clicks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminVerificationCodes",
                table: "AdminVerificationCodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Admins",
                table: "Admins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminRefreshTokens",
                table: "AdminRefreshTokens");

            migrationBuilder.RenameTable(
                name: "AdminVerificationCodes",
                newName: "AdminVerificationCode");

            migrationBuilder.RenameTable(
                name: "Admins",
                newName: "Admin");

            migrationBuilder.RenameTable(
                name: "AdminRefreshTokens",
                newName: "AdminRefreshToken");

            migrationBuilder.RenameIndex(
                name: "IX_AdminVerificationCodes_AdminId_Type",
                table: "AdminVerificationCode",
                newName: "IX_AdminVerificationCode_AdminId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_AdminVerificationCodes_AdminId",
                table: "AdminVerificationCode",
                newName: "IX_AdminVerificationCode_AdminId");

            migrationBuilder.RenameIndex(
                name: "IX_Admins_Email",
                table: "Admin",
                newName: "IX_Admin_Email");

            migrationBuilder.RenameIndex(
                name: "IX_AdminRefreshTokens_Token",
                table: "AdminRefreshToken",
                newName: "IX_AdminRefreshToken_Token");

            migrationBuilder.RenameIndex(
                name: "IX_AdminRefreshTokens_AdminId",
                table: "AdminRefreshToken",
                newName: "IX_AdminRefreshToken_AdminId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminVerificationCode",
                table: "AdminVerificationCode",
                column: "VerificationCodeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Admin",
                table: "Admin",
                column: "AdminId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminRefreshToken",
                table: "AdminRefreshToken",
                column: "RefreshTokenId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdminRefreshToken_Admin_AdminId",
                table: "AdminRefreshToken",
                column: "AdminId",
                principalTable: "Admin",
                principalColumn: "AdminId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminVerificationCode_Admin_AdminId",
                table: "AdminVerificationCode",
                column: "AdminId",
                principalTable: "Admin",
                principalColumn: "AdminId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
