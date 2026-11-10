using Kds.Contracts.Connections;

namespace Kds.Business.Connections;

/// <summary>Bir bağlantının UI'a dönen özeti (paylaşılan yanıt: Ekle/Guncelle/Listele döner).
/// Parola ASLA dönmez; durum son test sonucundan türetilir (SonTestBasarili: null = test
/// edilmedi, true = bağlı, false = hata).</summary>
public sealed record ConnectionSummary(
    Guid Id,
    string Ad,
    string Sunucu,
    string Veritabani,
    string? KullaniciAdi,
    DataMode Mod,
    bool? SonTestBasarili,
    int? SonTestMs,
    DateTimeOffset? SonTestTarihi,
    // M22b: İçe Alım modunda depo hedefi (null = varsayılan tenant deposu).
    Guid? DepoBaglantiId = null,
    // S45: dolu = kaynağa müşteri ağındaki ajan bağlanır (sunucu doğrudan bağlanmaz).
    Guid? AjanId = null);
