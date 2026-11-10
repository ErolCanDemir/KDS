namespace Kds.Data.Entities;

/// <summary>Eşik uyarısı (S20 · "Uyarılar"): bir <see cref="Report"/>'u periyodik olarak koşup
/// çıktısındaki bir değeri eşikle karşılaştırır; <b>yalnız durum değişince</b> e-posta gönderir.
/// Zamanlama durumu — abonelik/içe alım işlerindeki gibi — işin kendi satırında yaşar.
///
/// <para><b>Aboneliğin kardeşi ama işi zıt:</b> abonelik takvim geldiğinde HER ZAMAN gönderir
/// ("işte bu sabahki rapor"); uyarı yalnız durum kötüyse gönderir ("dikkat, eşik aşıldı").
/// Bu yüzden aboneliğe alan eklenerek değil, ayrı varlık olarak modellenir: karşılaştırma tanımı
/// (kolon+özet+operatör+eşik) ve <b>durum hafızası</b> abonelikte yoktur.</para>
///
/// <para><b>Durum hafızası neden kritik:</b> onsuz, eşik aşılı kaldığı sürece her kontrolde posta
/// giderdi — saat başı kontrolde bir haftalık aşım 168 e-posta demektir ve özellik ilk haftasında
/// okunmaz hâle gelir. <see cref="Tetiklendi"/> bu yüzden vardır: sakin→tetiklendi geçişinde bir
/// bildirim, sonra sessizlik; normale dönünce "toparlandı" bildirimi.</para></summary>
public class Alert : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Ad { get; set; } = string.Empty;

    /// <summary>İzlenecek rapor (yumuşak referans; silinirse koşum anlaşılır hatayla düşer).
    /// S30'dan beri null olabilir: uyarı ya bir RAPORU ya bir METRİĞİ izler — tam biri dolu.</summary>
    public Guid? RaporId { get; set; }

    /// <summary>İzlenecek metrik (S30; yumuşak referans — rapor kardeşiyle aynı gerekçe). Metrik
    /// uyarısında kolon/özet gerekmez: metrik zaten tek, adlandırılmış bir değerdir; koşum
    /// <c>MeasureMetric</c>'le aynı kırılımsız modeli sahibin kimliğiyle koşar.</summary>
    public Guid? MetrikId { get; set; }

    /// <summary>Bakılacak çıkış kolonu (rapor sonucundaki başlık). <see cref="Ozet"/>
    /// <c>SatirSayisi</c> ise boş olabilir — o özet kolona bakmaz.</summary>
    public string? Kolon { get; set; }

    /// <summary>Satırlar tek değere nasıl indirgenir: "Toplam" | "EnBuyuk" | "EnKucuk" |
    /// "Ortalama" | "SatirSayisi" | "IlkSatir" (<c>OzetTuru</c> adı).</summary>
    public string Ozet { get; set; } = "SatirSayisi";

    /// <summary>Karşılaştırma: "Buyuk" | "BuyukEsit" | "Kucuk" | "KucukEsit" | "Esit" | "EsitDegil".</summary>
    public string Op { get; set; } = "Buyuk";

    public decimal Esik { get; set; }

    /// <summary>Rapor parametre değerleri (abonelikteki desenle aynı JSON sözlük).</summary>
    public string? ParametrelerJson { get; set; }

    /// <summary>Alıcı e-posta adresleri (JSON: <c>["a@x.com"]</c>).</summary>
    public string AlicilarJson { get; set; } = "[]";

    /// <summary>İşi kuran kullanıcının <b>kimlik anlık görüntüsü</b> (rol + iddia, JSON). Koşum arka
    /// planda istek bağlamı olmadan yapıldığı için satır-düzeyi güvenlik kuralları bu kimliğe
    /// uygulanır; olmadan rol-kapsamlı kurallar sessizce düşer ve kısıtlı satırlar e-postayla
    /// dışarı çıkardı. Kayıt her güncellemede tazelenir. Boşsa (eski kayıtlar) iş, kurallı veriye
    /// dokunuyorsa çalıştırılmaz — fail-closed.</summary>
    public string? SahipKimlikJson { get; set; }

    /// <summary>Cron ifadesi (5 ya da 6 alan); null → yalnız elle denenir.</summary>
    public string? Zamanlama { get; set; }

    public bool Aktif { get; set; } = true;

    /// <summary>Eşik aşılı KALDIĞI sürece her kontrolde tekrar bildirilsin mi? Varsayılan
    /// <c>false</c> — bilinçli: gürültü, uyarıyı işe yaramaz hâle getiren şeydir.</summary>
    public bool SurekliBildir { get; set; }

    /// <summary>Local <c>User.Id</c> (yumuşak referans) — uyarıyı kuran. Müşteri rolü yalnız
    /// KENDİ uyarılarını görür/yönetir (sahiplik kapısı).</summary>
    public Guid SahipId { get; set; }

    // Durum (zamanlayıcı + elle "Şimdi Dene" günceller).
    /// <summary>Şu an eşik aşılı mı? Bildirim YALNIZ bu değer değişince gider.</summary>
    public bool Tetiklendi { get; set; }

    public DateTimeOffset? SonKontrolTarihi { get; set; }
    public decimal? SonDeger { get; set; }
    public DateTimeOffset? SonBildirimTarihi { get; set; }
    public string? SonDurum { get; set; }   // "Sakin" | "Tetiklendi" | "Hata"
    public string? SonHata { get; set; }
}
