using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfferwallApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiSecretToPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiSecret",
                table: "Partners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiSecret",
                table: "Partners");
        }
    }
}
