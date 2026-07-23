using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfferwallApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRedemptionCountToPayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedPayoutDate",
                table: "Payouts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RedemptionCount",
                table: "Payouts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedPayoutDate",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "RedemptionCount",
                table: "Payouts");
        }
    }
}
