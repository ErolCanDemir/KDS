using Kds.Business.Identity;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Subscriptions;

/// <summary>Tenant'ın rapor abonelikleri (özet + son gönderim durumu). Rapor adı yumuşak referanstan
/// (alt-sorgu) çözülür; en son güncellenen üstte. Alıcı sayısı JSON'dan bellekte hesaplanır.</summary>
public sealed class ListSubscriptionsHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<ListSubscriptionsRequest, IReadOnlyList<SubscriptionSummary>>
{
    public async Task<IReadOnlyList<SubscriptionSummary>> Handle(ListSubscriptionsRequest request, CancellationToken cancellationToken)
    {
        var benimId = await SubscriptionCommon.BenimIdAsync(context, kb, cancellationToken);
        var satirlar = await SubscriptionCommon.Gorunur(context.Abonelikler, kb, benimId)
            .Select(a => new
            {
                Abonelik = a,
                RaporAd = context.Raporlar.Where(r => r.Id == a.RaporId).Select(r => r.Ad).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return satirlar
            .Select(x =>
            {
                var a = x.Abonelik;
                return new SubscriptionSummary(a.Id, a.Ad, a.RaporId, x.RaporAd, a.Bicim,
                    SubscriptionCommon.AlicilariCoz(a.AlicilarJson).Count, a.Zamanlama, a.Aktif,
                    a.SonGonderimTarihi, a.SonAliciSayisi, a.SonSatirSayisi, a.SonDurum, a.SonHata);
            })
            .OrderByDescending(s => s.SonGonderimTarihi ?? DateTimeOffset.MinValue)
            .ThenBy(s => s.Ad)
            .ToList();
    }
}
