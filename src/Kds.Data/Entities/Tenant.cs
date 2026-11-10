namespace Kds.Data.Entities;

/// <summary>Kiracı (müşteri). Id, Keycloak token'ındaki <c>tenant_id</c> ile birebir aynıdır.
///
/// <para>S35 — Kuruluş Ayarları: kiracının KENDİ kimlik ve marka bilgisi burada durur. Sorgu
/// limitleri BİLEREK burada DEĞİL: onlar Sistem Ayarları'nda tanımlı ve zaten kiracı bazında
/// ezilebiliyor (öncelik: kiracı → kurulum → varsayılan). Aynı eşiği iki yerde tutmak, birinin
/// diğerini sessizce geçersiz kılmasıyla biter.</para></summary>
public class Tenant : EntityBase
{
    public string Ad { get; set; } = string.Empty;
    public TenantStatus Durum { get; set; } = TenantStatus.Aktif;

    /// <summary>Plan/lisans adı — S35'te BİLGİ alanıdır, davranış değiştirmez. Fiyatlandırma
    /// modeli (hibrit: creator seat + tenant paketi) henüz iş kararı olarak AÇIK; karar verilince
    /// bu alan kota/özellik kapısına dönüşecek. O güne kadar serbest metin tutmak, uydurma bir
    /// plan sınıflandırmasını şemaya çakmaktan iyidir.</summary>
    public string? Plan { get; set; }

    /// <summary>Marka ana rengi (#RRGGBB) — ürün kabuğunda vurgu rengi olarak kullanılır.
    /// Beyaz-etiket/gömme (embed) kapsamı buraya DEĞİL, "Gömme &amp; Marka" maddesine (v2) aittir.</summary>
    public string? MarkaRengi { get; set; }

    /// <summary>Kuruluş logosu — <c>data:</c> URI olarak saklanır (küçük dosya; ayrı depolama
    /// katmanı ve statik dosya sunucusu gerektirmez). Boyut sınırı komut doğrulamasındadır.</summary>
    public string? LogoDataUri { get; set; }

    public ICollection<User> Kullanicilar { get; set; } = new List<User>();
}
