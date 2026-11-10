using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMetrikler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Metrikler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VeriHaritasiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ifade = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Bicim = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SahipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrikler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Metrikler_VeriHaritalari_VeriHaritasiId",
                        column: x => x.VeriHaritasiId,
                        principalTable: "VeriHaritalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Metrikler_TenantId_Ad",
                table: "Metrikler",
                columns: new[] { "TenantId", "Ad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metrikler_VeriHaritasiId",
                table: "Metrikler",
                column: "VeriHaritasiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Metrikler");
        }
    }
}
