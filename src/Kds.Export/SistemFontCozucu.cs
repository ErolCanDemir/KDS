using PdfSharp.Fonts;

namespace Kds.Export;

/// <summary>PDFsharp için işletim sisteminden bir TrueType font BULUP döndürür (GDI'siz, cross-platform).
/// Windows'ta arial/segoeui, Linux'ta (Docker) DejaVu/Liberation aranır — bulunan font tüm typeface
/// istekleri için kullanılır (Türkçe glif kapsamı hepsinde var). Font dosyaları GÖMÜLMEZ (lisans);
/// çalışma anında OS'ten okunur. Font arama = IO adaptörü (AGENTS §4.6 izinli try/catch bölgesi).</summary>
internal sealed class SistemFontCozucu : IFontResolver
{
    private static readonly string[] AdayDosyalar =
    {
        "arial.ttf", "Arial.ttf", "segoeui.ttf", "SegoeUI.ttf",
        "DejaVuSans.ttf", "LiberationSans-Regular.ttf", "NotoSans-Regular.ttf", "verdana.ttf",
    };

    private readonly byte[] _font;

    public SistemFontCozucu() => _font = FontBul();

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new("govde");

    public byte[] GetFont(string faceName) => _font;

    private static byte[] FontBul()
    {
        var dizinler = new List<string>();
        var winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrEmpty(winFonts))
        {
            dizinler.Add(winFonts);
        }
        dizinler.AddRange(new[] { @"C:\Windows\Fonts", "/usr/share/fonts", "/usr/local/share/fonts", "/Library/Fonts" });

        foreach (var dizin in dizinler)
        {
            if (!Directory.Exists(dizin))
            {
                continue;
            }
            // Önce düz (Windows), sonra alt-dizinlerde (Linux dağıtımı) ara.
            foreach (var ad in AdayDosyalar)
            {
                var duz = Path.Combine(dizin, ad);
                if (File.Exists(duz))
                {
                    return File.ReadAllBytes(duz);
                }
            }
            foreach (var ad in AdayDosyalar)
            {
                var bulunan = DerinAra(dizin, ad);
                if (bulunan is not null)
                {
                    return File.ReadAllBytes(bulunan);
                }
            }
        }
        throw new InvalidOperationException(
            "PDF için kullanılabilir bir TrueType font bulunamadı (arial/DejaVu/Liberation). " +
            "Docker imajında bir font paketi kurun (ör. fonts-dejavu).");
    }

    /// <summary>Erişilemeyen alt-dizinleri atlayarak ilk eşleşen dosyayı bulur.</summary>
    private static string? DerinAra(string kok, string ad)
    {
        try
        {
            return Directory.EnumerateFiles(kok, ad, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
