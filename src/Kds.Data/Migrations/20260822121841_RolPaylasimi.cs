using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class RolPaylasimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaylasimRolleri",
                table: "Raporlar",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErisimRolleri",
                table: "RaporKlasorleri",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaylasimRolleri",
                table: "Panolar",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaylasimRolleri",
                table: "Raporlar");

            migrationBuilder.DropColumn(
                name: "ErisimRolleri",
                table: "RaporKlasorleri");

            migrationBuilder.DropColumn(
                name: "PaylasimRolleri",
                table: "Panolar");
        }
    }
}
