using FluentValidation;
using FluentValidation.Results;
using Kds.Contracts.Connections;
using Kds.Data;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Connections;

/// <summary>S45 — ajan bağlama doğrulaması (Ekle/Güncelle paylaşır; ConnectionDepoHedefi'nin eşi):
/// yalnız İçe Alım modunda anlamlıdır (diğer modda sessizce null'a düşer); ajan MEVCUT ve iptal
/// edilmemiş olmalıdır. DB'ye bakan kural olduğundan validator yerine handler çağırır.</summary>
internal static class ConnectionAjani
{
    public static async Task<Guid?> DogrulaAsync(
        KdsDbContext context, DataMode mod, Guid? ajanId, CancellationToken cancellationToken)
    {
        if (mod != DataMode.IceAlim || ajanId is not Guid id) return null;
        var ajan = await context.Ajanlar.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw Hata("Seçilen ajan bulunamadı.");
        return ajan.IptalTarihi is null
            ? id
            : throw Hata("Seçilen ajan iptal edilmiş — Yönetim → Ajanlar'dan yenisini üretin.");
    }

    private static ValidationException Hata(string mesaj) =>
        new([new ValidationFailure("ajanId", mesaj)]);
}
