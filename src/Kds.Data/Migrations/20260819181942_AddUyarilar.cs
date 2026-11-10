using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUyarilar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Uyarilar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RaporId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kolon = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Ozet = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Op = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Esik = table.Column<decimal>(type: "decimal(38,6)", precision: 38, scale: 6, nullable: false),
                    ParametrelerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AlicilarJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Zamanlama = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    SurekliBildir = table.Column<bool>(type: "bit", nullable: false),
                    SahipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tetiklendi = table.Column<bool>(type: "bit", nullable: false),
                    SonKontrolTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SonDeger = table.Column<decimal>(type: "decimal(38,6)", precision: 38, scale: 6, nullable: true),
                    SonBildirimTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SonDurum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SonHata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uyarilar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Uyarilar_TenantId_Ad",
                table: "Uyarilar",
                columns: new[] { "TenantId", "Ad" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Uyarilar");
        }
    }
}
