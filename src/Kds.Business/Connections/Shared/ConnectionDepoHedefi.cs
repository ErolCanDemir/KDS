using FluentValidation;
using FluentValidation.Results;
using Kds.Contracts.Connections;
using Kds.Data;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Connections;

/// <summary>M22b — depo hedefi doğrulama (Ekle/Güncelle paylaşır): yalnız İçe Alım modunda
/// anlamlıdır (diğer modda sessizce null'a düşer); hedef MEVCUT, CANLI modlu ve bağlantının
/// kendisi olmayan bir bağlantı olmalıdır. DB'ye bakan kural olduğundan validator yerine
/// handler çağırır; ihlal ValidationException ile 400'e düşer.</summary>
internal static class ConnectionDepoHedefi
{
    public static async Task<Guid?> DogrulaAsync(
        KdsDbContext context, DataMode mod, Guid? depoBaglantiId, Guid? kendiId, CancellationToken cancellationToken)
    {
        if (mod != DataMode.IceAlim || depoBaglantiId is not Guid hedefId) return null;
        if (kendiId == hedefId)
            throw Hata("Depo hedefi bağlantının kendisi olamaz.");
        var hedef = await context.Baglantilar.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == hedefId, cancellationToken)
            ?? throw Hata("Depo hedef bağlantısı bulunamadı.");
        if (hedef.Mod != DataMode.Canli)
            throw Hata("Depo hedefi CANLI modlu bir bağlantı olmalı (İçe Alım modlu hedef seçilemez).");
        return hedefId;
    }

    private static ValidationException Hata(string mesaj) =>
        new([new ValidationFailure("depoBaglantiId", mesaj)]);
}
