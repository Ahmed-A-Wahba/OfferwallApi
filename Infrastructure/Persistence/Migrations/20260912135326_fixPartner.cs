using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfferwallApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class fixPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdminVerificationCodes_AdminId_Type",
                table: "AdminVerificationCodes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Partners");

            migrationBuilder.AlterColumn<decimal>(
                name: "RevenueShareRatio",
                table: "Partners",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Partners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerId",
                table: "Conversions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Conversions_PartnerId",
                table: "Conversions",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminVerificationCodes_AdminId_Type_Code",
                table: "AdminVerificationCodes",
                columns: new[] { "AdminId", "Type", "Code" });

            migrationBuilder.AddForeignKey(
                name: "FK_Conversions_Partners_PartnerId",
                table: "Conversions",
                column: "PartnerId",
                principalTable: "Partners",
                principalColumn: "PartnerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversions_Partners_PartnerId",
                table: "Conversions");

            migrationBuilder.DropIndex(
                name: "IX_Conversions_PartnerId",
                table: "Conversions");

            migrationBuilder.DropIndex(
                name: "IX_AdminVerificationCodes_AdminId_Type_Code",
                table: "AdminVerificationCodes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "Conversions");

            migrationBuilder.AlterColumn<decimal>(
                name: "RevenueShareRatio",
                table: "Partners",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Partners",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminVerificationCodes_AdminId_Type",
                table: "AdminVerificationCodes",
                columns: new[] { "AdminId", "Type" });
        }
    }
}
