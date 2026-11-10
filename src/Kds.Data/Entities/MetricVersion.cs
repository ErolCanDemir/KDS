namespace Kds.Data.Entities;

/// <summary>Metrik sürüm geçmişi (S30): metrik her güncellendiğinde bir ÖNCEKİ hâli buraya
/// yazılır — "ciro tanımı geçen ay neydi, kim değiştirdi?" sorusunun cevabı. Metrik referansla
/// kullanıldığı için tanım değişikliği tüm raporları anında etkiler; bu güç, ancak geçmişi
/// görülebilir ve geri alınabilirse güvenle kullanılır.
///
/// <para><b>Satır = eski hâl:</b> güncelleme anında değiştirilmeden ÖNCEKİ değerler saklanır;
/// güncel hâl her zaman <see cref="Metric"/> satırının kendisidir. Sürüm numarası 1'den artar,
/// metrik başına benzersizdir. "Sürüme dön" normal kaydetme yolundan geçer — yani formül yeniden
/// doğrulanır ve dönüş de kendi başına bir sürüm üretir (geçmiş hiç silinmez).</para></summary>
public class MetricVersion : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Sahip metrik — metrik silinirse geçmişi de gider (kaskad; öksüz geçmişin alıcısı yok).</summary>
    public Guid MetrikId { get; set; }

    /// <summary>1'den artan sürüm numarası (metrik başına).</summary>
    public int SurumNo { get; set; }

    // Saklanan (eski) tanım — Metric alanlarının birebir kopyası.
    public string Ad { get; set; } = string.Empty;
    public string Ifade { get; set; } = string.Empty;
    public string Bicim { get; set; } = "Sayi";
    public string? Aciklama { get; set; }

    /// <summary>Bu hâli DEĞİŞTİREN kullanıcı (local <c>User.Id</c>, yumuşak referans) — yani bu
    /// satırı tarihe gömen güncellemeyi yapan kişi.</summary>
    public Guid DegistirenId { get; set; }
}
