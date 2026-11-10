using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class VeriHaritasiEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VeriHaritalari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaglantiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeriHaritalari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HaritaIliskiler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeriHaritasiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KaynakTabloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KaynakKolon = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    HedefTabloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HedefKolon = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FktenMi = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaritaIliskiler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HaritaIliskiler_VeriHaritalari_VeriHaritasiId",
                        column: x => x.VeriHaritasiId,
                        principalTable: "VeriHaritalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HaritaTablolar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeriHaritasiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sema = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HamAd = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DostcaAd = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaritaTablolar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HaritaTablolar_VeriHaritalari_VeriHaritasiId",
                        column: x => x.VeriHaritasiId,
                        principalTable: "VeriHaritalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HaritaKolonlar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HaritaTabloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HamAd = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DostcaAd = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VeriTipi = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaritaKolonlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HaritaKolonlar_HaritaTablolar_HaritaTabloId",
                        column: x => x.HaritaTabloId,
                        principalTable: "HaritaTablolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HaritaIliskiler_VeriHaritasiId",
                table: "HaritaIliskiler",
                column: "VeriHaritasiId");

            migrationBuilder.CreateIndex(
                name: "IX_HaritaKolonlar_HaritaTabloId",
                table: "HaritaKolonlar",
                column: "HaritaTabloId");

            migrationBuilder.CreateIndex(
                name: "IX_HaritaTablolar_VeriHaritasiId",
                table: "HaritaTablolar",
                column: "VeriHaritasiId");

            migrationBuilder.CreateIndex(
                name: "IX_VeriHaritalari_TenantId_Ad",
                table: "VeriHaritalari",
                columns: new[] { "TenantId", "Ad" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HaritaIliskiler");

            migrationBuilder.DropTable(
                name: "HaritaKolonlar");

            migrationBuilder.DropTable(
                name: "HaritaTablolar");

            migrationBuilder.DropTable(
                name: "VeriHaritalari");
        }
    }
}
