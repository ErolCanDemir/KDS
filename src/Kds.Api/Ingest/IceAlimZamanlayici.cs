using Kds.Api.Identity;
using Kds.Business.Ingest;
using Kds.Contracts.Ingest;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Api.Ingest;

/// <summary>İçe alım zamanlayıcısı (S12 · Faz 2): dakikada bir, TÜM tenant'lardaki aktif+cron'lu
/// işleri tarar; vadesi gelenleri (bkz. <see cref="CronZamanlama"/>) ilgili tenant adına
/// <see cref="RunIngestJobRequest"/> ile çalıştırır. Zamanlama durumu işin kendi satırında
/// (<c>IceAlimIsleri</c>) yaşadığı için ayrı bir job deposu (Hangfire vb.) gerekmez.
///
/// try/catch + log burada meşru — bu bir arka plan işi (AGENTS §4.6 izinli bölge): bir turun/işin
/// hatası tüm zamanlayıcıyı düşürmemeli. İş handler'ı hatayı DOMAIN durumuna zaten yazar; burada
/// yalnız sürdürülebilirlik için yakalanıp loglanır.</summary>
public sealed class IceAlimZamanlayici(IServiceScopeFactory kapsamUreteci, ILogger<IceAlimZamanlayici> gunlukcu)
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
                gunlukcu.LogError(hata, "İçe alım zamanlayıcı turu başarısız oldu.");
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
            // İstek dışıyız → global tenant filtresini atla; tüm tenant'ların işlerini gör.
            // S45: kaynağı AJANLI işler ATLANIR — sunucu o kaynağa bağlanamaz (dize müşteri
            // ağında); onların cron vadesini ajanın kendi yoklaması (AjanIsleriHandler) hesaplar.
            adaylar = await baglam.IceAlimIsleri.IgnoreQueryFilters()
                .Where(x => x.Aktif && x.Zamanlama != null && x.Zamanlama != "")
                .Where(x => !baglam.Baglantilar.IgnoreQueryFilters()
                    .Any(b => b.Id == x.BaglantiId && b.AjanId != null))
                .Select(x => new Aday(x.Id, x.TenantId, x.Zamanlama!, x.SonSenkronTarihi, x.OlusturmaTarihi))
                .ToListAsync(ct);
        }

        var simdi = DateTimeOffset.Now;
        var zon = TimeZoneInfo.Local;
        foreach (var aday in adaylar)
        {
            if (!CronZamanlama.Zamani(aday.Zamanlama, aday.SonSenkron, aday.Olusturma, simdi, zon))
            {
                continue;
            }

            await using var isKapsam = kapsamUreteci.CreateAsyncScope();
            using var _ = AmbientTenant.Baglan(aday.TenantId);   // iş, kendi tenant'ı adına çalışır
            var gonderici = isKapsam.ServiceProvider.GetRequiredService<ISender>();
            try
            {
                var sonuc = await gonderici.Send(new RunIngestJobRequest(aday.Id), ct);
                gunlukcu.LogInformation("Zamanlanmış içe alım {Id}: {Tablo} tablo, {Satir} satır, {Saniye}sn.",
                    aday.Id, sonuc.TabloSayisi, sonuc.ToplamSatir, sonuc.SaniyeSure);
            }
            catch (Exception hata)
            {
                gunlukcu.LogError(hata, "Zamanlanmış içe alım işi {Id} çalışırken hata verdi.", aday.Id);
            }
        }
    }

    private sealed record Aday(Guid Id, Guid TenantId, string Zamanlama, DateTimeOffset? SonSenkron, DateTimeOffset Olusturma);
}
