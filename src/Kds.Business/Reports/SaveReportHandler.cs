using System.Text.Json;
using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Raporu kaydeder. Yeni: sahip = existing kullanıcı. Güncelleme: yalnız görünür rapor
/// (Özel'i sahibi + admin) — görünmeze "bulunamadı" davranılır (varlık sızdırmaz). SQL raporu
/// (HamSql) kayıtta da SELECT-only bekçisinden geçer — reddedilen SQL hiç saklanmaz.</summary>
public sealed class SaveReportHandler(KdsDbContext context, IUserContext kb, Execution.IQueryGuard bekci)
    : IRequestHandler<SaveReportRequest, ReportSummary>
{
    public async Task<ReportSummary> Handle(SaveReportRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı kaydı bulunamadı.");

        var sqlRaporu = !string.IsNullOrWhiteSpace(request.HamSql);
        if (sqlRaporu)
        {
            if (!await context.Baglantilar.AnyAsync(b => b.Id == request.BaglantiId, cancellationToken))
                throw new KeyNotFoundException("Raporun bağlantısı bulunamadı.");
            var karar = bekci.Inspect(request.HamSql!);
            if (!karar.Gecerli)
                throw new Execution.QuerySecurityException(karar.Sebep!);
        }
        else if (!await context.Haritalar.AnyAsync(h => h.Id == request.Model!.HaritaId, cancellationToken))
        {
            throw new KeyNotFoundException("Modelin Veri Haritası bulunamadı.");
        }
        if (request.KlasorId is Guid kId && !await context.RaporKlasorleri.AnyAsync(k => k.Id == kId, cancellationToken))
            throw new KeyNotFoundException("Klasör bulunamadı.");

        Report rapor;
        if (request.Id is Guid mevcutId)
        {
            rapor = await context.Raporlar.FirstOrDefaultAsync(r => r.Id == mevcutId, cancellationToken)
                ?? throw new KeyNotFoundException("Report bulunamadı.");
            if (rapor.Paylasim == ReportSharing.Ozel && rapor.SahipId != myId && !kb.IsAdmin)
                throw new KeyNotFoundException("Report bulunamadı.");
        }
        else
        {
            rapor = new Report { SahipId = myId };
            context.Raporlar.Add(rapor);
        }

        rapor.Ad = request.Ad.Trim();
        rapor.KlasorId = request.KlasorId;
        rapor.HaritaId = sqlRaporu ? null : request.Model!.HaritaId;
        rapor.ModelJson = sqlRaporu ? null : JsonSerializer.Serialize(request.Model, ReportCommon.JsonAyar);
        rapor.HamSql = sqlRaporu ? request.HamSql!.Trim() : null;
        rapor.BaglantiId = sqlRaporu ? request.BaglantiId : null;
        rapor.Gorsellestirme = request.Gorsellestirme;
        rapor.Paylasim = request.Paylasim;
        // S27: rol listesi yalnız Roller paylaşımında anlamlı; diğerlerinde temizlenir (bayat
        // liste "Ekip'e çevirip geri Roller'a dönünce eski roller hortladı" sürprizi yaratmasın).
        rapor.PaylasimRolleri = request.Paylasim == ReportSharing.Roller
            ? RolKurallari.Normallestir(request.PaylasimRolleri)
              ?? throw new FluentValidation.ValidationException("\"Belirli roller\" paylaşımında en az bir rol seçilmelidir.")
            : null;
        await context.SaveChangesAsync(cancellationToken);

        var klasorAd = rapor.KlasorId is Guid kid2
            ? await context.RaporKlasorleri.Where(k => k.Id == kid2).Select(k => k.Ad).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new ReportSummary(rapor.Id, rapor.Ad, rapor.KlasorId, klasorAd, rapor.Gorsellestirme,
            rapor.Paylasim, null, rapor.SahipId == myId, false, rapor.GuncellemeTarihi ?? rapor.OlusturmaTarihi,
            rapor.PaylasimRolleri?.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }
}
