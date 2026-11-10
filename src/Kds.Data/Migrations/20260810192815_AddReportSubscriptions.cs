using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaporAbonelikleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RaporId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Bicim = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AlicilarJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Zamanlama = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    SahipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SonGonderimTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SonAliciSayisi = table.Column<int>(type: "int", nullable: true),
                    SonSatirSayisi = table.Column<long>(type: "bigint", nullable: true),
                    SonDurum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SonHata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaporAbonelikleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaporAbonelikleri_TenantId_Ad",
                table: "RaporAbonelikleri",
                columns: new[] { "TenantId", "Ad" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaporAbonelikleri");
        }
    }
}
