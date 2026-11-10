using Kds.Data.Entities;

namespace Kds.Business.Connections;

/// <summary>Varlık → özet DTO eşlemesi (parola dışarı çıkmaz).</summary>
internal static class ConnectionMapping
{
    public static ConnectionSummary Ozet(Connection b) =>
        new(b.Id, b.Ad, b.Sunucu, b.Veritabani, b.KullaniciAdi, b.Mod,
            b.SonTestBasarili, b.SonTestMs, b.SonTestTarihi, b.DepoBaglantiId, b.AjanId);
}
