using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSatirKurallari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SatirKurallari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeriHaritasiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HaritaTabloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kolon = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Op = table.Column<int>(type: "int", nullable: false),
                    DegerKaynagi = table.Column<int>(type: "int", nullable: false),
                    Deger = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Roller = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatirKurallari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SatirKurallari_HaritaTablolar_HaritaTabloId",
                        column: x => x.HaritaTabloId,
                        principalTable: "HaritaTablolar",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SatirKurallari_Tenantlar_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenantlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SatirKurallari_VeriHaritalari_VeriHaritasiId",
                        column: x => x.VeriHaritasiId,
                        principalTable: "VeriHaritalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SatirKurallari_HaritaTabloId",
                table: "SatirKurallari",
                column: "HaritaTabloId");

            migrationBuilder.CreateIndex(
                name: "IX_SatirKurallari_TenantId_VeriHaritasiId",
                table: "SatirKurallari",
                columns: new[] { "TenantId", "VeriHaritasiId" });

            migrationBuilder.CreateIndex(
                name: "IX_SatirKurallari_VeriHaritasiId",
                table: "SatirKurallari",
                column: "VeriHaritasiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SatirKurallari");
        }
    }
}
