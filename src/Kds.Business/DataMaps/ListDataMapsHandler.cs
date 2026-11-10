using Kds.Contracts.DataMaps;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.DataMaps;

public sealed class ListDataMapsHandler(KdsDbContext context)
    : IRequestHandler<ListDataMapsRequest, IReadOnlyList<DataMapSummary>>
{
    public async Task<IReadOnlyList<DataMapSummary>> Handle(ListDataMapsRequest request, CancellationToken cancellationToken)
    {
        // M21: son eklenen/güncellenen başa (ada göre alfabetik sıralama kafa karıştırıyordu).
        // Sıralama BELLEKTE — SQLite (testler) DateTimeOffset ORDER BY çeviremiyor; liste küçük.
        var liste = await context.Haritalar
            .Select(h => new
            {
                Ozet = new DataMapSummary(
                    h.Id,
                    h.Ad,
                    h.BaglantiId,
                    context.Baglantilar.Where(b => b.Id == h.BaglantiId).Select(b => b.Ad).FirstOrDefault(),
                    h.Mod,
                    h.Tablolar.Count,
                    h.Iliskiler.Count),
                Zaman = h.GuncellemeTarihi ?? h.OlusturmaTarihi,
            })
            .ToListAsync(cancellationToken);
        return liste.OrderByDescending(x => x.Zaman).Select(x => x.Ozet).ToList();
    }
}
