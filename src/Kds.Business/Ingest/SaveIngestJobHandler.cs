using Kds.Contracts.Ingest;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Ingest;

/// <summary>İçe alım işini kaydeder (newEntity/güncelle). Seçili tablolar JSON'a yazılır.</summary>
public sealed class SaveIngestJobHandler(KdsDbContext context)
    : IRequestHandler<SaveIngestJobRequest, IngestJobSummary>
{
    public async Task<IngestJobSummary> Handle(SaveIngestJobRequest request, CancellationToken cancellationToken)
    {
        IngestJob job;
        if (request.Id is Guid mevcutId)
        {
            job = await context.IceAlimIsleri.FirstOrDefaultAsync(x => x.Id == mevcutId, cancellationToken)
                ?? throw new KeyNotFoundException("İçe alım işi bulunamadı.");
        }
        else
        {
            job = new IngestJob();
            context.IceAlimIsleri.Add(job);
        }

        // S44: artımlı KOLONU değişen tablonun su seviyesi düşürülür — eski kolonun değeriyle yeni
        // kolonu karşılaştırmak satırları SESSİZCE atlatabilirdi. Kolonu aynı kalan tablolar
        // seviyesini korur (kaydetme, artımlı ilerlemeyi bozmaz).
        var eskiKolonlar = IngestCommon.Deserialize(job.TablolarJson)
            .ToDictionary(t => $"{t.Sema}.{t.Tablo}", t => t.ArtimliKolon);
        var seviyeler = IngestCommon.DeserializeSuSeviyeleri(job.ArtimliDurumJson);
        foreach (var t in request.Tablolar)
        {
            var anahtar = $"{t.Sema}.{t.Tablo}";
            if (!eskiKolonlar.TryGetValue(anahtar, out var eskiKolon)
                || !string.Equals(eskiKolon, t.ArtimliKolon, StringComparison.OrdinalIgnoreCase))
                seviyeler.Remove(anahtar);
        }

        job.ArtimliDurumJson = IngestCommon.SerializeSuSeviyeleri(seviyeler);

        job.Ad = request.Ad.Trim();
        job.BaglantiId = request.BaglantiId;
        job.TablolarJson = IngestCommon.Serialize(request.Tablolar);
        job.Zamanlama = string.IsNullOrWhiteSpace(request.Zamanlama) ? null : request.Zamanlama.Trim();
        job.Aktif = request.Aktif;
        await context.SaveChangesAsync(cancellationToken);

        var baglantiAd = await context.Baglantilar.Where(b => b.Id == job.BaglantiId)
            .Select(b => b.Ad).FirstOrDefaultAsync(cancellationToken);
        return new IngestJobSummary(job.Id, job.Ad, job.BaglantiId, baglantiAd, request.Tablolar.Count,
            job.Zamanlama, job.Aktif, job.SonSenkronTarihi, job.SonSenkronSaniye, job.SonSatirSayisi,
            job.SonDurum, job.SonHata,
            request.Tablolar.Count(t => !string.IsNullOrWhiteSpace(t.ArtimliKolon)));
    }
}
