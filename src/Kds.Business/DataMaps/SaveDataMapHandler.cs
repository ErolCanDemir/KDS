using Kds.Contracts.DataMaps;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.DataMaps;

/// <summary>Haritayı kaydeder (newEntity/güncelle). Güncellemede eski alt varlıklar silinip taze
/// eklenir; silme ile ekleme aynı transaction'da AYRI SaveChanges'lerde yapılır (istemci tablo
/// Id'leri yeniden eklenebilsin, PK/tracker çakışması olmasın).</summary>
public sealed class SaveDataMapHandler(KdsDbContext context)
    : IRequestHandler<SaveDataMapRequest, DataMapSummary>
{
    public async Task<DataMapSummary> Handle(SaveDataMapRequest request, CancellationToken cancellationToken)
    {
        DataMap dataMap = null!;

        await context.IslemdeCalistirAsync(async ic =>
        {
            if (request.Id is Guid mevcutId)
            {
                dataMap = await context.Haritalar
                    .Include(x => x.Tablolar).ThenInclude(t => t.Kolonlar)
                    .Include(x => x.Iliskiler)
                    .FirstOrDefaultAsync(x => x.Id == mevcutId, ic)
                    ?? throw new KeyNotFoundException("Harita bulunamadı.");

                context.RemoveRange(dataMap.Tablolar.SelectMany(t => t.Kolonlar));
                context.RemoveRange(dataMap.Tablolar);
                context.RemoveRange(dataMap.Iliskiler);
                dataMap.Tablolar.Clear();
                dataMap.Iliskiler.Clear();
                await context.SaveChangesAsync(ic); // silmeler ayrı batch: eski alt varlıkları tracker'dan düşür

                dataMap.Ad = request.Ad.Trim();
                dataMap.BaglantiId = request.BaglantiId;
                dataMap.Mod = request.Mod;

                // Kök TAKİPLİ ama Added DEĞİL. Yeni alt varlıklar istemci-üretimli Guid Id taşır;
                // EF "store-generated key dolu → existing kayıt" varsayımıyla bunları Modified sanıp
                // INSERT yerine UPDATE (→ 0 satır → concurrency) atar. Bu yüzden AÇIKÇA Add'liyoruz.
                foreach (var t in request.Tablolar.Select(d => AltTablo(d, mevcutId)))
                    context.Add(t);
                foreach (var i in request.Iliskiler.Select(d => AltIliski(d, mevcutId)))
                    context.Add(i);
            }
            else
            {
                dataMap = new DataMap
                {
                    Ad = request.Ad.Trim(),
                    BaglantiId = request.BaglantiId,
                    Mod = request.Mod,
                    Tablolar = request.Tablolar.Select(d => AltTablo(d, null)).ToList(),
                    Iliskiler = request.Iliskiler.Select(d => AltIliski(d, null)).ToList(),
                };
                context.Haritalar.Add(dataMap); // kök Added → alt varlıklar da otomatik Added
            }
            // Nihai SaveChanges + commit IslemdeCalistirAsync tarafından yapılır.
        }, cancellationToken);

        return new DataMapSummary(dataMap.Id, dataMap.Ad, dataMap.BaglantiId, null, dataMap.Mod,
            request.Tablolar.Count, request.Iliskiler.Count);
    }

    // haritaId dolu (güncelleme) → FK açıkça verilir; null (newEntity) → navigation fixup'a bırakılır.
    private static DataMapTable AltTablo(DataMapTableDto t, Guid? haritaId) => new()
    {
        Id = t.Id,
        VeriHaritasiId = haritaId ?? Guid.Empty,
        Sema = t.Sema,
        HamAd = t.HamAd,
        DostcaAd = t.DostcaAd,
        Kolonlar = t.Kolonlar.Select(k => new DataMapColumn
        {
            HamAd = k.HamAd,
            DostcaAd = k.DostcaAd,
            Rol = k.Rol,
            VeriTipi = k.VeriTipi,
        }).ToList(),
    };

    private static DataMapRelationship AltIliski(DataMapRelationshipDto i, Guid? haritaId) => new()
    {
        VeriHaritasiId = haritaId ?? Guid.Empty,
        KaynakTabloId = i.KaynakTabloId,
        KaynakKolon = i.KaynakKolon,
        HedefTabloId = i.HedefTabloId,
        HedefKolon = i.HedefKolon,
        FktenMi = i.FktenMi,
    };
}
