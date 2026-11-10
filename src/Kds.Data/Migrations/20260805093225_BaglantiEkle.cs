using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class BaglantiEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Baglantilar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sunucu = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Veritabani = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    KullaniciAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SifreSifreli = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SonTestBasarili = table.Column<bool>(type: "bit", nullable: true),
                    SonTestMs = table.Column<int>(type: "int", nullable: true),
                    SonTestTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Baglantilar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Baglantilar_TenantId_Ad",
                table: "Baglantilar",
                columns: new[] { "TenantId", "Ad" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Baglantilar");
        }
    }
}
