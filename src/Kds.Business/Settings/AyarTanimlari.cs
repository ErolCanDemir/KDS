namespace Kds.Business.Settings;

/// <summary>S16/A3 — ayarların TEK doğruluk kaynağı: anahtar, tip, varsayılan ve açıklama.
/// Ekran bu listeden çizilir (yeni ayar eklemek = buraya bir satır + tüketicide okuma).
/// <para>Varsayılanların kodda olması sorun değildir — bunlar ORTAMA bağlı değil, ürünün makul
/// başlangıç değerleridir; ortama bağlı olanlar (bağlantılar) kurulumdan/ayardan gelir ve
/// varsayılanları BOŞTUR.</para></summary>
public static class AyarTanimlari
{
    public const string SorguZamanAsimiSn = "Sorgu.ZamanAsimiSn";
    public const string SorguVarsayilanSatir = "Sorgu.VarsayilanSatir";
    public const string SorguEnCokSatir = "Sorgu.EnCokSatir";
    public const string TaniPlanZamanAsimiSn = "Tani.PlanZamanAsimiSn";
    public const string TaniTaramaEsigi = "Tani.TaramaEsigi";
    public const string TaniSiralamaEsigi = "Tani.SiralamaEsigi";
    public const string TaniYavasEsikMs = "Tani.YavasEsikMs";
    public const string DepoSunucuBaglantisi = "Depo.SunucuBaglantisi";
    public const string DepoDbAdiDeseni = "Depo.DbAdiDeseni";
    public const string EpostaSunucu = "Eposta.Sunucu";
    public const string EpostaPort = "Eposta.Port";
    public const string EpostaSsl = "Eposta.SslKullan";
    public const string EpostaKullanici = "Eposta.Kullanici";
    public const string EpostaParola = "Eposta.Parola";
    public const string EpostaGonderen = "Eposta.Gonderen";
    public const string SoruSorTabanAdres = "SoruSor.TabanAdres";
    public const string SoruSorModel = "SoruSor.Model";

    /// <summary>Depo DB adı deseninde kiracı kimliğinin yerine geçen yer tutucu.</summary>
    public const string KiraciYerTutucu = "{kiraci}";

    public static readonly IReadOnlyList<AyarTanimi> Tumu =
    [
        new(SorguZamanAsimiSn, "Sorgu", "Sorgu zaman aşımı (sn)", AyarTipi.Sayi, "60",
            "Bir sorgunun çalışabileceği en uzun süre. Büyük tablolarda ilk (soğuk) koşum uzun sürebilir."),
        new(SorguVarsayilanSatir, "Sorgu", "Varsayılan satır sayısı", AyarTipi.Sayi, "1000",
            "Satır limiti verilmemiş sorgularda kaç satır okunur."),
        new(SorguEnCokSatir, "Sorgu", "Sert satır tavanı", AyarTipi.Sayi, "10000",
            "Kullanıcı daha fazlasını istese de aşılamaz — kaçak sorgulara karşı emniyet."),
        new(TaniPlanZamanAsimiSn, "Tanı", "Plan alma zaman aşımı (sn)", AyarTipi.Sayi, "30",
            "\"Sorun ne?\" tanısında tahmini planın alınması için beklenecek süre."),
        new(TaniTaramaEsigi, "Tanı", "Büyük tarama eşiği (satır)", AyarTipi.Sayi, "500000",
            "Bir tablodan bu kadar satır OKUNUYORSA \"büyük tarama\" bulgusu üretilir."),
        new(TaniSiralamaEsigi, "Tanı", "Büyük sıralama eşiği (satır)", AyarTipi.Sayi, "100000",
            "TOP ile birlikte bu boyutta bir sıralama varsa row-goal uyarısı verilir."),
        new(TaniYavasEsikMs, "Tanı", "\"Yavaş sorgu\" eşiği (ms)", AyarTipi.Sayi, "10000",
            "Bu süreyi aşan koşumlarda ekranda \"Sorun ne?\" düğmesi belirir."),

        new(DepoSunucuBaglantisi, "Depo", "Depo sunucusu bağlantısı", AyarTipi.Sir, "",
            "İçe alım verisinin yazılacağı SQL sunucusu (veritabanı adı verilmez — kiracı başına " +
            "aşağıdaki desenle üretilir). Boşsa kurulumdaki değer kullanılır."),
        new(DepoDbAdiDeseni, "Depo", "Depo veritabanı adı deseni", AyarTipi.Metin, "KdsDepo_{kiraci}",
            "Kiracı başına depo veritabanının adı. {kiraci} yer tutucusu kiracı kimliğiyle değişir."),

        new(EpostaSunucu, "E-posta", "SMTP sunucusu", AyarTipi.Metin, "",
            "Boş bırakılırsa e-posta GÖNDERİLMEZ; abonelikler yalnız günlüğe yazılır (dev davranışı)."),
        new(EpostaPort, "E-posta", "SMTP portu", AyarTipi.Sayi, "587", "Genellikle 587 (STARTTLS) ya da 465."),
        new(EpostaSsl, "E-posta", "SSL/TLS kullan", AyarTipi.EvetHayir, "true", "Şifreli bağlantı."),
        new(EpostaKullanici, "E-posta", "SMTP kullanıcısı", AyarTipi.Metin, "", "Boş = kimlik doğrulamasız gönderim."),
        new(EpostaParola, "E-posta", "SMTP parolası", AyarTipi.Sir, "", "Şifreli saklanır; ekranda gösterilmez."),
        new(EpostaGonderen, "E-posta", "Gönderen adresi", AyarTipi.Metin, "",
            "Aboneliklerin \"Kimden\" adresi (ör. rapor@firma.com). SMTP kullanılacaksa zorunludur."),

        // S23 — Soru Sor (NLQ). SQLST ailesiyle aynı altyapı: YEREL Ollama + qwen2.5-coder:7b
        // (Apache-2.0). Soru ve şema adları asla internete gitmez; model bu makinede koşar.
        new(SoruSorTabanAdres, "Soru Sor", "Yerel model adresi", AyarTipi.Metin, "http://127.0.0.1:11434",
            "Ollama servisinin adresi. Varsayılan, aynı makinede koşan Ollama'dır — soru hiçbir zaman dışarı gitmez."),
        new(SoruSorModel, "Soru Sor", "Model", AyarTipi.Metin, "qwen2.5-coder:7b",
            "Kullanılacak yerel model. Kurulum: ollama pull qwen2.5-coder:7b (Apache-2.0; SQLST ailesiyle aynı model)."),
    ];

    public static AyarTanimi? Bul(string anahtar) => Tumu.FirstOrDefault(t => t.Anahtar == anahtar);
}

public enum AyarTipi { Metin = 0, Sayi, EvetHayir, Sir }

/// <summary>Tek ayarın tanımı. <see cref="Sir"/> tipindekiler şifreli saklanır ve API'ye düz dönmez.</summary>
public sealed record AyarTanimi(
    string Anahtar,
    string Grup,
    string Etiket,
    AyarTipi Tip,
    string Varsayilan,
    string Aciklama);
