using Kds.Contracts.DataMaps;
using Kds.Data;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.DataMaps;

/// <summary>EF üzerinden dataMap detayını kurar — <see cref="GetDataMapHandler"/> ile sorgu
/// handler'larının paylaştığı TEK okuma yolu.</summary>
public sealed class DataMapReader(KdsDbContext context) : IDataMapReader
{
    public async Task<DataMapDetail?> GetDetailAsync(Guid haritaId, CancellationToken cancellationToken)
    {
        var h = await context.Haritalar
            .Include(x => x.Tablolar).ThenInclude(t => t.Kolonlar)
            .Include(x => x.Iliskiler)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == haritaId, cancellationToken);
        if (h is null) return null;

        return new DataMapDetail(h.Id, h.Ad, h.BaglantiId, h.Mod,
            h.Tablolar.Select(t => new DataMapTableDto(t.Id, t.Sema, t.HamAd, t.DostcaAd,
                t.Kolonlar.Select(k => new DataMapColumnDto(k.HamAd, k.DostcaAd, k.Rol, k.VeriTipi)).ToList())).ToList(),
            h.Iliskiler.Select(i => new DataMapRelationshipDto(i.KaynakTabloId, i.KaynakKolon, i.HedefTabloId, i.HedefKolon, i.FktenMi)).ToList());
    }
}
