using System.Globalization;
using Kds.Contracts.Alerts;
using Kds.Contracts.Queries;

namespace Kds.Business.Alerts;

/// <summary>Uyarı değerlendirmesinin SAF çekirdeği: sonuç kümesi → tek değer → eşik kararı.
/// IO'suz olduğu için doğrudan test edilir; koşum/gönderim kabuğu <see cref="RunAlertHandler"/>'da.</summary>
public static class UyariDegerlendirme
{
    /// <summary>Sonucu tek değere indirger. <see cref="OzetTuru.SatirSayisi"/> kolona bakmaz;
    /// diğerlerinde kolon adı sonuçta yoksa <b>hata verilir</b> — sessizce 0 dönmek, uyarının
    /// "her şey yolunda" demesi anlamına gelirdi ve bu en tehlikeli sessiz arızadır.</summary>
    public static decimal? Ozetle(QueryRunResult sonuc, OzetTuru ozet, string? kolon) =>
        Ozetle(sonuc, ozet, kolon, op: null, esik: null);

    /// <summary>Özet + <b>kesme denetimi</b>. <paramref name="op"/>/<paramref name="esik"/> verilirse
    /// kesilmiş sonuçta kararın yine de KESİN olduğu durumlar geçirilir (aşağıya bakınız).</summary>
    public static decimal? Ozetle(QueryRunResult sonuc, OzetTuru ozet, string? kolon, UyariOp? op, decimal? esik)
    {
        if (sonuc.SatirSiniriUygulandi && ozet != OzetTuru.IlkSatir && !KesmeKararaEngelDegil(sonuc, ozet, op, esik))
        {
            throw new InvalidOperationException(
                "Uyarı ölçülemedi: rapor satır sınırına takıldı, dönen sonuç EKSİK. Eksik veriyle " +
                "eşik kararı vermek yanlış sonuç üretir (ör. satır sayısı sınırda takılı kalır). " +
                "Raporu daraltın (filtre/gruplama ekleyin), raporun satır limitini yükseltin ya da " +
                "uyarıyı özetlenmiş (gruplanmış) bir rapora bağlayın.");
        }

        if (ozet == OzetTuru.SatirSayisi)
        {
            return sonuc.Satirlar.Count;
        }

        var indeks = KolonIndeksi(sonuc, kolon);

        // İlkSatir adının hakkını verir: GERÇEKTEN ilk satıra bakar. Önceden "ilk SAYISAL değer"e
        // bakıyordu — ilk satırın hücresi NULL ise sessizce bir sonraki satırın değeri ölçülüyordu,
        // yani kullanıcının "en üstteki değer" beklentisiyle ölçülen şey ayrışıyordu (bağımsız
        // inceleme bulgusu). İlk hücre sayı değilse karar verilemez (null → alarm yok).
        if (ozet == OzetTuru.IlkSatir)
        {
            return sonuc.Satirlar.Count > 0 && indeks < sonuc.Satirlar[0].Count
                ? Sayiya(sonuc.Satirlar[0][indeks])
                : null;
        }

        var degerler = sonuc.Satirlar
            .Select(s => indeks < s.Count ? Sayiya(s[indeks]) : null)
            .Where(d => d is not null)
            .Select(d => d!.Value)
            .ToList();

        if (degerler.Count == 0)
        {
            return null;   // satır yok ya da hiçbiri sayısal değil → karar verilemez
        }

        try
        {
            return ozet switch
            {
                OzetTuru.Toplam => degerler.Sum(),
                OzetTuru.Ortalama => degerler.Average(),
                OzetTuru.EnBuyuk => degerler.Max(),
                OzetTuru.EnKucuk => degerler.Min(),
                _ => null,
            };
        }
        catch (OverflowException)
        {
            // Dar-tipli çeviri (AGENTS §4.6): astronomik float değerlerin toplamı decimal aralığını
            // aşabilir. Ham OverflowException kullanıcıya hiçbir şey söylemez; bu mesaj söyler.
            throw new InvalidOperationException(
                "Uyarı ölçülemedi: değerler toplanamayacak kadar büyük (decimal aralığı aşıldı). " +
                "Raporu filtreleyin ya da özetlenmiş (gruplanmış) bir rapora bağlayın.");
        }
    }

    /// <summary>Kesilmiş sonuç bu kararı BOZAR mı? Bozmadığı bir hâl vardır ve önemlidir:
    /// <b>kesilmiş satır sayısı gerçek sayının ALT SINIRIdır</b>. "Satır sayısı &gt; N" kuralında
    /// kesik sayı eşiği zaten aşıyorsa gerçek sayı da aşar — karar kesindir.
    ///
    /// <para>Bu ayrım olmadan kapı fazla geniş kalıyordu: ekranın önerdiği varsayılan kural
    /// ("Satır sayısı &gt; 0") satır sınırını aşan HER raporda kalıcı "Hata"ya düşer, doğru çalışan
    /// uyarı hiç e-posta göndermezdi. Diğer özetler (Toplam/Ortalama/En büyük/En küçük) kesmeden
    /// gerçekten bozulur; onlarda reddetmek doğrudur.</para></summary>
    private static bool KesmeKararaEngelDegil(QueryRunResult sonuc, OzetTuru ozet, UyariOp? op, decimal? esik) =>
        ozet == OzetTuru.SatirSayisi
        && esik is { } e
        && op is UyariOp.Buyuk or UyariOp.BuyukEsit
        && Asildi(sonuc.Satirlar.Count, op.Value, e);

    /// <summary>Eşik aşıldı mı? Değer yoksa (ölçülemedi) <c>false</c> — "veri yok" ile "eşik aşıldı"
    /// aynı şey değildir; ölçülemeyen durum alarma dönüştürülmez.</summary>
    public static bool Asildi(decimal? deger, UyariOp op, decimal esik) => deger is { } d && op switch
    {
        UyariOp.Buyuk => d > esik,
        UyariOp.BuyukEsit => d >= esik,
        UyariOp.Kucuk => d < esik,
        UyariOp.KucukEsit => d <= esik,
        UyariOp.Esit => d == esik,
        UyariOp.EsitDegil => d != esik,
        _ => false,
    };

    /// <summary>Bildirim gönderilmeli mi? Kural: <b>yalnız durum değişince</b>. Eşik aşılı kaldığı
    /// sürece her kontrolde göndermek (saat başı kontrolde bir hafta = 168 e-posta) uyarıyı ilk
    /// haftasında okunmaz hâle getirir — özelliği öldüren şey budur. "Sürekli bildir" isteyen
    /// açıkça seçer.</summary>
    public static bool BildirilmeliMi(bool oncekiTetik, bool simdikiTetik, bool surekliBildir) =>
        oncekiTetik != simdikiTetik || (simdikiTetik && surekliBildir);

    /// <summary>İnsan okunur karşılaştırma metni ("Toplam Tutar > 500").</summary>
    public static string KosulMetni(OzetTuru ozet, string? kolon, UyariOp op, decimal esik)
    {
        var sol = ozet == OzetTuru.SatirSayisi ? "Satır sayısı" : $"{OzetAdi(ozet)} {kolon}".Trim();
        return $"{sol} {OpIsareti(op)} {esik.ToString("0.####", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Metrik uyarısının karşılaştırma metni ("Metrik 'Toplam Ciro' > 500") — S30.</summary>
    public static string MetrikKosulMetni(string metrikAd, UyariOp op, decimal esik) =>
        $"Metrik '{metrikAd}' {OpIsareti(op)} {esik.ToString("0.####", CultureInfo.InvariantCulture)}";

    private static string OzetAdi(OzetTuru o) => o switch
    {
        OzetTuru.Toplam => "Toplam",
        OzetTuru.Ortalama => "Ortalama",
        OzetTuru.EnBuyuk => "En büyük",
        OzetTuru.EnKucuk => "En küçük",
        OzetTuru.IlkSatir => "İlk satır",
        _ => "Satır sayısı",
    };

    private static string OpIsareti(UyariOp op) => op switch
    {
        UyariOp.Buyuk => ">",
        UyariOp.BuyukEsit => "≥",
        UyariOp.Kucuk => "<",
        UyariOp.KucukEsit => "≤",
        UyariOp.Esit => "=",
        _ => "≠",
    };

    private static int KolonIndeksi(QueryRunResult sonuc, string? kolon)
    {
        if (string.IsNullOrWhiteSpace(kolon))
        {
            throw new InvalidOperationException("Uyarı için izlenecek kolon seçilmemiş.");
        }

        var i = sonuc.Kolonlar.ToList().FindIndex(k =>
            string.Equals(k.Ad, kolon, StringComparison.OrdinalIgnoreCase));
        return i >= 0
            ? i
            : throw new InvalidOperationException(
                $"Uyarının izlediği '{kolon}' kolonu raporun çıktısında yok. Rapor değişmiş olabilir — " +
                "uyarıyı güncelleyin (mevcut kolonlar: " + string.Join(", ", sonuc.Kolonlar.Select(k => k.Ad)) + ").");
    }

    /// <summary>Hücreyi decimal'a çevirir (dar-tipli parse — AGENTS §4.6 izinli bölge).
    /// <list type="bullet">
    /// <item><b>bool/bit:</b> 1/0 sayılır — "hatalı kayıt var mı" türü bit kolonlarında
    /// "Toplam = kaç tanesi işaretli" doğal biçimde çalışsın (önceden sessizce atlanıyor, uyarı
    /// "Sakin" görünerek körleşiyordu — bağımsız inceleme bulgusu).</item>
    /// <item><b>NaN:</b> sayı değil → null (ölçülemedi; alarm üretilmez).</item>
    /// <item><b>±Sonsuz / decimal aralığı dışı float:</b> uca SATÜRE edilir — eşik
    /// karşılaştırması için doğru anlam budur (1e30'luk bir okuma her eşiğin üstündedir);
    /// ham dönüşüm OverflowException fırlatıp uyarıyı anlamsız bir "Hata"ya sokuyordu.</item>
    /// </list></summary>
    private static decimal? Sayiya(object? hucre) => hucre switch
    {
        null => null,
        decimal d => d,
        int i => i,
        long l => l,
        short s => s,
        byte b => b,
        bool b => b ? 1m : 0m,
        double db => DoubledanGuvenli(db),
        float f => DoubledanGuvenli(f),
        string m when decimal.TryParse(m, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
        _ => null,
    };

    private static decimal? DoubledanGuvenli(double d) => d switch
    {
        double.NaN => null,
        >= (double)decimal.MaxValue => decimal.MaxValue,
        <= (double)decimal.MinValue => decimal.MinValue,
        _ => (decimal)d,
    };
}
