using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Kds.Contracts.Queries;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Kds.Export;

/// <summary>Sorgu sonucunu CSV/Excel/PDF'e çevirir (S13). CSV: UTF-8 BOM (Excel Türkçe'yi doğru açar).
/// Excel: ClosedXML, sayılar/tarihler yerel tiple. PDF: MigraDoc tablo (font OS'ten, cross-platform).</summary>
public sealed class ResultExporter : IResultExporter
{
    private static bool _fontAyarlandi;
    private static readonly object Kilit = new();

    public ResultExporter()
    {
        // PDFsharp font çözücüsü süreç-geneli tek sefer ayarlanır (ikinci kez set edilirse patlar).
        lock (Kilit)
        {
            if (!_fontAyarlandi)
            {
                GlobalFontSettings.FontResolver = new SistemFontCozucu();
                _fontAyarlandi = true;
            }
        }
    }

    public ExportedFile DisaAktar(QueryRunResult sonuc, ExportFormat bicim, string baslik)
    {
        var ad = DosyaAdiTemizle(baslik);
        return bicim switch
        {
            ExportFormat.Csv => new ExportedFile(CsvUret(sonuc), "text/csv; charset=utf-8", ad + ".csv"),
            ExportFormat.Xlsx => new ExportedFile(XlsxUret(sonuc, baslik),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ad + ".xlsx"),
            ExportFormat.Pdf => new ExportedFile(PdfUret(sonuc, baslik), "application/pdf", ad + ".pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(bicim)),
        };
    }

    // ---- CSV ----
    private static byte[] CsvUret(QueryRunResult s)
    {
        using var ms = new MemoryStream();
        using (var w = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true))
        {
            w.WriteLine(string.Join(",", s.Kolonlar.Select(k => CsvKacir(k.Ad))));
            foreach (var satir in s.Satirlar)
            {
                w.WriteLine(string.Join(",", satir.Select(h => CsvKacir(MetinDeger(h)))));
            }
        }
        return ms.ToArray();
    }

    private static string CsvKacir(string v) =>
        v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r')
            ? "\"" + v.Replace("\"", "\"\"") + "\""
            : v;

    // ---- Excel ----
    private static byte[] XlsxUret(QueryRunResult s, string baslik)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(SayfaAdi(baslik));

        for (var c = 0; c < s.Kolonlar.Count; c++)
        {
            var hucre = ws.Cell(1, c + 1);
            hucre.Value = s.Kolonlar[c].Ad;
            hucre.Style.Font.Bold = true;
        }

        for (var r = 0; r < s.Satirlar.Count; r++)
        {
            var satir = s.Satirlar[r];
            for (var c = 0; c < satir.Count; c++)
            {
                if (satir[c] is { } deger)
                {
                    ws.Cell(r + 2, c + 1).Value = Hucre(deger);
                }
            }
        }

        ws.SheetView.FreezeRows(1);
        // Otomatik genişlik yalnız ilk ~200 satırdan örneklenir (büyük sette maliyeti sınırlar).
        ws.Columns().AdjustToContents(1, Math.Min(s.Satirlar.Count + 1, 200));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static XLCellValue Hucre(object v) => v switch
    {
        bool b => b,
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
            Convert.ToDouble(v, CultureInfo.InvariantCulture),
        DateTime d => d,
        DateTimeOffset dto => dto.DateTime,
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        Guid g => g.ToString(),
        _ => v.ToString() ?? string.Empty,
    };

    // ---- PDF ----
    private static byte[] PdfUret(QueryRunResult s, string baslik)
    {
        var doc = new Document();
        var normal = doc.Styles["Normal"];
        if (normal is not null)
        {
            normal.Font.Name = "Arial"; // ad önemsiz — çözücü OS fontunu döndürür
        }

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.Orientation = s.Kolonlar.Count > 5 ? Orientation.Landscape : Orientation.Portrait;
        sec.PageSetup.TopMargin = sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.4);
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(1.2);

        var h = sec.AddParagraph(baslik);
        h.Format.Font.Size = 14;
        h.Format.Font.Bold = true;
        var alt = sec.AddParagraph($"{s.Satirlar.Count} satır · {DateTime.Now:yyyy-MM-dd HH:mm}");
        alt.Format.Font.Size = 8;
        alt.Format.Font.Color = Colors.Gray;
        alt.Format.SpaceAfter = Unit.FromPoint(8);

        var t = sec.AddTable();
        t.Borders.Width = 0.25;
        t.Borders.Color = Colors.Gainsboro;

        var kullanilabilirCm = (sec.PageSetup.Orientation == Orientation.Landscape ? 29.7 : 21.0) - 2.4;
        var kolonGenislik = Unit.FromCentimeter(kullanilabilirCm / Math.Max(1, s.Kolonlar.Count));
        foreach (var _ in s.Kolonlar)
        {
            t.AddColumn(kolonGenislik);
        }

        var baslikSatir = t.AddRow();
        baslikSatir.HeadingFormat = true; // her sayfada tekrarlar
        baslikSatir.Shading.Color = Colors.WhiteSmoke;
        baslikSatir.Format.Font.Bold = true;
        baslikSatir.Format.Font.Size = 8.5;
        for (var c = 0; c < s.Kolonlar.Count; c++)
        {
            baslikSatir.Cells[c].AddParagraph(s.Kolonlar[c].Ad);
        }

        foreach (var satir in s.Satirlar)
        {
            var row = t.AddRow();
            row.Format.Font.Size = 8;
            for (var c = 0; c < s.Kolonlar.Count && c < satir.Count; c++)
            {
                row.Cells[c].AddParagraph(MetinDeger(satir[c]));
            }
        }

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms);
        return ms.ToArray();
    }

    // ---- Ortak ----
    private static string MetinDeger(object? v) => v switch
    {
        null => string.Empty,
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? string.Empty,
    };

    private static string DosyaAdiTemizle(string baslik)
    {
        var temiz = new string((baslik ?? "rapor")
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ' ? ch : '_').ToArray()).Trim();
        return string.IsNullOrEmpty(temiz) ? "rapor" : temiz;
    }

    private static string SayfaAdi(string baslik)
    {
        // Excel sayfa adı ≤31 ve []:*?/\ içeremez.
        var yasak = new[] { '[', ']', ':', '*', '?', '/', '\\' };
        var temiz = new string((baslik ?? "Rapor").Where(ch => !yasak.Contains(ch)).ToArray()).Trim();
        if (string.IsNullOrEmpty(temiz))
        {
            temiz = "Rapor";
        }
        return temiz.Length > 31 ? temiz[..31] : temiz;
    }
}
