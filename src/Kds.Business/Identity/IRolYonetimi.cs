using Kds.Contracts.Identity;

namespace Kds.Business.Identity;

/// <summary>S27 — rol yönetimi SINIRI: iş katmanı "rolleri ver / rol aç / üye ata"dan fazlasını
/// bilmez. Somut uygulama Api katmanındaki Keycloak Admin API adaptörüdür (EpostaGonderici /
/// OllamaSoruCevirici ile aynı desen: IO kabuğu dışarıda, sahteyle test edilir).</summary>
public interface IRolYonetimi
{
    Task<IReadOnlyList<RolOzeti>> RolleriListeleAsync(CancellationToken ct);

    Task<RolOzeti> RolOlusturAsync(string ad, string? aciklama, CancellationToken ct);

    /// <summary>Rolü siler. Üyesi olan rol silinmez (çağıran önce üyeleri çıkarmalı).</summary>
    Task SilAsync(string ad, CancellationToken ct);

    Task<IReadOnlyList<RolUyesi>> UyeleriListeleAsync(string rolAdi, CancellationToken ct);

    Task UyeEkleAsync(string rolAdi, Guid keycloakId, CancellationToken ct);

    Task UyeCikarAsync(string rolAdi, Guid keycloakId, CancellationToken ct);

    /// <summary>Kiracıya göre süzülmüş kullanıcı araması (üye ekleme adayları).</summary>
    Task<IReadOnlyList<RolUyesi>> KullaniciAraAsync(string? arama, Guid tenantId, CancellationToken ct);

    Task<RolYonetimDurumu> DurumAsync(CancellationToken ct);
}

/// <summary>Rol servisine ulaşılamadı / Keycloak reddetti — YAPILANDIRMA/ortam sorunudur
/// (yönetim istemcisi tanımsız, izin eksik, Keycloak kapalı). Mesaj "ne yapmalı"yı söyler;
/// API 503'e çevirir (SoruCeviriHatasi ile aynı aile).</summary>
public sealed class RolYonetimHatasi(string mesaj, Exception? ic = null) : Exception(mesaj, ic);
