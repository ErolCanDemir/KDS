using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class RaporEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaporKlasorleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaporKlasorleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Raporlar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    KlasorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HaritaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gorsellestirme = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Paylasim = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SahipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Raporlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RaporFavorileri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaporId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaporFavorileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaporFavorileri_Raporlar_RaporId",
                        column: x => x.RaporId,
                        principalTable: "Raporlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaporFavorileri_RaporId_KullaniciId",
                table: "RaporFavorileri",
                columns: new[] { "RaporId", "KullaniciId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaporKlasorleri_TenantId_Ad",
                table: "RaporKlasorleri",
                columns: new[] { "TenantId", "Ad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Raporlar_TenantId_Ad",
                table: "Raporlar",
                columns: new[] { "TenantId", "Ad" });

            migrationBuilder.CreateIndex(
                name: "IX_Raporlar_TenantId_KlasorId",
                table: "Raporlar",
                columns: new[] { "TenantId", "KlasorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaporFavorileri");

            migrationBuilder.DropTable(
                name: "RaporKlasorleri");

            migrationBuilder.DropTable(
                name: "Raporlar");
        }
    }
}
