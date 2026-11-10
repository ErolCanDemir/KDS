using Kds.Common.Cqrs;

namespace Kds.Contracts.Users;

/// <summary>JIT provisioning: geçerli token'daki kullanıcıyı local User tablosuna
/// ekler/günceller (ilk giriş) ve local kullanıcı Id'sini döndürür.</summary>
public sealed record ProvisionUserRequest(
    Guid KeycloakId,
    string KullaniciAdi,
    string? EPosta,
    string? AdSoyad,
    Guid TenantId) : ICommand<Guid>;
