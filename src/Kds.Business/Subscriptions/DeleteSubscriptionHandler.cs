using Kds.Business.Identity;
using Kds.Contracts.Subscriptions;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Subscriptions;

/// <summary>Aboneliği siler. Yoksa false → 404.</summary>
public sealed class DeleteSubscriptionHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<DeleteSubscriptionRequest, bool>
{
    public async Task<bool> Handle(DeleteSubscriptionRequest request, CancellationToken cancellationToken)
    {
        // Sahiplik kapısı: müşteri yalnız KENDİ aboneliğini siler.
        var benimId = await SubscriptionCommon.BenimIdAsync(context, kb, cancellationToken);
        var a = await SubscriptionCommon.Gorunur(context.Abonelikler, kb, benimId)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (a is null)
            return false;
        context.Abonelikler.Remove(a);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
