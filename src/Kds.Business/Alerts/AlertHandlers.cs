using Kds.Business.Identity;
using Kds.Contracts.Alerts;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Alerts;

/// <summary>Uyarı listesi. Müşteri yalnız KENDİ uyarılarını görür (sahiplik kapısı); admin ve
/// geliştirici kiracının tamamını. Rapor adı yumuşak referanstan çözülür.</summary>
public sealed class ListAlertsHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<ListAlertsRequest, IReadOnlyList<AlertSummary>>
{
    public async Task<IReadOnlyList<AlertSummary>> Handle(ListAlertsRequest request, CancellationToken cancellationToken)
    {
        var benimId = await AlertCommon.BenimIdAsync(context, kb, cancellationToken);
        var satirlar = await AlertCommon.Gorunur(context.Uyarilar, kb, benimId)
            .Select(u => new
            {
                Uyari = u,
                RaporAd = context.Raporlar.Where(r => r.Id == u.RaporId).Select(r => r.Ad).FirstOrDefault(),
                MetrikAd = context.Metrikler.Where(m => m.Id == u.MetrikId).Select(m => m.Ad).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return satirlar
            .Select(x =>
            {
                var u = x.Uyari;
                return new AlertSummary(u.Id, u.Ad, u.RaporId, x.RaporAd, u.Kolon,
                    AlertCommon.OzetCoz(u.Ozet), AlertCommon.OpCoz(u.Op), u.Esik,
                    AlertCommon.AlicilariCoz(u.AlicilarJson).Count, u.Zamanlama, u.Aktif, u.SurekliBildir,
                    AlertCommon.DurumCoz(u), u.SonDeger, u.SonKontrolTarihi, u.SonBildirimTarihi, u.SonHata,
                    BenimMi: u.SahipId == benimId, MetrikId: u.MetrikId, MetrikAd: x.MetrikAd);
            })
            // Tetiklenenler ÜSTTE: ekranı açan kişinin ilk görmesi gereken şey ilgi bekleyen uyarıdır.
            .OrderByDescending(s => s.Durum == UyariDurumu.Tetiklendi)
            .ThenByDescending(s => s.SonKontrolTarihi ?? DateTimeOffset.MinValue)
            .ThenBy(s => s.Ad)
            .ToList();
    }
}

public sealed class GetAlertHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<GetAlertRequest, AlertDetail>
{
    public async Task<AlertDetail> Handle(GetAlertRequest request, CancellationToken cancellationToken)
    {
        var benimId = await AlertCommon.BenimIdAsync(context, kb, cancellationToken);
        var u = await AlertCommon.ErisilebilirAsync(context, kb, benimId, request.Id, cancellationToken);
        return new AlertDetail(u.Id, u.Ad, u.RaporId, u.Kolon, AlertCommon.OzetCoz(u.Ozet),
            AlertCommon.OpCoz(u.Op), u.Esik, AlertCommon.AlicilariCoz(u.AlicilarJson), u.Zamanlama,
            u.Aktif, u.SurekliBildir, AlertCommon.ParametreCoz(u.ParametrelerJson), u.MetrikId);
    }
}

/// <summary>Uyarıyı kaydeder. Eşik/kolon/rapor değişirse <b>durum sıfırlanır</b>: eski karar yeni
/// kurala ait değildir, taşınırsa ilk koşumda ya yanlış "toparlandı" ya da sessiz kalma olur.</summary>
public sealed class SaveAlertHandler(KdsDbContext context, IUserContext kb, Security.ISatirKuraliCozucu satirKuralCozucu)
    : IRequestHandler<SaveAlertRequest, AlertSummary>
{
    public async Task<AlertSummary> Handle(SaveAlertRequest request, CancellationToken cancellationToken)
    {
        var benimId = await AlertCommon.BenimIdAsync(context, kb, cancellationToken);
        if (request.RaporId is Guid raporId)
        {
            await AlertCommon.RaporErisimDogrulaAsync(context, kb, benimId, raporId, satirKuralCozucu, cancellationToken);
        }
        else
        {
            await AlertCommon.MetrikErisimDogrulaAsync(context, kb, request.MetrikId!.Value, satirKuralCozucu, cancellationToken);
        }

        Alert uyari;
        if (request.Id is Guid mevcutId)
        {
            uyari = await AlertCommon.ErisilebilirAsync(context, kb, benimId, mevcutId, cancellationToken);
            var kuralDegisti = uyari.RaporId != request.RaporId
                || uyari.MetrikId != request.MetrikId
                || uyari.Kolon != request.Kolon
                || uyari.Ozet != request.Ozet.ToString()
                || uyari.Op != request.Op.ToString()
                || uyari.Esik != request.Esik;
            if (kuralDegisti)
            {
                DurumSifirla(uyari);
            }
        }
        else
        {
            uyari = new Alert { SahipId = benimId ?? Guid.Empty };
            context.Uyarilar.Add(uyari);
        }

        uyari.Ad = request.Ad.Trim();
        uyari.RaporId = request.RaporId;
        uyari.MetrikId = request.MetrikId;
        // Metrik uyarısında kolon anlamsız — kalıntı değer koşumda kafa karıştırmasın diye silinir.
        uyari.Kolon = request.MetrikId is not null || string.IsNullOrWhiteSpace(request.Kolon)
            ? null : request.Kolon.Trim();
        uyari.Ozet = request.Ozet.ToString();
        uyari.Op = request.Op.ToString();
        uyari.Esik = request.Esik;
        uyari.AlicilarJson = AlertCommon.AlicilariYaz(request.Alicilar);
        uyari.ParametrelerJson = AlertCommon.ParametreYaz(request.ParametreDegerleri);
        uyari.Zamanlama = string.IsNullOrWhiteSpace(request.Zamanlama) ? null : request.Zamanlama.Trim();
        uyari.Aktif = request.Aktif;
        uyari.SurekliBildir = request.SurekliBildir;
        // Kimlik görüntüsü HER kayıtta tazelenir: rol/nitelik değişmişse iş güncel kimlikle koşsun.
        uyari.SahipKimlikJson = Security.KimlikAnlikGoruntusu.Yaz(kb) ?? uyari.SahipKimlikJson;
        await context.SaveChangesAsync(cancellationToken);

        var raporAd = await context.Raporlar.Where(r => r.Id == uyari.RaporId)
            .Select(r => r.Ad).FirstOrDefaultAsync(cancellationToken);
        var metrikAd = await context.Metrikler.Where(m => m.Id == uyari.MetrikId)
            .Select(m => m.Ad).FirstOrDefaultAsync(cancellationToken);
        // BenimMi GERÇEK sahipliği söyler: admin başkasının uyarısını da düzenleyebilir; sabit
        // "true" dönmek listedeki "Başkasının" rozetiyle çelişiyordu (bağımsız inceleme bulgusu).
        return new AlertSummary(uyari.Id, uyari.Ad, uyari.RaporId, raporAd, uyari.Kolon,
            request.Ozet, request.Op, uyari.Esik, request.Alicilar.Count, uyari.Zamanlama, uyari.Aktif,
            uyari.SurekliBildir, AlertCommon.DurumCoz(uyari), uyari.SonDeger, uyari.SonKontrolTarihi,
            uyari.SonBildirimTarihi, uyari.SonHata, BenimMi: uyari.SahipId == benimId,
            MetrikId: uyari.MetrikId, MetrikAd: metrikAd);
    }

    private static void DurumSifirla(Alert u)
    {
        u.Tetiklendi = false;
        u.SonDurum = null;
        u.SonDeger = null;
        u.SonKontrolTarihi = null;
        u.SonHata = null;
    }
}

public sealed class DeleteAlertHandler(KdsDbContext context, IUserContext kb)
    : IRequestHandler<DeleteAlertRequest, bool>
{
    public async Task<bool> Handle(DeleteAlertRequest request, CancellationToken cancellationToken)
    {
        var benimId = await AlertCommon.BenimIdAsync(context, kb, cancellationToken);
        var uyari = await AlertCommon.Gorunur(context.Uyarilar, kb, benimId)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);
        if (uyari is null)
        {
            return false;
        }

        context.Uyarilar.Remove(uyari);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
