using Kds.Api.Identity;
using Kds.Business.Ingest;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Api.Subscriptions;

/// <summary>Rapor aboneliği zamanlayıcısı (S15): dakikada bir, TÜM tenant'lardaki aktif+cron'lu
/// abonelikleri tarar; vadesi gelenleri (bkz. <see cref="CronZamanlama"/>) ilgili tenant adına
/// <see cref="RunSubscriptionRequest"/> ile çalıştırır (rapor→export→e-posta). Zamanlama durumu
/// aboneliğin kendi satırında (<c>RaporAbonelikleri</c>) yaşadığından ayrı job deposu (Hangfire vb.)
/// gerekmez — İçe Alım zamanlayıcısıyla aynı hafif desen.
///
/// try/catch + log burada meşru — arka plan işi (AGENTS §4.6): bir turun/aboneliğin hatası tüm
/// zamanlayıcıyı düşürmemeli. Abonelik handler'ı hatayı DOMAIN durumuna zaten yazar.</summary>
public sealed class AbonelikZamanlayici(IServiceScopeFactory kapsamUreteci, ILogger<AbonelikZamanlayici> gunlukcu)
    : BackgroundService
{
    private static readonly TimeSpan Aralik = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var zamanlayici = new PeriodicTimer(Aralik);
        do
        {
            try
            {
                await TurAtAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception hata)
            {
                gunlukcu.LogError(hata, "Abonelik zamanlayıcı turu başarısız oldu.");
            }
        }
        while (await BekleAsync(zamanlayici, stoppingToken));
    }

    private static async Task<bool> BekleAsync(PeriodicTimer zamanlayici, CancellationToken ct)
    {
        try
        {
            return await zamanlayici.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task TurAtAsync(CancellationToken ct)
    {
        List<Aday> adaylar;
        await using (var kapsam = kapsamUreteci.CreateAsyncScope())
        {
            var baglam = kapsam.ServiceProvider.GetRequiredService<KdsDbContext>();
            // İstek dışıyız → global tenant filtresini atla; tüm tenant'ların aboneliklerini gör.
            adaylar = await baglam.Abonelikler.IgnoreQueryFilters()
                .Where(x => x.Aktif && x.Zamanlama != null && x.Zamanlama != "")
                .Select(x => new Aday(x.Id, x.TenantId, x.Zamanlama!, x.SonGonderimTarihi, x.OlusturmaTarihi))
                .ToListAsync(ct);
        }

        var simdi = DateTimeOffset.Now;
        var zon = TimeZoneInfo.Local;
        foreach (var aday in adaylar)
        {
            if (!CronZamanlama.Zamani(aday.Zamanlama, aday.SonGonderim, aday.Olusturma, simdi, zon))
            {
                continue;
            }

            await using var isKapsam = kapsamUreteci.CreateAsyncScope();
            using var _ = AmbientTenant.Baglan(aday.TenantId);   // abonelik, kendi tenant'ı adına çalışır
            var gonderici = isKapsam.ServiceProvider.GetRequiredService<ISender>();
            try
            {
                var sonuc = await gonderici.Send(new RunSubscriptionRequest(aday.Id), ct);
                gunlukcu.LogInformation("Zamanlanmış abonelik {Id}: {Alici} alıcı, {Satir} satır, {Saniye}sn.",
                    aday.Id, sonuc.AliciSayisi, sonuc.SatirSayisi, sonuc.SaniyeSure);
            }
            catch (Exception hata)
            {
                gunlukcu.LogError(hata, "Zamanlanmış abonelik {Id} gönderilirken hata verdi.", aday.Id);
            }
        }
    }

    private sealed record Aday(Guid Id, Guid TenantId, string Zamanlama, DateTimeOffset? SonGonderim, DateTimeOffset Olusturma);
}
