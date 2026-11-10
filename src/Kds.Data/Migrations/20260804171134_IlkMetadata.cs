using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class IlkMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenantlar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenantlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeycloakId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KullaniciAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EPosta = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    AdSoyad = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SonGirisTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kullanicilar_Tenantlar_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenantlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Tenantlar",
                columns: new[] { "Id", "Ad", "Durum", "GuncellemeTarihi", "OlusturmaTarihi" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Deneme A.Ş.", 0, null, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "Örnek Ltd.", 0, null, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_KeycloakId",
                table: "Kullanicilar",
                column: "KeycloakId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_TenantId_KullaniciAdi",
                table: "Kullanicilar",
                columns: new[] { "TenantId", "KullaniciAdi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Kullanicilar");

            migrationBuilder.DropTable(
                name: "Tenantlar");
        }
    }
}
