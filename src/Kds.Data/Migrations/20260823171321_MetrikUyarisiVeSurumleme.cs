using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kds.Data.Migrations
{
    /// <inheritdoc />
    public partial class MetrikUyarisiVeSurumleme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RaporId",
                table: "Uyarilar",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "MetrikId",
                table: "Uyarilar",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MetrikSurumleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetrikId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurumNo = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Ifade = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Bicim = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DegistirenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturmaTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GuncellemeTarihi = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetrikSurumleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetrikSurumleri_Metrikler_MetrikId",
                        column: x => x.MetrikId,
                        principalTable: "Metrikler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetrikSurumleri_MetrikId_SurumNo",
                table: "MetrikSurumleri",
                columns: new[] { "MetrikId", "SurumNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetrikSurumleri");

            migrationBuilder.DropColumn(
                name: "MetrikId",
                table: "Uyarilar");

            migrationBuilder.AlterColumn<Guid>(
                name: "RaporId",
                table: "Uyarilar",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
