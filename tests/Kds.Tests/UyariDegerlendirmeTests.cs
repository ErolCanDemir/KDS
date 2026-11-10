using Kds.Business.Alerts;
using Kds.Contracts.Alerts;
using Kds.Contracts.Queries;
using Xunit;

namespace Kds.Tests;

/// <summary>S20 uyarı çekirdeği: sonuç → tek değer → eşik kararı → BİLDİRİM kararı.
/// En kritik davranış sonuncusudur: uyarı, eşik aşılı kaldığı sürece susmalıdır. Susmazsa saat başı
/// kontrolde bir haftalık aşım 168 e-posta demektir ve özellik ilk haftasında okunmaz hâle gelir —
/// bu tür bir özelliği öldüren şey yanlış eşik değil, gürültüdür.</summary>
public sealed class UyariDegerlendirmeTests
{
    private static QueryRunResult Sonuc(string kolon, params object?[] degerler) =>
        new([new QueryColumnDto(kolon, "int")], degerler.Select(d => (IReadOnlyList<object?>)[d]).ToList(), false, 1);

    [Fact]
    public void satir_sayisi_kolona_bakmaz()
    {
        // "Hatalı kayıt VARSA haber ver" — en yaygın uyarı biçimi; kolon seçilmesi gerekmez.
        Assert.Equal(3, UyariDegerlendirme.Ozetle(Sonuc("Adet", 5, 7, 9), OzetTuru.SatirSayisi, kolon: null));
        Assert.Equal(0, UyariDegerlendirme.Ozetle(Sonuc("Adet"), OzetTuru.SatirSayisi, kolon: null));
    }

    [Theory]
    [InlineData(OzetTuru.Toplam, 21)]
    [InlineData(OzetTuru.EnBuyuk, 9)]
    [InlineData(OzetTuru.EnKucuk, 5)]
    [InlineData(OzetTuru.Ortalama, 7)]
    [InlineData(OzetTuru.IlkSatir, 5)]
    public void ozetler_dogru_hesaplanir(OzetTuru ozet, int beklenen)
    {
        Assert.Equal(beklenen, UyariDegerlendirme.Ozetle(Sonuc("Adet", 5, 7, 9), ozet, "Adet"));
    }

    [Fact]
    public void kolon_sonucta_yoksa_hata_verilir_sessizce_sifir_donmez()
    {
        // Sessizce 0 dönmek "her şey yolunda" demek olurdu: rapor değişince uyarı sessizce körleşir.
        var hata = Assert.Throws<InvalidOperationException>(() =>
            UyariDegerlendirme.Ozetle(Sonuc("Adet", 1), OzetTuru.Toplam, "OlmayanKolon"));
        Assert.Contains("OlmayanKolon", hata.Message);
        Assert.Contains("Adet", hata.Message);   // mevcut kolonları söyler → düzeltmesi kolay
    }

    [Fact]
    public void olculemeyen_deger_alarma_donusturulmez()
    {
        // Satır yok / hiçbiri sayısal değil → "veri yok" ile "eşik aşıldı" aynı şey DEĞİLDİR.
        Assert.Null(UyariDegerlendirme.Ozetle(Sonuc("Adet"), OzetTuru.Toplam, "Adet"));
        Assert.False(UyariDegerlendirme.Asildi(null, UyariOp.Buyuk, 0));
        Assert.False(UyariDegerlendirme.Asildi(null, UyariOp.Kucuk, 1_000_000));
    }

    [Theory]
    [InlineData(UyariOp.Buyuk, 500, 501, true)]
    [InlineData(UyariOp.Buyuk, 500, 500, false)]
    [InlineData(UyariOp.BuyukEsit, 500, 500, true)]
    [InlineData(UyariOp.Kucuk, 10, 9, true)]
    [InlineData(UyariOp.KucukEsit, 10, 10, true)]
    [InlineData(UyariOp.Esit, 0, 0, true)]
    [InlineData(UyariOp.EsitDegil, 0, 1, true)]
    public void esik_karsilastirmasi(UyariOp op, int esik, int deger, bool beklenen)
    {
        Assert.Equal(beklenen, UyariDegerlendirme.Asildi(deger, op, esik));
    }

    [Fact]
    public void bildirim_YALNIZ_durum_degisince_gider()
    {
        // sakin → tetiklendi: haber ver
        Assert.True(UyariDegerlendirme.BildirilmeliMi(oncekiTetik: false, simdikiTetik: true, surekliBildir: false));
        // tetiklendi → tetiklendi: SUS (özelliği öldüren spam tam burada doğar)
        Assert.False(UyariDegerlendirme.BildirilmeliMi(true, true, false));
        // tetiklendi → sakin: "normale döndü" bilgisi de gider; yoksa "sustu mu, düzeldi mi?" belirsizliği kalır
        Assert.True(UyariDegerlendirme.BildirilmeliMi(true, false, false));
        // sakin → sakin: sus
        Assert.False(UyariDegerlendirme.BildirilmeliMi(false, false, false));
    }

    [Fact]
    public void surekli_bildir_acikca_secilirse_her_kontrolde_gider()
    {
        Assert.True(UyariDegerlendirme.BildirilmeliMi(true, true, surekliBildir: true));
        // Sakin durumda "sürekli bildir" bile sessizdir — uyarı, iyi haberi tekrar tekrar duyurmaz.
        Assert.False(UyariDegerlendirme.BildirilmeliMi(false, false, surekliBildir: true));
    }

    [Fact]
    public void kosul_metni_insan_okunur()
    {
        Assert.Equal("Toplam Tutar > 500", UyariDegerlendirme.KosulMetni(OzetTuru.Toplam, "Tutar", UyariOp.Buyuk, 500));
        Assert.Equal("Satır sayısı > 0", UyariDegerlendirme.KosulMetni(OzetTuru.SatirSayisi, null, UyariOp.Buyuk, 0));
    }
}

/// <summary>19-20 Ağu 2026 bağımsız incelemesinin KÜÇÜK bulguları — kapatılırken teste bağlandı.</summary>
public sealed class UyariDegerlendirmeKucukBulgularTests
{
    private static QueryRunResult Sonuc(params object?[][] satirlar) =>
        new([new QueryColumnDto("Deger", "int")], satirlar.Select(s => (IReadOnlyList<object?>)s).ToList(), false, 1);

    [Fact]
    public void ilk_satir_GERCEKTEN_ilk_satira_bakar()
    {
        // Önceden "ilk SAYISAL değer" ölçülüyordu: ilk satır NULL ise sessizce ikinci satırın
        // değeri alınıyor, kullanıcının "en üstteki değer" beklentisi ile ölçüm ayrışıyordu.
        Assert.Equal(7, UyariDegerlendirme.Ozetle(Sonuc([7], [99]), OzetTuru.IlkSatir, "Deger"));
        // İlk hücre NULL → karar verilemez (null); 99'a KAYMAZ.
        Assert.Null(UyariDegerlendirme.Ozetle(Sonuc([null], [99]), OzetTuru.IlkSatir, "Deger"));
    }

    [Fact]
    public void bit_kolonlar_sayilir()
    {
        // "hatalı kayıt var mı" tipik olarak bit kolonudur; önceden bool hücreler sessizce
        // atlanıyor ve uyarı "Sakin" görünerek körleşiyordu.
        Assert.Equal(2, UyariDegerlendirme.Ozetle(Sonuc([true], [true], [false]), OzetTuru.Toplam, "Deger"));
    }

    [Fact]
    public void nan_ve_tasan_float_uyariyi_dusurmez()
    {
        // NaN sayı değildir → ölçülemedi (null, alarm yok).
        Assert.Null(UyariDegerlendirme.Ozetle(Sonuc([double.NaN]), OzetTuru.EnBuyuk, "Deger"));
        // decimal aralığını aşan float uca satüre edilir: 1e300'lük okuma HER eşiğin üstündedir —
        // önceden ham dönüşüm OverflowException fırlatıp uyarıyı anlamsız "Hata"ya sokuyordu.
        Assert.Equal(decimal.MaxValue, UyariDegerlendirme.Ozetle(Sonuc([1e300]), OzetTuru.EnBuyuk, "Deger"));
        Assert.True(UyariDegerlendirme.Asildi(decimal.MaxValue, UyariOp.Buyuk, 500));
    }
}
