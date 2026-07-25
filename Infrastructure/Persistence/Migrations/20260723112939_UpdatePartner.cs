using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfferwallApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactFullName",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhoneNumber",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisteredAddress",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxRegistrationNumber",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxResidencyCountry",
                table: "Partners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaxResidencyCountryCode",
                table: "Partners",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradingName",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "ContactFullName",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "ContactPhoneNumber",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "RegisteredAddress",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "TaxRegistrationNumber",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "TaxResidencyCountry",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "TaxResidencyCountryCode",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "TradingName",
                table: "Partners");
        }
    }
}
