using Kds.Business.Identity;
using Kds.Contracts.Security;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Security;

/// <summary>RLS kurallarını listeler. Kural yönetimi YALNIZ ADMİNdir (uçta da kilitli): kuralı
/// görebilen, hangi verinin kimden saklandığını da görür.</summary>
public sealed class ListSatirKurallariHandler(KdsDbContext context)
    : IRequestHandler<ListSatirKurallariRequest, IReadOnlyList<SatirKuraliDto>>
{
    public async Task<IReadOnlyList<SatirKuraliDto>> Handle(
        ListSatirKurallariRequest request, CancellationToken cancellationToken)
    {
        // Harita ve tablo ADLARI da dönüyor ki ekran ham kimlik göstermek zorunda kalmasın.
        var sorgu =
            from k in context.SatirKurallari.AsNoTracking()
            join h in context.Haritalar.AsNoTracking() on k.VeriHaritasiId equals h.Id
            join t in context.Set<DataMapTable>().AsNoTracking() on k.HaritaTabloId equals t.Id
            where request.VeriHaritasiId == null || k.VeriHaritasiId == request.VeriHaritasiId
            orderby h.Ad, t.DostcaAd, k.Ad
            select new SatirKuraliDto(
                k.Id, k.VeriHaritasiId, h.Ad, k.HaritaTabloId, t.DostcaAd,
                k.Ad, k.Kolon, k.Op, k.DegerKaynagi, k.Deger, k.Roller, k.Aktif);
        return await sorgu.ToListAsync(cancellationToken);
    }
}

/// <summary>Kuralı ekler/günceller. Doğrulama BURADA yapılır: hedef tablo gerçekten o haritaya mı
/// ait, kolon haritada var mı? Yanlış kural, çalıştırma anında sorguyu tamamen durdurur
/// (fail-closed) — hatayı kuralı yazarken görmek çok daha ucuzdur.</summary>
public sealed class SaveSatirKuraliHandler(KdsDbContext context)
    : IRequestHandler<SaveSatirKuraliRequest, SatirKuraliDto>
{
    public async Task<SatirKuraliDto> Handle(SaveSatirKuraliRequest request, CancellationToken cancellationToken)
    {
        var harita = await context.Haritalar.AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == request.VeriHaritasiId, cancellationToken)
            ?? throw new KeyNotFoundException("Veri haritası bulunamadı.");

        var tablo = await context.Set<DataMapTable>().AsNoTracking()
            .Include(t => t.Kolonlar)
            .FirstOrDefaultAsync(t => t.Id == request.HaritaTabloId, cancellationToken)
            ?? throw new KeyNotFoundException("Harita tablosu bulunamadı.");
        if (tablo.VeriHaritasiId != harita.Id)
            throw new InvalidOperationException("Seçilen tablo bu haritaya ait değil.");
        if (!tablo.Kolonlar.Any(k => k.HamAd == request.Kolon))
            throw new InvalidOperationException(
                $"'{request.Kolon}' kolonu '{tablo.DostcaAd}' tablosunda yok — kural çalıştırma anında sorguyu durdururdu.");
        if (string.IsNullOrWhiteSpace(request.Ad))
            throw new InvalidOperationException("Kurala bir ad verin (hata mesajlarında bu ad görünür).");

        RowRule kural;
        if (request.Id is Guid mevcut)
        {
            kural = await context.SatirKurallari.FirstOrDefaultAsync(k => k.Id == mevcut, cancellationToken)
                ?? throw new KeyNotFoundException("Kural bulunamadı.");
        }
        else
        {
            kural = new RowRule();
            context.SatirKurallari.Add(kural);
        }

        kural.VeriHaritasiId = request.VeriHaritasiId;
        kural.HaritaTabloId = request.HaritaTabloId;
        kural.Ad = request.Ad.Trim();
        kural.Kolon = request.Kolon;
        kural.Op = request.Op;
        kural.DegerKaynagi = request.DegerKaynagi;
        kural.Deger = request.Deger?.Trim();
        kural.Roller = request.Roller?.Trim();
        kural.Aktif = request.Aktif;
        await context.SaveChangesAsync(cancellationToken);

        return new SatirKuraliDto(kural.Id, kural.VeriHaritasiId, harita.Ad, kural.HaritaTabloId, tablo.DostcaAd,
            kural.Ad, kural.Kolon, kural.Op, kural.DegerKaynagi, kural.Deger, kural.Roller, kural.Aktif);
    }
}

public sealed class DeleteSatirKuraliHandler(KdsDbContext context)
    : IRequestHandler<DeleteSatirKuraliRequest, bool>
{
    public async Task<bool> Handle(DeleteSatirKuraliRequest request, CancellationToken cancellationToken)
    {
        var kural = await context.SatirKurallari.FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);
        if (kural is null) return false;
        context.SatirKurallari.Remove(kural);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
