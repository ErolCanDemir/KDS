namespace Kds.Contracts.Users;

/// <summary>Kullanıcı listesi ekranı için özet satır.</summary>
public sealed record UserSummary(
    string KullaniciAdi,
    string? EPosta,
    string? AdSoyad,
    DateTimeOffset? SonGirisTarihi);
