using System.Text;
using Kds.Contracts.Queries;
using Kds.Export;
using Xunit;

namespace Kds.Tests;

/// <summary>S13 dışa aktarım: üç biçim de geçerli dosya üretir (CSV kaçış + BOM, XLSX zip imzası,
/// PDF %PDF + font çözücü). Null/virgül/tırnak/tarih uç değerleri örnekte.</summary>
public sealed class ResultExporterTests
{
    private static QueryRunResult Ornek() => new(
        [new QueryColumnDto("Ad", "nvarchar"), new QueryColumnDto("Tutar", "decimal"), new QueryColumnDto("Tarih", "datetime")],
        [
            new object?[] { "Alfa, Ltd.", 1234.5m, new DateTime(2026, 8, 9, 10, 30, 0) },
            new object?[] { "Beta \"A\"", null, null },
        ],
        SatirSiniriUygulandi: false, SureMs: 12);

    [Fact]
    public void csv_baslik_kacis_ve_bom()
    {
        var d = new ResultExporter().DisaAktar(Ornek(), ExportFormat.Csv, "Test Rapor");

        Assert.Equal("text/csv; charset=utf-8", d.IcerikTipi);
        Assert.Equal("Test Rapor.csv", d.DosyaAdi);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, d.Icerik[..3]); // UTF-8 BOM

        var metin = Encoding.UTF8.GetString(d.Icerik);
        Assert.Contains("Ad,Tutar,Tarih", metin);
        Assert.Contains("\"Alfa, Ltd.\"", metin);        // virgül → tırnakla sarılır
        Assert.Contains("\"Beta \"\"A\"\"\"", metin);    // tırnak → ikiye katlanır
    }

    [Fact]
    public void xlsx_zip_imzasi()
    {
        var d = new ResultExporter().DisaAktar(Ornek(), ExportFormat.Xlsx, "Aylık: Ciro?");

        Assert.EndsWith(".xlsx", d.DosyaAdi);
        Assert.True(d.Icerik.Length > 100);
        Assert.Equal((byte)'P', d.Icerik[0]);
        Assert.Equal((byte)'K', d.Icerik[1]); // xlsx = zip (PK)
    }

    [Fact]
    public void pdf_imzasi_ve_font_cozucu()
    {
        var d = new ResultExporter().DisaAktar(Ornek(), ExportFormat.Pdf, "Test Rapor");

        Assert.EndsWith(".pdf", d.DosyaAdi);
        Assert.True(d.Icerik.Length > 200);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(d.Icerik, 0, 5)); // PDF sihirli baytları
    }
}
