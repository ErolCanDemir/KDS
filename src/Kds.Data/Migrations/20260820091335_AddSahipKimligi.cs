using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSahipKimligi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SahipKimlikJson",
                table: "Uyarilar",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SahipKimlikJson",
                table: "RaporAbonelikleri",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SahipKimlikJson",
                table: "Uyarilar");

            migrationBuilder.DropColumn(
                name: "SahipKimlikJson",
                table: "RaporAbonelikleri");
        }
    }
}
