using System.Text.Json;
using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Contracts.Queries;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Raporu kimliğiyle çalıştırır (kod incelemesi K1). Görünürlük RAPOR üzerinden denetlenir
/// (Özel → sahibi + admin; Ekip → tenant'taki herkes) — sorgu uçlarının Admin/Geliştirici kilidi
/// bu ucu bağlamaz; <c>musteri</c> paylaşılan raporu koşabilir. İki tasarım türü de desteklenir:
/// ham SQL doğrudan tek kapıya, model üretici zincirine gider (bekçi + sınır + denetim oradan).</summary>
public sealed class RunReportHandler(
    KdsDbContext context, IUserContext kb, ISender gonderici, Security.ISatirKuraliCozucu satirKuralCozucu)
    : IRequestHandler<RunReportRequest, QueryRunResult>
{
    public async Task<QueryRunResult> Handle(RunReportRequest request, CancellationToken cancellationToken)
    {
        var myId = await ReportCommon.MyIdAsync(context, kb, cancellationToken);
        var r = await context.Raporlar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        // S27: görünürlük tek kuraldan (liste ile aynı) — Roller paylaşımı + klasör rol erişimi dahil.
        if (r is null || !await RaporGorunurluk.GorebilirAsync(context, kb, r, myId, cancellationToken))
            throw new KeyNotFoundException("Report bulunamadı.");

        if (r.HamSql is not null && r.BaglantiId is Guid baglantiId)
        {
            // RLS KAPISI: ham SQL raporu modelden geçmez, dolayısıyla satır kuralı UYGULANAMAZ.
            // Kurala tabi bir kullanıcı bu raporu koşarsa kısıtlama sessizce delinirdi — açıkça
            // reddediyoruz. (Kuralsız kullanıcılar ve admin etkilenmez.)
            var kullanici = new Contracts.Identity.CalistiranKullanici(kb.Roller, kb.KullaniciAdi, kb.EPosta, kb.Iddialar);
            if (await satirKuralCozucu.KuralaTabiMiAsync(kullanici, cancellationToken))
                throw new Security.SatirKuraliHatasi(
                    "Bu rapor elle yazılmış SQL ile kurulmuş; satır-düzeyi güvenlik kuralları yalnız " +
                    "tasarımcıyla kurulmuş raporlara uygulanabilir. Kurala tabi kullanıcılar bu raporu " +
                    "çalıştıramaz — raporu tasarımcıyla yeniden kurun ya da bu kullanıcılara kapatın.");

            return await gonderici.Send(new RunQueryRequest(baglantiId, r.HamSql,
                Parametreler: new Dictionary<string, object?>(), SatirSiniri: null,
                RaporId: r.Id), cancellationToken); // M26
        }

        var model = r.ModelJson is null ? null
            : JsonSerializer.Deserialize<Contracts.QueryModel.QueryModel>(r.ModelJson, ReportCommon.JsonAyar);
        if (model is null)
            throw new InvalidOperationException("Report modeli çözülemedi.");
        return await gonderici.Send(
            new Contracts.QueryModel.RunQueryModelRequest(model, request.ParametreDegerleri, RaporId: r.Id), cancellationToken);
    }
}
