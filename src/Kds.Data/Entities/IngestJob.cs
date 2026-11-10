namespace Kds.Data.Entities;

/// <summary>İçe alım işi (S12, Mod B): İçe Alım modlu bir <see cref="Connection"/> kaynağından seçili
/// tabloları tenant'ın kendi deposuna (ayrı DB) çeker. <see cref="TablolarJson"/> = seçilen tablolar
/// (`[{Sema,Tablo,Kolonlar?}]`; Kolonlar null → tüm kolonlar). Zamanlama v1'de cron (Faz 2); durum
/// alanları son senkronu tutar. Kaynak/depo yumuşak referans.</summary>
public class IngestJob : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Ad { get; set; } = string.Empty;

    /// <summary>Kaynak bağlantı (İçe Alım modlu). Yumuşak referans.</summary>
    public Guid BaglantiId { get; set; }

    /// <summary>Seçilen tablolar (JSON): <c>[{Sema, Tablo, Kolonlar?}]</c>; Kolonlar null → tüm kolonlar.</summary>
    public string TablolarJson { get; set; } = "[]";

    /// <summary>Cron ifadesi (Faz 2 zamanlama); null → yalnız elle çalıştırılır.</summary>
    public string? Zamanlama { get; set; }

    public bool Aktif { get; set; } = true;

    /// <summary>S45 — "Şimdi Çalıştır" AJANLI işte sunucuda koşamaz; bu damga elle tetiği ajana
    /// taşır: ajan bir sonraki yoklamada damgalı işi görür, koşar, iş bitiminde damga silinir.
    /// null = bekleyen elle tetik yok (cron vadesi ayrıca hesaplanır).</summary>
    public DateTimeOffset? AjanTetikTarihi { get; set; }

    /// <summary>S44 — artımlı yükleme su seviyeleri (JSON: <c>{"sema.tablo": "son değer"}</c>).
    /// Değer, tablonun artımlı kolonunun son koşuda görülen en büyük değeridir (rowversion hex /
    /// sayı / ISO tarih — koşucu kolon tipine göre yorumlar). null/eksik anahtar → o tablo bir
    /// sonraki koşuda TAM yüklenir. "Tam Yükle" bu alanı sıfırlar.</summary>
    public string? ArtimliDurumJson { get; set; }

    // Son senkron durumu (Faz 2'de job güncelliyor; Faz 1'de elle çalıştırma da yazar).
    public DateTimeOffset? SonSenkronTarihi { get; set; }
    public int? SonSenkronSaniye { get; set; }
    public long? SonSatirSayisi { get; set; }
    public string? SonDurum { get; set; }   // "Basarili" | "Hata"
    public string? SonHata { get; set; }
}
