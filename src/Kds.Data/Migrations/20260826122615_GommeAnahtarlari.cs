using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class GommeAnahtarlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GommeAnahtarlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RaporId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    KimlikJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IzinliAlanlar = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GecerlilikBitis = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IptalTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OlusturanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SonKullanim = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GommeAnahtarlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GommeAnahtarlari_TenantId_IptalTarihi",
                table: "GommeAnahtarlari",
                columns: new[] { "TenantId", "IptalTarihi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GommeAnahtarlari");
        }
    }
}
