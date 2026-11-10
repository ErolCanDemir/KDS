using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParametrelerJson",
                table: "RaporAbonelikleri",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParametrelerJson",
                table: "RaporAbonelikleri");
        }
    }
}
