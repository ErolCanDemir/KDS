using Kds.Contracts.Ingest;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Ingest;

/// <summary>Tenant'ın içe alım işlerini listeler (son senkron durumuyla). Ada göre sıralı.</summary>
public sealed class ListIngestJobsHandler(KdsDbContext context)
    : IRequestHandler<ListIngestJobsRequest, IReadOnlyList<IngestJobSummary>>
{
    public async Task<IReadOnlyList<IngestJobSummary>> Handle(ListIngestJobsRequest request, CancellationToken cancellationToken)
    {
        var liste = await context.IceAlimIsleri.Select(j => new
        {
            j.Id, j.Ad, j.BaglantiId, j.TablolarJson, j.Zamanlama, j.Aktif,
            j.SonSenkronTarihi, j.SonSenkronSaniye, j.SonSatirSayisi, j.SonDurum, j.SonHata,
            BaglantiAd = context.Baglantilar.Where(b => b.Id == j.BaglantiId).Select(b => b.Ad).FirstOrDefault(),
        }).ToListAsync(cancellationToken);

        return liste
            .Select(j =>
            {
                var tablolar = IngestCommon.Deserialize(j.TablolarJson);
                return new IngestJobSummary(j.Id, j.Ad, j.BaglantiId, j.BaglantiAd,
                    tablolar.Count, j.Zamanlama, j.Aktif,
                    j.SonSenkronTarihi, j.SonSenkronSaniye, j.SonSatirSayisi, j.SonDurum, j.SonHata,
                    tablolar.Count(t => !string.IsNullOrWhiteSpace(t.ArtimliKolon)));
            })
            .OrderBy(x => x.Ad, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
