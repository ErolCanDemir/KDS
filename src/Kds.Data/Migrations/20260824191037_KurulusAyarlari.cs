using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class KurulusAyarlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoDataUri",
                table: "Tenantlar",
                type: "nvarchar(max)",
                maxLength: 400000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarkaRengi",
                table: "Tenantlar",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Tenantlar",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tenantlar",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "LogoDataUri", "MarkaRengi", "Plan" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Tenantlar",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "LogoDataUri", "MarkaRengi", "Plan" },
                values: new object[] { null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoDataUri",
                table: "Tenantlar");

            migrationBuilder.DropColumn(
                name: "MarkaRengi",
                table: "Tenantlar");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Tenantlar");
        }
    }
}
