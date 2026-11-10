using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kds.Business.Identity;
using Kds.Contracts.Identity;

namespace Kds.Api.Identity;

/// <summary>S27 — Keycloak Admin API adaptörü: roller Keycloak realm rolleridir; bu sınıf
/// <c>kds-yonetim</c> servis hesabıyla (client_credentials) Admin API'ye çıkar.
/// IO adaptörü — try/catch burada meşru (AGENTS §4.6). Teknik hatalar
/// <see cref="RolYonetimHatasi"/>'na çevrilir; mesaj yöneticinin çözebileceği dildedir.</summary>
public sealed class KeycloakRolYonetimi(Kurulum.KurulumDurumu durum, IHttpClientFactory istemciler) : IRolYonetimi
{
    /// <summary>Teknik/dahili roller ekranda gösterilmez (Keycloak'ın kendi mekanizmaları).</summary>
    private static readonly string[] GizliRoller = ["uma_authorization", "offline_access"];

    /// <summary>Ayarlar KurulumDurumu'ndan CANLI okunur (sihirbaz sonrası yeniden başlatma yok) —
    /// KurulumAyarlari DI'da kayıtlı değildir, boot anındaki kopyayı saklamak bayatlatırdı.</summary>
    private Kurulum.KurulumAyarlari kurulum => durum.Ayarlar;

    private static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Keycloak'a GİDEN gövdeler camelCase ister ("Name" alanını tanımaz, HTTP 400 döner).</summary>
    private static readonly JsonSerializerOptions JYaz = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /* --- servis token'ı: kısa süreli önbellek (her istekte token almak gereksiz yük) --- */
    private static string? _token;
    private static DateTimeOffset _tokenSonu = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim TokenKilidi = new(1, 1);

    private (string TokenUcu, string AdminUcu) Ucler()
    {
        // KimlikYetkili: http://host/realms/{realm} → admin ucu: http://host/admin/realms/{realm}
        var yetkili = kurulum.KimlikYetkili.TrimEnd('/');
        var i = yetkili.IndexOf("/realms/", StringComparison.OrdinalIgnoreCase);
        if (i < 0)
        {
            throw new RolYonetimHatasi($"Kimlik yetkilisi beklenen biçimde değil ({yetkili}) — '/realms/<ad>' içermeli.");
        }

        return ($"{yetkili}/protocol/openid-connect/token",
                yetkili[..i] + "/admin" + yetkili[i..]);
    }

    private void YapilandirmaKontrol()
    {
        if (string.IsNullOrWhiteSpace(kurulum.KimlikYonetimIstemci) || string.IsNullOrWhiteSpace(kurulum.KimlikYonetimSir))
        {
            throw new RolYonetimHatasi(
                "Rol yönetimi yapılandırılmamış: Kimlik.YonetimIstemci/YonetimSir boş. Keycloak'ta " +
                "servis hesaplı bir istemci (kds-yonetim) tanımlayıp realm-management izinlerini verin (infra/KURULUM.md).");
        }
    }

    private async Task<string> TokenAlAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenSonu) return _token;
        await TokenKilidi.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _tokenSonu) return _token;
            YapilandirmaKontrol();
            var (tokenUcu, _) = Ucler();
            using var istemci = istemciler.CreateClient(nameof(KeycloakRolYonetimi));
            using var yanit = await istemci.PostAsync(tokenUcu, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = kurulum.KimlikYonetimIstemci,
                ["client_secret"] = kurulum.KimlikYonetimSir,
            }), ct);
            if (!yanit.IsSuccessStatusCode)
            {
                throw new RolYonetimHatasi(
                    $"Rol servisi Keycloak'tan yetki alamadı (HTTP {(int)yanit.StatusCode}). İstemci sırrı yanlış " +
                    "ya da servis hesabı kapalı olabilir (infra/KURULUM.md).");
            }

            using var belge = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync(ct));
            _token = belge.RootElement.GetProperty("access_token").GetString()!;
            var omur = belge.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 60;
            _tokenSonu = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, omur - 30));
            return _token;
        }
        finally
        {
            TokenKilidi.Release();
        }
    }

    private async Task<HttpResponseMessage> IstekAsync(HttpMethod metot, string yol, object? govde, CancellationToken ct)
    {
        var (_, adminUcu) = Ucler();
        var token = await TokenAlAsync(ct);
        using var istemci = istemciler.CreateClient(nameof(KeycloakRolYonetimi));
        var istek = new HttpRequestMessage(metot, adminUcu + yol);
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (govde is not null)
        {
            istek.Content = new StringContent(JsonSerializer.Serialize(govde, JYaz), Encoding.UTF8, "application/json");
        }

        try
        {
            return await istemci.SendAsync(istek, ct);
        }
        catch (HttpRequestException hata)
        {
            throw new RolYonetimHatasi(
                "Kimlik sunucusuna (Keycloak) ulaşılamadı — servis kapalı olabilir. Roller ekranı " +
                "kimlik sunucusu olmadan çalışamaz; ürünün geri kalanı etkilenmez.", hata);
        }
    }

    /* ---------------- IRolYonetimi ---------------- */

    public async Task<IReadOnlyList<RolOzeti>> RolleriListeleAsync(CancellationToken ct)
    {
        using var yanit = await IstekAsync(HttpMethod.Get, "/roles?briefRepresentation=false&max=200", null, ct);
        await BasariBekle(yanit, "roller listelenemedi", ct);
        var roller = await Coz<List<KcRol>>(yanit, ct);

        var sonuc = new List<RolOzeti>();
        foreach (var r in roller.Where(Gorunur))
        {
            // Üye sayısı: ilk 500 (rozet için yeterli; tam sayım Keycloak'ta pahalı).
            using var uyeYanit = await IstekAsync(
                HttpMethod.Get, $"/roles/{Uri.EscapeDataString(r.Name)}/users?briefRepresentation=true&max=500", null, ct);
            var uyeSayisi = 0;
            if (uyeYanit.IsSuccessStatusCode)
            {
                uyeSayisi = (await Coz<List<KcKullanici>>(uyeYanit, ct)).Count;
            }

            sonuc.Add(new RolOzeti(r.Name, r.Description, uyeSayisi, RolKurallari.SistemRolu(r.Name)));
        }

        // Sistem rolleri önce (sabit sırada), sonra özel roller ada göre.
        return [.. sonuc.OrderBy(r => r.Sistem ? RolKurallari.SistemRolleri.ToList().IndexOf(r.Ad) : 99)
                        .ThenBy(r => r.Ad, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<RolOzeti> RolOlusturAsync(string ad, string? aciklama, CancellationToken ct)
    {
        using var yanit = await IstekAsync(HttpMethod.Post, "/roles", new { name = ad, description = aciklama }, ct);
        if (yanit.StatusCode == HttpStatusCode.Conflict)
        {
            // İş kuralı reti (400) — servis arızası (503) değil: kullanıcı adı değiştirip düzeltebilir.
            throw new FluentValidation.ValidationException($"'{ad}' adında bir rol zaten var.");
        }

        await BasariBekle(yanit, "rol oluşturulamadı", ct);
        return new RolOzeti(ad, aciklama, 0, Sistem: false);
    }

    public async Task SilAsync(string ad, CancellationToken ct)
    {
        // Üyesi olan rol silinmez — sessiz yetki düşürme olmasın (iş kuralı; adaptörde çünkü
        // üye bilgisi burada ucuz).
        var uyeler = await UyeleriListeleAsync(ad, ct);
        if (uyeler.Count > 0)
        {
            throw new FluentValidation.ValidationException(
                $"'{ad}' rolünün {uyeler.Count} üyesi var — önce üyeleri çıkarın, sonra silin.");
        }

        using var yanit = await IstekAsync(HttpMethod.Delete, $"/roles/{Uri.EscapeDataString(ad)}", null, ct);
        if (yanit.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"'{ad}' rolü bulunamadı.");
        }

        await BasariBekle(yanit, "rol silinemedi", ct);
    }

    public async Task<IReadOnlyList<RolUyesi>> UyeleriListeleAsync(string rolAdi, CancellationToken ct)
    {
        using var yanit = await IstekAsync(
            HttpMethod.Get, $"/roles/{Uri.EscapeDataString(rolAdi)}/users?max=500", null, ct);
        if (yanit.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"'{rolAdi}' rolü bulunamadı.");
        }

        await BasariBekle(yanit, "rol üyeleri listelenemedi", ct);
        var kullanicilar = await Coz<List<KcKullanici>>(yanit, ct);
        return [.. kullanicilar
            .Where(k => !k.Username.StartsWith("service-account-", StringComparison.OrdinalIgnoreCase))
            .Select(Uye)
            .OrderBy(u => u.KullaniciAdi, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task UyeEkleAsync(string rolAdi, Guid keycloakId, CancellationToken ct)
    {
        var rol = await RolTemsiliAsync(rolAdi, ct);
        using var yanit = await IstekAsync(
            HttpMethod.Post, $"/users/{keycloakId}/role-mappings/realm", new[] { rol }, ct);
        await BasariBekle(yanit, "üye eklenemedi", ct);
    }

    public async Task UyeCikarAsync(string rolAdi, Guid keycloakId, CancellationToken ct)
    {
        var rol = await RolTemsiliAsync(rolAdi, ct);
        var (_, adminUcu) = Ucler();
        var token = await TokenAlAsync(ct);
        using var istemci = istemciler.CreateClient(nameof(KeycloakRolYonetimi));
        var istek = new HttpRequestMessage(HttpMethod.Delete, $"{adminUcu}/users/{keycloakId}/role-mappings/realm")
        {
            Content = new StringContent(JsonSerializer.Serialize(new[] { rol }, JYaz), Encoding.UTF8, "application/json"),
        };
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var yanit = await istemci.SendAsync(istek, ct);
        await BasariBekle(yanit, "üye çıkarılamadı", ct);
    }

    public async Task<IReadOnlyList<RolUyesi>> KullaniciAraAsync(string? arama, Guid tenantId, CancellationToken ct)
    {
        // Kiracı süzgeci: kullanıcı attribute'u tenant_id (girişte token'a yazılan iddia ile aynı).
        var q = Uri.EscapeDataString($"tenant_id:{tenantId}");
        var yol = $"/users?q={q}&briefRepresentation=true&max=50";
        if (!string.IsNullOrWhiteSpace(arama))
        {
            yol += "&search=" + Uri.EscapeDataString(arama);
        }

        using var yanit = await IstekAsync(HttpMethod.Get, yol, null, ct);
        await BasariBekle(yanit, "kullanıcılar aranamadı", ct);
        var kullanicilar = await Coz<List<KcKullanici>>(yanit, ct);
        return [.. kullanicilar
            .Where(k => !k.Username.StartsWith("service-account-", StringComparison.OrdinalIgnoreCase))
            .Select(Uye)
            .OrderBy(u => u.KullaniciAdi, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<RolYonetimDurumu> DurumAsync(CancellationToken ct)
    {
        try
        {
            YapilandirmaKontrol();
            using var yanit = await IstekAsync(HttpMethod.Get, "/roles?max=1", null, ct);
            return yanit.IsSuccessStatusCode
                ? new RolYonetimDurumu(true, null)
                : new RolYonetimDurumu(false,
                    $"Keycloak Admin API beklenmedik cevap verdi (HTTP {(int)yanit.StatusCode}) — " +
                    "servis hesabının realm-management izinlerini denetleyin (infra/KURULUM.md).");
        }
        catch (RolYonetimHatasi hata)
        {
            return new RolYonetimDurumu(false, hata.Message);
        }
    }

    /* ---------------- yardımcılar ---------------- */

    private static bool Gorunur(KcRol r) =>
        !GizliRoller.Contains(r.Name, StringComparer.OrdinalIgnoreCase)
        && !r.Name.StartsWith("default-roles-", StringComparison.OrdinalIgnoreCase);

    private static RolUyesi Uye(KcKullanici k)
    {
        var adSoyad = string.Join(' ', new[] { k.FirstName, k.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return new RolUyesi(k.Id, k.Username, k.Email, string.IsNullOrWhiteSpace(adSoyad) ? null : adSoyad);
    }

    private async Task<KcRol> RolTemsiliAsync(string rolAdi, CancellationToken ct)
    {
        using var yanit = await IstekAsync(HttpMethod.Get, $"/roles/{Uri.EscapeDataString(rolAdi)}", null, ct);
        if (yanit.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"'{rolAdi}' rolü bulunamadı.");
        }

        await BasariBekle(yanit, "rol okunamadı", ct);
        return await Coz<KcRol>(yanit, ct);
    }

    private static async Task BasariBekle(HttpResponseMessage yanit, string eylem, CancellationToken ct)
    {
        if (yanit.IsSuccessStatusCode) return;
        if (yanit.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new RolYonetimHatasi(
                $"Keycloak izin vermedi ({eylem}) — kds-yonetim servis hesabının realm-management " +
                "rolleri eksik olabilir (infra/KURULUM.md).");
        }

        var govde = await yanit.Content.ReadAsStringAsync(ct);
        throw new RolYonetimHatasi($"Keycloak hatası ({eylem}): HTTP {(int)yanit.StatusCode} {govde[..Math.Min(200, govde.Length)]}");
    }

    private static async Task<T> Coz<T>(HttpResponseMessage yanit, CancellationToken ct) =>
        JsonSerializer.Deserialize<T>(await yanit.Content.ReadAsStringAsync(ct), J)
        ?? throw new RolYonetimHatasi("Keycloak boş/çözümlenemeyen yanıt döndürdü.");

    // Id role-mapping için ZORUNLU (Keycloak temsili id+name ile eşler); rol oluştururken null kalır.
    private sealed record KcRol(Guid? Id, string Name, string? Description);

    private sealed record KcKullanici(Guid Id, string Username, string? Email, string? FirstName, string? LastName);
}
