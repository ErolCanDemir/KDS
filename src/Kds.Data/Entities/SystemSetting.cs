namespace Kds.Data.Entities;

/// <summary>S16/A3 — Sistem Ayarı (anahtar/değer). İşletme ayarları (sorgu zaman aşımı, tanı
/// eşikleri, depo/SMTP bilgileri…) koda gömülü olmak yerine burada yaşar ve Yönetim →
/// Sistem Ayarları ekranından değiştirilir.
/// <para><see cref="TenantId"/>: null = kurulum GENELİ; dolu = o kiracıya özel ezme (SaaS'ta
/// "müşteri A'nın zaman aşımı 120 sn" gibi ihtiyaç kod değişmeden karşılansın). Bu tablo
/// bilerek <c>ITenantEntity</c> DEĞİLDİR — genel satırlar tüm kiracılara görünmeli.</para>
/// <para><see cref="SirMi"/>: parola/bağlantı gibi değerler Data Protection ile şifreli
/// saklanır ve API yanıtlarında düz metin DÖNMEZ.</para></summary>
public class SystemSetting : EntityBase
{
    public Guid? TenantId { get; set; }

    public string Anahtar { get; set; } = string.Empty;

    /// <summary>Değer (metin gösterimi; sayı/bool da metin olarak saklanır). Sır ise şifrelidir.</summary>
    public string Deger { get; set; } = string.Empty;

    public bool SirMi { get; set; }
}
