using Kds.Data;
using Kds.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Agents;

/// <summary>S45 — ajan protokol isteklerinin TEK kimlik kapısı: imza → kayıt → iptal. Her handler
/// buradan geçer; kapı kopyalansaydı bir uçta iptal denetimi unutulurdu. Hata mesajı BİLEREK
/// ayrımsız (Gömme'deki gibi): anahtarın var olup olmadığını söylemek numaralandırmaya yardım eder.</summary>
public static class AjanKapisi
{
    /// <summary>Anahtarı doğrular ve ajan kaydını İZLENEN (tracked) olarak döner — çağıran
    /// SonGorulme gibi alanları güncelleyebilsin. Kiracı henüz bağlı değildir; çağıran, dönen
    /// kaydın TenantId'siyle kapsamı kendisi bağlar.</summary>
    public static async Task<Agent> DogrulaAsync(
        KdsDbContext context, IAjanAnahtarKoruyucu koruyucu, string anahtar, CancellationToken ct)
    {
        var ajanId = koruyucu.Coz(anahtar) ?? throw new AjanReddi();

        // Kiracı henüz bilinmiyor (oturum yok) — yalnız ajanın KENDİ satırı filtre dışı okunur.
        var ajan = await context.Ajanlar.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == ajanId, ct)
            ?? throw new AjanReddi();

        return ajan.IptalTarihi is null ? ajan : throw new AjanReddi();
    }
}

public sealed class AjanReddi() : Exception("Bu ajan anahtarı geçersiz ya da iptal edilmiş.");
