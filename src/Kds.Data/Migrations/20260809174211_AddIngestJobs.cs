using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IceAlimIsleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaglantiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TablolarJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Zamanlama = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    SonSenkronTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SonSenkronSaniye = table.Column<int>(type: "int", nullable: true),
                    SonSatirSayisi = table.Column<long>(type: "bigint", nullable: true),
                    SonDurum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SonHata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IceAlimIsleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IceAlimIsleri_TenantId_Ad",
                table: "IceAlimIsleri",
                columns: new[] { "TenantId", "Ad" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IceAlimIsleri");
        }
    }
}
