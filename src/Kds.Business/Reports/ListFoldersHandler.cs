using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Klasörler + içlerindeki GÖRÜNÜR rapor sayıları (ad sırasıyla). S27: rol erişimli
/// klasörde, kesişen roldeki kullanıcı özel olmayan raporları da sayar; rol kesişimi bellekte.</summary>
public sealed class ListFoldersHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<ListFoldersRequest, IReadOnlyList<FolderDto>>
{
    public async Task<IReadOnlyList<FolderDto>> Handle(ListFoldersRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken) ?? Guid.Empty;
        var admin = kb.IsAdmin;

        var klasorler = await context.RaporKlasorleri
            .OrderBy(k => k.Ad)
            .Select(k => new
            {
                k.Id,
                k.Ad,
                k.ErisimRolleri,
                // Kaba sayım parçaları DB'de; rol-bazlı kesin sayım bellekte birleşir.
                EkipVeyaBenim = context.Raporlar.Count(r => r.KlasorId == k.Id &&
                    (admin || r.Paylasim == ReportSharing.Ekip || r.SahipId == myId)),
                RolPaylasimlilar = context.Raporlar
                    .Where(r => r.KlasorId == k.Id && r.Paylasim == ReportSharing.Roller && r.SahipId != myId)
                    .Select(r => r.PaylasimRolleri)
                    .ToList(),
                OzelOlmayanToplam = context.Raporlar.Count(r => r.KlasorId == k.Id && r.Paylasim != ReportSharing.Ozel),
                BenimOzel = context.Raporlar.Count(r => r.KlasorId == k.Id && r.Paylasim == ReportSharing.Ozel && r.SahipId == myId),
            })
            .ToListAsync(cancellationToken);

        return [.. klasorler.Select(k =>
        {
            int sayi;
            if (admin)
            {
                sayi = k.EkipVeyaBenim;
            }
            else if (RolKurallari.Kesisir(k.ErisimRolleri, kb.Roller))
            {
                // Klasöre rol erişimi: özel olmayan hepsi + kendi özellerim.
                sayi = k.OzelOlmayanToplam + k.BenimOzel;
            }
            else
            {
                sayi = k.EkipVeyaBenim
                    + k.RolPaylasimlilar.Count(roller => RolKurallari.Kesisir(roller, kb.Roller));
            }

            return new FolderDto(k.Id, k.Ad, sayi,
                k.ErisimRolleri == null ? null : k.ErisimRolleri.Split(',', StringSplitOptions.RemoveEmptyEntries));
        })];
    }
}
