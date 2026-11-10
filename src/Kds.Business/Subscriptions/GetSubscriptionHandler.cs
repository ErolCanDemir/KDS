using Kds.Business.Identity;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Subscriptions;

/// <summary>Tek aboneliğin editör içeriği (alıcılar JSON'dan listeye çözülür). Yoksa null → 404.</summary>
public sealed class GetSubscriptionHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<GetSubscriptionRequest, SubscriptionDetail?>
{
    public async Task<SubscriptionDetail?> Handle(GetSubscriptionRequest request, CancellationToken cancellationToken)
    {
        // Sahiplik kapısı: müşteri başkasının aboneliğini AÇAMAZ (alıcı listesi de veridir).
        var benimId = await SubscriptionCommon.BenimIdAsync(context, kb, cancellationToken);
        var a = await SubscriptionCommon.Gorunur(context.Abonelikler.AsNoTracking(), kb, benimId)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        return a is null ? null
            : new SubscriptionDetail(a.Id, a.Ad, a.RaporId, a.Bicim,
                SubscriptionCommon.AlicilariCoz(a.AlicilarJson), a.Zamanlama, a.Aktif,
                SubscriptionCommon.ParametreCoz(a.ParametrelerJson));
    }
}
