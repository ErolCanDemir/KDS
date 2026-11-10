namespace Kds.Data.Entities;

/// <summary>İki dataMap tablosu arasındaki ilişki (join): kaynak.kolon → hedef.kolon.
/// FK'dan otomatik önerilmiş ya da elle eklenmiş olabilir. Tablo referansları aggregate içi
/// yumuşak referanstır (FK kısıtı yok — çoklu cascade yolundan kaçınmak için).</summary>
public class DataMapRelationship : EntityBase
{
    public Guid VeriHaritasiId { get; set; }
    public Guid KaynakTabloId { get; set; }
    public string KaynakKolon { get; set; } = string.Empty;
    public Guid HedefTabloId { get; set; }
    public string HedefKolon { get; set; } = string.Empty;

    /// <summary>FK'dan mı önerildi (true) yoksa elle mi eklendi (false).</summary>
    public bool FktenMi { get; set; }
}
