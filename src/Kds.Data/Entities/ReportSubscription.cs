namespace Kds.Data.Entities;

/// <summary>Rapor aboneliği (S15 · "Zamanlanmış Raporlar"): bir <see cref="Report"/>'u periyodik olarak
/// (cron) ya da elle çalıştırıp sonucunu seçilen biçimde (PDF/Excel/CSV) e-posta ekiyle alıcılara
/// gönderir. Zamanlama durumu — İçe Alım işlerindeki gibi — işin kendi satırında yaşar (ayrı job
/// deposu yok; zamanlayıcı <see cref="SonGonderimTarihi"/>'ni taban alır). Rapor/sahip yumuşak referans.
/// <see cref="AlicilarJson"/> = e-posta adresleri (JSON string[]).</summary>
public class ReportSubscription : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Ad { get; set; } = string.Empty;

    /// <summary>Gönderilecek rapor (yumuşak referans; silinirse gönderim anlaşılır hatayla düşer).</summary>
    public Guid RaporId { get; set; }

    /// <summary>Ek biçimi: "Pdf" | "Xlsx" | "Csv" (Kds.Export.ExportFormat adı). Data→Export bağını
    /// açmamak için string tutulur; handler <c>ExportFormat</c>'a çözer.</summary>
    public string Bicim { get; set; } = "Pdf";

    /// <summary>Alıcı e-posta adresleri (JSON: <c>["a@x.com","b@y.com"]</c>).</summary>
    public string AlicilarJson { get; set; } = "[]";

    /// <summary>Rapor parametre değerleri (M24/M25 — JSON sözlük: anahtar→değer; TarihAralik ucu
    /// <c>"$bugun"</c> olabilir → çalıştırma günü). Null/boş = değersiz koş (parametresiz rapor).</summary>
    public string? ParametrelerJson { get; set; }

    /// <summary>İşi kuran kullanıcının <b>kimlik anlık görüntüsü</b> (rol + iddia, JSON). Koşum arka
    /// planda istek bağlamı olmadan yapıldığı için satır-düzeyi güvenlik kuralları bu kimliğe
    /// uygulanır; olmadan rol-kapsamlı kurallar sessizce düşer ve kısıtlı satırlar e-postayla
    /// dışarı çıkardı. Kayıt her güncellemede tazelenir. Boşsa (eski kayıtlar) iş, kurallı veriye
    /// dokunuyorsa çalıştırılmaz — fail-closed.</summary>
    public string? SahipKimlikJson { get; set; }

    /// <summary>Cron ifadesi (5 ya da 6 alan); null → yalnız elle gönderilir.</summary>
    public string? Zamanlama { get; set; }

    public bool Aktif { get; set; } = true;

    /// <summary>Local <c>User.Id</c> (yumuşak referans) — aboneliği kuran.</summary>
    public Guid SahipId { get; set; }

    // Son gönderim durumu (zamanlayıcı + elle "Şimdi Gönder" günceller).
    public DateTimeOffset? SonGonderimTarihi { get; set; }
    public int? SonAliciSayisi { get; set; }
    public long? SonSatirSayisi { get; set; }
    public string? SonDurum { get; set; }   // "Basarili" | "Hata"
    public string? SonHata { get; set; }
}
