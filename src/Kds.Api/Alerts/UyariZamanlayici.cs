using Kds.Api.Identity;
using Kds.Business.Ingest;
using Kds.Contracts.Alerts;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Api.Alerts;

/// <summary>Eşik uyarısı zamanlayıcısı (S20): dakikada bir, TÜM tenant'lardaki aktif+cron'lu
/// uyarıları tarar; vadesi gelenleri ilgili tenant adına değerlendirir. Abonelik/İçe Alım
/// zamanlayıcılarıyla aynı hafif desen — durum uyarının kendi satırında yaşadığı için ayrı job
/// deposu (Hangfire vb.) gerekmez.
///
/// <para>Vade tabanı <c>SonKontrolTarihi</c>'dir (son BİLDİRİM değil): uyarı her planlı anda
/// ölçülmeli, bildirimi gönderip göndermeme kararı ayrı bir şeydir (yalnız durum değişince).</para>
///
/// try/catch + log burada meşru — arka plan işi (AGENTS §4.6): bir uyarının hatası tüm
/// zamanlayıcıyı düşürmemeli. Handler hatayı DOMAIN durumuna zaten yazar.</summary>
public sealed class UyariZamanlayici(IServiceScopeFactory kapsamUreteci, ILogger<UyariZamanlayici> gunlukcu)
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
                gunlukcu.LogError(hata, "Uyarı zamanlayıcı turu başarısız oldu.");
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
            // İstek dışıyız → global tenant filtresini atla; tüm tenant'ların uyarılarını gör.
            adaylar = await baglam.Uyarilar.IgnoreQueryFilters()
                .Where(x => x.Aktif && x.Zamanlama != null && x.Zamanlama != "")
                .Select(x => new Aday(x.Id, x.TenantId, x.Zamanlama!, x.SonKontrolTarihi, x.OlusturmaTarihi))
                .ToListAsync(ct);
        }

        var simdi = DateTimeOffset.Now;
        var zon = TimeZoneInfo.Local;
        foreach (var aday in adaylar)
        {
            if (!CronZamanlama.Zamani(aday.Zamanlama, aday.SonKontrol, aday.Olusturma, simdi, zon))
            {
                continue;
            }

            await using var isKapsam = kapsamUreteci.CreateAsyncScope();
            using var _ = AmbientTenant.Baglan(aday.TenantId);   // uyarı kendi tenant'ı adına koşar
            var gonderici = isKapsam.ServiceProvider.GetRequiredService<ISender>();
            try
            {
                var sonuc = await gonderici.Send(new RunAlertRequest(aday.Id, BildirimGonder: true), ct);
                if (sonuc.DurumDegisti)
                {
                    gunlukcu.LogInformation(
                        "Uyarı {Id} durum değiştirdi: {Durum} (değer {Deger}); bildirim {Bildirim}.",
                        aday.Id, sonuc.Tetiklendi ? "TETİKLENDİ" : "sakin", sonuc.Deger,
                        sonuc.BildirimGonderildi ? "gönderildi" : "gönderilmedi");
                }
            }
            catch (Exception hata)
            {
                gunlukcu.LogError(hata, "Zamanlanmış uyarı {Id} değerlendirilirken hata verdi.", aday.Id);
            }
        }
    }

    private sealed record Aday(Guid Id, Guid TenantId, string Zamanlama, DateTimeOffset? SonKontrol, DateTimeOffset Olusturma);
}
