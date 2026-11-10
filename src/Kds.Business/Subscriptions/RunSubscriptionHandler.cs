using System.Diagnostics;
using System.Text.Json;
using Kds.Business.Reports;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using Kds.Export;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Subscriptions;

/// <summary>Aboneliği ŞİMDİ çalıştırır: raporu koşar → seçilen biçime çevirir → alıcılara e-postayla
/// gönderir → son gönderim durumunu yazar. Elle "Şimdi Gönder" ucu ve zamanlayıcı (AbonelikZamanlayici)
/// AYNI handler'ı çağırır (tek doğruluk kaynağı; zamanlayıcı tenant'ı ambient bağlar).
///
/// Raporu <b>görünürlük kapısı olmadan</b> koşar: abonelik zaten Admin/Geliştirici tarafından, kendi
/// tenant'ında kuruldu → RunReport'un kullanıcı-bağlamlı Özel/Ekip denetimi (arka planda kullanıcı yok)
/// burada uygulanmaz; rapor tenant-filtreli yüklenir, ardından RunReportHandler ile aynı çalıştırma
/// zinciri (bekçi + sınır + denetim) kullanılır. Parametreli raporlar varsayılan değerleriyle koşar.
///
/// try/catch burada meşru — bu bir JOB icrası (AGENTS §4.6 izinli bölge): hata DOMAIN durumuna
/// (SonDurum="Hata") yazılır ve YENİDEN fırlatılır (yutulmaz). _logger kullanılmaz.</summary>
public sealed class RunSubscriptionHandler(
    KdsDbContext context, ISender gonderici, IResultExporter disaAktarici, IEpostaGonderici epostaGonderici,
    Security.ISatirKuraliCozucu satirKuralCozucu)
    : IRequestHandler<RunSubscriptionRequest, SubscriptionRunResult>
{
    public async Task<SubscriptionRunResult> Handle(RunSubscriptionRequest request, CancellationToken cancellationToken)
    {
        // NOT: burada SAHİPLİK kapısı yok ve olmamalı — zamanlayıcı bu handler'ı kullanıcı bağlamı
        // olmadan çağırır. Elle "Şimdi Gönder" yolundaki sahiplik kapısı controller'da uygulanır.
        var abonelik = await context.Abonelikler.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Abonelik bulunamadı.");

        // RLS kapısı ise BURADA olmalı: veriye bakar, kimliğe değil. Kaydetme anındaki kontrol
        // yalnız erken uyarıdır; kapı eklenmeden önce kurulmuş abonelikleri, sonradan eklenen
        // kuralları ve "admin kurar, kurala tabi kullanıcı alıcıdır" durumunu ancak bu kapsar.
        var kosanKimlik = Security.KimlikAnlikGoruntusu.Oku(abonelik.SahipKimlikJson);
        await Security.ZamanlanmisIsKapisi.DogrulaAsync(
            context, satirKuralCozucu, abonelik.RaporId, "rapor aboneliği", kosanKimlik, cancellationToken);

        var kronometre = Stopwatch.StartNew();
        try
        {
            // M24: abonelikte saklanan parametre değerleriyle koş — zorunlu parametreli rapor
            // artık "değer girin" hatasıyla düşmez ("$bugun" ucu üretici tarafında güne çözülür).
            var sonuc = await RaporuKosAsync(abonelik.RaporId,
                SubscriptionCommon.ParametreCoz(abonelik.ParametrelerJson), kosanKimlik, cancellationToken);
            var dosya = disaAktarici.DisaAktar(sonuc, SubscriptionCommon.BicimCoz(abonelik.Bicim), abonelik.Ad);
            var alicilar = SubscriptionCommon.AlicilariCoz(abonelik.AlicilarJson);
            var gonderildi = await epostaGonderici.GonderAsync(
                new EpostaMesaji(alicilar, $"Rapor: {abonelik.Ad}", GovdeKur(abonelik.Ad, sonuc.Satirlar.Count),
                    new EpostaEki(dosya.Icerik, dosya.DosyaAdi, dosya.IcerikTipi)),
                cancellationToken);
            kronometre.Stop();

            DurumYaz(abonelik, "Basarili", alicilar.Count, sonuc.Satirlar.Count, null);
            await context.SaveChangesAsync(cancellationToken);
            return new SubscriptionRunResult(
                alicilar.Count, sonuc.Satirlar.Count, (int)kronometre.Elapsed.TotalSeconds, dosya.DosyaAdi, gonderildi);
        }
        catch (Exception hata)
        {
            kronometre.Stop();
            DurumYaz(abonelik, "Hata", null, null, hata.Message);
            // Hata durumu İPTAL EDİLMEYEN token'la yazılır — eşi RunIngestJobHandler'da: iptal edilmiş
            // token'la kaydetmek, gönderim iptal/zaman aşımıyla düştüğünde hatayı sessizce yutar ve
            // abonelik "hiç gönderilmemiş" görünürdü (18 Ağu 2026 SMTP doğrulamasında bu yaşandı).
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>Raporu kimliğiyle koşar (RunReportHandler'ın iç dallanmasının görünürlük-kapısı olmayan
    /// eşi): ham SQL doğrudan tek kapıya, model üretici zincirine gider (bekçi + sınır + denetim oradan).</summary>
    private async Task<QueryRunResult> RaporuKosAsync(
        Guid raporId, IReadOnlyDictionary<string, object?>? parametreDegerleri,
        Kds.Contracts.Identity.CalistiranKullanici? kosanKimlik, CancellationToken cancellationToken)
    {
        var r = await context.Raporlar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == raporId, cancellationToken)
            ?? throw new InvalidOperationException("Abonelikteki rapor bulunamadı (silinmiş olabilir).");

        if (r.HamSql is not null && r.BaglantiId is Guid baglantiId)
            return await gonderici.Send(new RunQueryRequest(baglantiId, r.HamSql,
                SatirSiniri: null, Parametreler: new Dictionary<string, object?>(),
                RaporId: r.Id), cancellationToken); // M26

        var model = r.ModelJson is null ? null
            : JsonSerializer.Deserialize<QueryModel>(r.ModelJson, ReportCommon.JsonAyar);
        if (model is null)
            throw new InvalidOperationException("Rapor modeli çözülemedi.");
        // RLS, işin SAHİBİNİN kimliğine uygulanır (arka planda istek bağlamı yok).
        return await gonderici.Send(
            new RunQueryModelRequest(model, parametreDegerleri, RaporId: r.Id, KosanKimlik: kosanKimlik), cancellationToken);
    }

    private static string GovdeKur(string raporAd, int satirSayisi) =>
        $"""
         <p>Merhaba,</p>
         <p><b>{System.Net.WebUtility.HtmlEncode(raporAd)}</b> raporunuzun güncel çıktısı ekte ({satirSayisi:N0} satır).</p>
         <p style="color:#67727e;font-size:12px">Bu e-posta LST-KDS zamanlanmış rapor aboneliğiyle otomatik gönderildi.</p>
         """;

    private static void DurumYaz(Kds.Data.Entities.ReportSubscription a, string durum, int? alici, long? satir, string? hata)
    {
        a.SonGonderimTarihi = DateTimeOffset.Now;
        a.SonAliciSayisi = alici;
        a.SonSatirSayisi = satir;
        a.SonDurum = durum;
        a.SonHata = hata;
    }
}
