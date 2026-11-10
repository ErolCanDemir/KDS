using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class KdsAjani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AjanTetikTarihi",
                table: "IceAlimIsleri",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AjanId",
                table: "Baglantilar",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Ajanlar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IptalTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SonGorulme = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OlusturanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ajanlar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ajanlar_TenantId_Ad",
                table: "Ajanlar",
                columns: new[] { "TenantId", "Ad" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ajanlar");

            migrationBuilder.DropColumn(
                name: "AjanTetikTarihi",
                table: "IceAlimIsleri");

            migrationBuilder.DropColumn(
                name: "AjanId",
                table: "Baglantilar");
        }
    }
}
