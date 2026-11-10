using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Görünür raporlar (S27 rol paylaşımıyla): Ekip paylaşımlılar + kullanıcının kendi
/// Özel'leri + rol kesişimli Roller paylaşımlılar + rol erişimli klasörlerdeki (özel olmayan)
/// raporlar; admin hepsi. Rol kesişimi BELLEKTE yapılır (virgüllü liste SQL'e çevrilemez;
/// kaba süzgeç DB'de — Özel raporlar başkası için hiç inmez). Son güncellenen üstte.</summary>
public sealed class ListReportsHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<ListReportsRequest, IReadOnlyList<ReportSummary>>
{
    public async Task<IReadOnlyList<ReportSummary>> Handle(ListReportsRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken) ?? Guid.Empty;

        var sorgu = context.Raporlar.AsQueryable();
        if (!kb.IsAdmin)
            sorgu = sorgu.Where(r => r.Paylasim != ReportSharing.Ozel || r.SahipId == myId);

        // Rol erişimli klasörler (S27): kesişen roldeki kullanıcı, klasördeki özel olmayan raporları görür.
        var rolluKlasorler = kb.IsAdmin
            ? []
            : (await context.RaporKlasorleri
                .Where(k => k.ErisimRolleri != null)
                .Select(k => new { k.Id, k.ErisimRolleri })
                .ToListAsync(cancellationToken))
                .Where(k => RolKurallari.Kesisir(k.ErisimRolleri, kb.Roller))
                .Select(k => k.Id)
                .ToHashSet();

        // Sıralama/rol süzme bellekte: SQLite (testler) DateTimeOffset ORDER BY çeviremiyor; liste küçük.
        var liste = await sorgu
            .Select(r => new
            {
                r.Id,
                r.Ad,
                r.KlasorId,
                r.Gorsellestirme,
                r.Paylasim,
                r.PaylasimRolleri,
                r.SahipId,
                KlasorAd = context.RaporKlasorleri.Where(k => k.Id == r.KlasorId).Select(k => k.Ad).FirstOrDefault(),
                SahipAd = context.Kullanicilar.Where(u => u.Id == r.SahipId)
                    .Select(u => u.AdSoyad ?? u.KullaniciAdi).FirstOrDefault(),
                FavoriMi = r.Favoriler.Any(f => f.KullaniciId == myId),
                Tarih = r.GuncellemeTarihi ?? r.OlusturmaTarihi,
            })
            .ToListAsync(cancellationToken);

        return [.. liste
            .Where(r => kb.IsAdmin
                || r.SahipId == myId
                || r.Paylasim == ReportSharing.Ekip
                || (r.Paylasim == ReportSharing.Roller && RolKurallari.Kesisir(r.PaylasimRolleri, kb.Roller))
                || (r.Paylasim != ReportSharing.Ozel && r.KlasorId != null && rolluKlasorler.Contains(r.KlasorId.Value)))
            .OrderByDescending(r => r.Tarih)
            .Select(r => new ReportSummary(
                r.Id, r.Ad, r.KlasorId, r.KlasorAd, r.Gorsellestirme, r.Paylasim, r.SahipAd,
                r.SahipId == myId, r.FavoriMi, r.Tarih,
                r.PaylasimRolleri == null ? null : r.PaylasimRolleri.Split(',', StringSplitOptions.RemoveEmptyEntries)))];
    }
}
