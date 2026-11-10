namespace Kds.Data.Entities;

/// <summary>S45 — KDS Ajanı kaydı: müşteri ağına kurulan, DIŞARI DOĞRU bağlanan hafif süreç
/// (Power BI On-premises Data Gateway modeli; 00-analiz §3). Ajan, ağdaki kaynaklardan içe alım
/// işlerini KENDİ tarafında koşturup veriyi KDS deposuna İTER — böylece SaaS'ın müşteri DB'sine
/// içeri doğru erişimi hiç gerekmez ve kaynak kimlik bilgileri müşteri ağından ÇIKMAZ (ajanın
/// yerel yapılandırmasında durur, KDS'de saklanmaz).
///
/// Anahtar deseni Gömme'yle (S42) aynı: anahtarın içinde yalnız bu kaydın kimliği taşınır (Data
/// Protection imzalı), yetki DB satırındadır → iptal ANINDA etkili. Anahtar üretimde BİR KEZ
/// gösterilir, saklanmaz.</summary>
public class Agent : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Ad { get; set; } = string.Empty;

    /// <summary>İptal SİLMEZ, işaretler — "hangi ajan ne zaman kapatıldı" denetlenebilsin.</summary>
    public DateTimeOffset? IptalTarihi { get; set; }

    /// <summary>Ajanın son iş yoklaması — ekran "çevrimiçi/çevrimdışı" çipini bundan çizer.</summary>
    public DateTimeOffset? SonGorulme { get; set; }

    /// <summary>Anahtarı üreten kullanıcı (yerel User.Id; yumuşak referans).</summary>
    public Guid OlusturanId { get; set; }
}
