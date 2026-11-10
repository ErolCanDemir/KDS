using Kds.Data;

namespace Kds.Api.Kurulum;

/// <summary>S16/A1 — uygulamanın GÜNCEL kurulum durumu (singleton). Aynı zamanda
/// <see cref="IMetadataBaglantiSaglayici"/>'dir: DbContext her scope'ta bağlantıyı buradan alır,
/// böylece sihirbaz (A2) <see cref="Guncelle"/> çağırdığında uygulama YENİDEN BAŞLAMADAN yeni
/// veritabanına geçer. Yapılandırılmamışken bağlantı istenirse <see cref="KurulumGerekliException"/>
/// (→ 503 + kurulumGerekli) fırlar.</summary>
public sealed class KurulumDurumu(KurulumDeposu depo, KurulumAyarlari baslangic, string kaynak)
    : IMetadataBaglantiSaglayici
{
    private KurulumAyarlari ayarlar = baslangic;

    /// <summary>Ayarların hangi kaynaktan geldiği (log/teşhis için).</summary>
    public string Kaynak { get; private set; } = kaynak;

    public KurulumAyarlari Ayarlar => Volatile.Read(ref ayarlar);

    public bool Yapilandirildi => Ayarlar.Yapilandirildi;

    public string Dize() => Yapilandirildi ? Ayarlar.MetadataBaglantisi : throw new KurulumGerekliException();

    /// <summary>Sihirbazın kurulumu tamamladığı an: şifreli dosyaya yazar ve canlı durumu değiştirir.</summary>
    public void Guncelle(KurulumAyarlari yeni)
    {
        depo.Yaz(yeni);
        Volatile.Write(ref ayarlar, yeni);
        Kaynak = "kurulum dosyası";
    }
}
