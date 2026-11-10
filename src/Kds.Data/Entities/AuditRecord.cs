namespace Kds.Data.Entities;

/// <summary>Bir sorgu icrasının denetim kaydı (00-analiz §7). Başarılı ve başarısız/engellenmiş
/// denemeler yazılır (güvenlik için reddedilen denemeler de görünür). Parola / gerçek iş verisi
/// TUTULMAZ — yalnız SQL metni + ölçümler.</summary>
public class AuditRecord : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Kim çalıştırdı (local User.Id). Kullanıcı bağlamı olan icra ucu (S8)
    /// gelince doldurulur; şimdilik boş olabilir.</summary>
    public Guid? KullaniciId { get; set; }

    public Guid? BaglantiId { get; set; }
    public string Sql { get; set; } = string.Empty;

    /// <summary>Parametre adı→değer sözlüğünün JSON'u (kod incelemesi O5): SQL şablonu tek başına
    /// "ne arandı"yı göstermiyordu. Değerler kullanıcı girdisidir (parola/sır değil).</summary>
    public string? ParametrelerJson { get; set; }

    public bool Basarili { get; set; }
    public int? SatirSayisi { get; set; }
    public int SureMs { get; set; }
    public string? Hata { get; set; }

    /// <summary>Koşum kayıtlı bir RAPOR üzerinden geldiyse raporun kimliği (M26 — yumuşak referans;
    /// tasarımcı/SQL editörü koşumlarında null).</summary>
    public Guid? RaporId { get; set; }
}
