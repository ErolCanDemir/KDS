namespace Kds.Api.Kurulum;

/// <summary>S16 — kurulumun kalıcı yapılandırması (şifreli <c>kurulum.json</c> içeriği ya da
/// ortam değişkenlerinden türetilmiş hâli). Yalnız ORTAMA bağlı değerler burada; işletme
/// ayarları (zaman aşımı, eşikler, SMTP…) A3'te yönetim veritabanındaki Sistem Ayarları'na gider.</summary>
public sealed class KurulumAyarlari
{
    public const int GecerliSurum = 1;

    public int Surum { get; set; } = GecerliSurum;

    /// <summary>Yönetim (metadata) veritabanı bağlantı dizesi. Boş = uygulama kurulmamış.</summary>
    public string MetadataBaglantisi { get; set; } = string.Empty;

    /// <summary>İçe alım deposunun SUNUCU bağlantısı (tenant başına DB burada yaratılır). Boşsa
    /// içe alım/depo yolu yapılandırılmamıştır.</summary>
    public string DepoBaglantisi { get; set; } = string.Empty;

    /// <summary>Olay günlüğü (Mongo) bağlantısı. Boş = günlük kapalı (no-op).</summary>
    public string LogBaglantisi { get; set; } = string.Empty;

    /// <summary>Kimlik sağlayıcı (Keycloak) — token doğrulaması bunlarla yapılır.</summary>
    public string KimlikYetkili { get; set; } = string.Empty;
    public string KimlikIstemci { get; set; } = string.Empty;
    public string? KimlikMetadataAdresi { get; set; }
    public bool KimlikHttpsZorunlu { get; set; } = true;

    /// <summary>S48 — issuer DESENİ (regex): dolu ise token issuer'ı sabit <see cref="KimlikYetkili"/>
    /// yerine bu desenle doğrulanır. Adresi oynak tünellerde (ör. *.lhr.life) yayın için:
    /// Keycloak hostname'i isteğe göre türetir, API de desene uyan her issuer'ı kabul eder.
    /// Boş = eski davranış (tek sabit issuer).</summary>
    public string? KimlikIssuerDeseni { get; set; }

    /// <summary>S27 — Roles &amp; Yetkiler: Keycloak Admin API'sine çıkan SERVİS hesabı (confidential
    /// client, client_credentials). Boşsa rol yönetimi ekranı "yapılandırılmadı" der; ürünün geri
    /// kalanı etkilenmez. Realm'de bu client'a realm-management izinleri verilir (infra/KURULUM.md).</summary>
    public string KimlikYonetimIstemci { get; set; } = string.Empty;
    public string KimlikYonetimSir { get; set; } = string.Empty;

    /// <summary>Önyüzün adresi (CORS). Boşsa kurulum modunda serbest, kurulu sistemde zorunludur.</summary>
    public string OnyuzAdres { get; set; } = string.Empty;

    public DateTimeOffset? KurulumTarihi { get; set; }

    /// <summary>Uygulamanın çalışabilmesi için asgari koşul: yönetim veritabanı bilinmeli.</summary>
    public bool Yapilandirildi => !string.IsNullOrWhiteSpace(MetadataBaglantisi);
}
