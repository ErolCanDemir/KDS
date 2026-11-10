using Kds.Business.Identity;
using Kds.Business.Reports;
using Kds.Contracts.Dashboards;
using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Dashboards;

/// <summary>Panoyu kaydeder. Yeni: sahip = existing kullanıcı. Güncelleme: yalnız sahibi + admin —
/// görünmeze "bulunamadı" davranılır (varlık sızdırmaz). Rapor id'leri sırayla JSON'a yazılır.</summary>
public sealed class SaveDashboardHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<SaveDashboardRequest, DashboardSummary>
{
    public async Task<DashboardSummary> Handle(SaveDashboardRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı kaydı bulunamadı.");

        Dashboard pano;
        if (request.Id is Guid mevcutId)
        {
            pano = await context.Panolar.FirstOrDefaultAsync(p => p.Id == mevcutId, cancellationToken)
                ?? throw new KeyNotFoundException("Pano bulunamadı.");
            if (pano.SahipId != myId && !kb.IsAdmin)
                throw new KeyNotFoundException("Pano bulunamadı.");
        }
        else
        {
            pano = new Dashboard { SahipId = myId };
            context.Panolar.Add(pano);
        }

        pano.Ad = request.Ad.Trim();
        pano.Paylasim = request.Paylasim;
        // S27: rol listesi yalnız Roller paylaşımında; diğerlerinde temizlenir (rapor tarafıyla aynı kural).
        pano.PaylasimRolleri = request.Paylasim == ReportSharing.Roller
            ? Identity.RolKurallari.Normallestir(request.PaylasimRolleri)
              ?? throw new FluentValidation.ValidationException("\"Belirli roller\" paylaşımında en az bir rol seçilmelidir.")
            : null;
        pano.RaporlarJson = DashboardCommon.SerializeReports(request.Raporlar);
        // Yerlesim yalnız gönderildiğinde güncellenir; null → mevcut yerleşim korunur (editör raporu
        // değiştirir ama sürükle-bırak yerleşimini silmez).
        if (request.Yerlesim is not null)
            pano.YerlesimJson = DashboardCommon.SerializeLayout(request.Yerlesim);
        await context.SaveChangesAsync(cancellationToken);

        return new DashboardSummary(pano.Id, pano.Ad, pano.Paylasim, request.Raporlar.Count,
            null, pano.SahipId == myId, pano.GuncellemeTarihi ?? pano.OlusturmaTarihi,
            pano.PaylasimRolleri?.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }
}
