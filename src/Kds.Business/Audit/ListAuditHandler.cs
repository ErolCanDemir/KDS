using Kds.Contracts.Audit;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Audit;

/// <summary>Denetim kayıtlarını filtreli + sayfalı döndürür (en yeni önce). Tenant izolasyonu global
/// query filter'dan gelir. Kullanıcı/bağlantı ADLARI ilişkili alt-sorguyla (yumuşak ref; silinmişse null)
/// çözülür — denetim satırı "kim/hangi bağlantı"yı okunur gösterir.</summary>
public sealed class ListAuditHandler(KdsDbContext context)
    : IRequestHandler<ListAuditRequest, AuditPage>
{
    public async Task<AuditPage> Handle(ListAuditRequest request, CancellationToken cancellationToken)
    {
        var boyut = Math.Clamp(request.SayfaBoyut, 1, 200);
        var sayfa = Math.Max(0, request.Sayfa);

        var q = context.DenetimKayitlari.AsNoTracking();
        if (request.Basarili is bool b)
        {
            q = q.Where(x => x.Basarili == b);
        }
        if (!string.IsNullOrWhiteSpace(request.Arama))
        {
            var ara = request.Arama.Trim();
            q = q.Where(x => x.Sql.Contains(ara));
        }
        if (request.Baslangic is { } bt)
        {
            q = q.Where(x => x.OlusturmaTarihi >= bt);
        }
        if (request.Bitis is { } et)
        {
            q = q.Where(x => x.OlusturmaTarihi <= et);
        }

        var toplam = await q.CountAsync(cancellationToken);

        var kayitlar = await q
            .OrderByDescending(x => x.OlusturmaTarihi)
            .Skip(sayfa * boyut).Take(boyut)
            .Select(x => new AuditRecordDto(
                x.Id,
                x.OlusturmaTarihi,
                context.Kullanicilar.Where(k => k.Id == x.KullaniciId).Select(k => k.KullaniciAdi).FirstOrDefault(),
                context.Baglantilar.Where(bg => bg.Id == x.BaglantiId).Select(bg => bg.Ad).FirstOrDefault(),
                x.Sql,
                x.ParametrelerJson,
                x.Basarili,
                x.SatirSayisi,
                x.SureMs,
                x.Hata,
                context.Raporlar.Where(r => r.Id == x.RaporId).Select(r => r.Ad).FirstOrDefault())) // M26
            .ToListAsync(cancellationToken);

        return new AuditPage(toplam, kayitlar);
    }
}
