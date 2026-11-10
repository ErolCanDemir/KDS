using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class DenetimKaydiEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DenetimKayitlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaglantiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Sql = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Basarili = table.Column<bool>(type: "bit", nullable: false),
                    SatirSayisi = table.Column<int>(type: "int", nullable: true),
                    SureMs = table.Column<int>(type: "int", nullable: false),
                    Hata = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DenetimKayitlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DenetimKayitlari_TenantId_OlusturmaTarihi",
                table: "DenetimKayitlari",
                columns: new[] { "TenantId", "OlusturmaTarihi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DenetimKayitlari");
        }
    }
}
