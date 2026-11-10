using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Subscriptions;

/// <summary>Rapor aboneliğini kaydeder (yeni/güncelle). Alıcılar JSON'a yazılır; yeni abonelikte
/// sahip = token'daki kullanıcı (Özel rapor görünürlüğü gibi yumuşak referans).</summary>
public sealed class SaveSubscriptionHandler(KdsDbContext context, IUserContext kb, Security.ISatirKuraliCozucu satirKuralCozucu)
    : IRequestHandler<SaveSubscriptionRequest, SubscriptionSummary>
{
    public async Task<SubscriptionSummary> Handle(SaveSubscriptionRequest request, CancellationToken cancellationToken)
    {
        // Sahiplik kapısı (19 Ağu 2026 — abonelikler müşteri rolüne açıldı): müşteri yalnız KENDİ
        // aboneliğini düzenler ve yalnız erişebildiği bir rapora abone olabilir.
        var benimId = await SubscriptionCommon.BenimIdAsync(context, kb, cancellationToken);
        await SubscriptionCommon.RaporErisimDogrulaAsync(context, kb, benimId, request.RaporId, satirKuralCozucu, cancellationToken);

        ReportSubscription abonelik;
        if (request.Id is Guid mevcutId)
        {
            abonelik = await SubscriptionCommon.ErisilebilirAsync(context, kb, benimId, mevcutId, cancellationToken);
        }
        else
        {
            abonelik = new ReportSubscription { SahipId = benimId ?? Guid.Empty };
            context.Abonelikler.Add(abonelik);
        }

        abonelik.Ad = request.Ad.Trim();
        abonelik.RaporId = request.RaporId;
        abonelik.Bicim = request.Bicim;
        abonelik.AlicilarJson = SubscriptionCommon.AlicilariYaz(request.Alicilar);
        abonelik.ParametrelerJson = SubscriptionCommon.ParametreYaz(request.ParametreDegerleri); // M24
        abonelik.Zamanlama = string.IsNullOrWhiteSpace(request.Zamanlama) ? null : request.Zamanlama.Trim();
        abonelik.Aktif = request.Aktif;
        // Kimlik görüntüsü HER kayıtta tazelenir (bkz. KimlikAnlikGoruntusu).
        abonelik.SahipKimlikJson = Security.KimlikAnlikGoruntusu.Yaz(kb) ?? abonelik.SahipKimlikJson;
        await context.SaveChangesAsync(cancellationToken);

        var raporAd = await context.Raporlar.Where(r => r.Id == abonelik.RaporId)
            .Select(r => r.Ad).FirstOrDefaultAsync(cancellationToken);
        return new SubscriptionSummary(abonelik.Id, abonelik.Ad, abonelik.RaporId, raporAd, abonelik.Bicim,
            request.Alicilar.Count, abonelik.Zamanlama, abonelik.Aktif,
            abonelik.SonGonderimTarihi, abonelik.SonAliciSayisi, abonelik.SonSatirSayisi, abonelik.SonDurum, abonelik.SonHata);
    }
}
