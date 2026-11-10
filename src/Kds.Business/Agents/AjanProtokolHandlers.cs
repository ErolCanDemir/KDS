using Kds.Business.DataMaps;
using Kds.Business.Embedding;
using Kds.Business.Ingest;
using Kds.Contracts.Agents;
using Kds.Data;
using Kds.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Agents;

/* S45 — AJAN PROTOKOLÜ SUNUCU TARAFI. Her handler aynı zincirden geçer:
 * anahtar (AjanKapisi) → kiracı kapsamı (IKiraciKapsami; gömmedeki desen — oturum yok, kiracı
 * ancak anahtar çözülünce bilinir) → işin GERÇEKTEN bu ajanın bağlantısına ait olduğu denetimi
 * (aynı kiracının başka ajanı başka kaynağın işine yazamasın). */

/// <summary>Ortak: iş + ajan eşleşmesini doğrular ve işin depo dizesini çözer (M22b hedef
/// bağlantı → onun dizesi; yoksa varsayılan tenant deposu, yoksa yaratılır).</summary>
internal static class AjanIsOrtak
{
    public static async Task<Data.Entities.IngestJob> IsiDogrulaAsync(
        KdsDbContext context, Guid isId, Guid ajanId, CancellationToken ct)
    {
        var job = await context.IceAlimIsleri.FirstOrDefaultAsync(x => x.Id == isId, ct)
            ?? throw new KeyNotFoundException("İçe alım işi bulunamadı.");
        var ajanli = await context.Baglantilar.AsNoTracking()
            .AnyAsync(b => b.Id == job.BaglantiId && b.AjanId == ajanId, ct);
        return ajanli ? job : throw new AjanReddi();
    }

    public static async Task<string> DepoDizesiCozAsync(
        KdsDbContext context, ConnectionStringResolver dizeCozucu, DepoCozucu depo,
        IAjanDepoYazici depoYazici, Guid baglantiId, Guid tenantId, CancellationToken ct)
    {
        var kaynak = await context.Baglantilar.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == baglantiId, ct);
        if (kaynak?.DepoBaglantiId is Guid hedefId)
        {
            return await dizeCozucu.ResolveAsync(hedefId, ct);
        }

        var db = depo.DbAdi(tenantId);
        await depoYazici.DepoyuHazirlaAsync(depo.SunucuDizesi(), db, ct);
        return SqlConnectionString.VeritabaniIle(depo.SunucuDizesi(), db);
    }
}

/// <summary>Ajanın koşacağı işleri verir: elle tetiklenmiş (AjanTetikTarihi) ya da cron vadesi
/// gelmiş olanlar. Verilen iş "Kosuyor" işaretlenir ve SonSenkronTarihi ilerletilir — aynı iş bir
/// sonraki yoklamada TEKRAR verilmez (çifte koşum). Ajan iş ortasında ölürse durum "Kosuyor"da
/// kalır ve bir SONRAKİ cron vadesinde yeniden dağıtılır (kendi kendini onarır).
/// Yoklama "son görülme"yi de günceller — ekran çevrimiçi çipini bundan çizer.</summary>
public sealed class AjanIsleriHandler(
    KdsDbContext context, IAjanAnahtarKoruyucu koruyucu, IKiraciKapsami kiraciKapsami)
    : IRequestHandler<AjanIsleriRequest, AjanIsleriResult>
{
    public async Task<AjanIsleriResult> Handle(AjanIsleriRequest request, CancellationToken cancellationToken)
    {
        var ajan = await AjanKapisi.DogrulaAsync(context, koruyucu, request.Anahtar, cancellationToken);
        ajan.SonGorulme = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        using var _ = kiraciKapsami.Baglan(ajan.TenantId);

        var baglantiIdler = await context.Baglantilar
            .Where(b => b.AjanId == ajan.Id)
            .Select(b => b.Id).ToListAsync(cancellationToken);
        var isler = await context.IceAlimIsleri
            .Where(x => x.Aktif && baglantiIdler.Contains(x.BaglantiId))
            .ToListAsync(cancellationToken);

        var simdi = DateTimeOffset.Now;
        var zon = TimeZoneInfo.Local;
        var verilecek = isler.Where(x =>
                x.AjanTetikTarihi != null
                || CronZamanlama.Zamani(x.Zamanlama, x.SonSenkronTarihi, x.OlusturmaTarihi, simdi, zon))
            .ToList();

        var sonuc = new List<AjanIsDto>();
        foreach (var job in verilecek)
        {
            job.SonDurum = "Kosuyor";
            job.SonSenkronTarihi = simdi;   // cron vadesini ileri iter → çifte dağıtım olmaz
            job.AjanTetikTarihi = null;     // elle tetik DAĞITIMDA düşer — yoksa ikinci yoklama aynı işi yine verirdi
            var seviyeler = IngestCommon.DeserializeSuSeviyeleri(job.ArtimliDurumJson);
            sonuc.Add(new AjanIsDto(job.Id, job.Ad, job.BaglantiId,
                [.. IngestCommon.Deserialize(job.TablolarJson).Select(t => new AjanTabloDto(
                    t.Sema, t.Tablo, t.Kolonlar, t.ArtimliKolon,
                    seviyeler.TryGetValue($"{t.Sema}.{t.Tablo}", out var s) ? s : null))]));
        }

        if (verilecek.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return new AjanIsleriResult(ajan.Ad, sonuc);
    }
}

public sealed class AjanTabloBaslaHandler(
    KdsDbContext context, IAjanAnahtarKoruyucu koruyucu, IKiraciKapsami kiraciKapsami,
    ConnectionStringResolver dizeCozucu, DepoCozucu depo, IAjanDepoYazici depoYazici)
    : IRequestHandler<AjanTabloBaslaRequest, AjanTabloBaslaResult>
{
    public async Task<AjanTabloBaslaResult> Handle(AjanTabloBaslaRequest request, CancellationToken cancellationToken)
    {
        var ajan = await AjanKapisi.DogrulaAsync(context, koruyucu, request.Anahtar, cancellationToken);
        using var _ = kiraciKapsami.Baglan(ajan.TenantId);
        var job = await AjanIsOrtak.IsiDogrulaAsync(context, request.IsId, ajan.Id, cancellationToken);

        var depoDizesi = await AjanIsOrtak.DepoDizesiCozAsync(
            context, dizeCozucu, depo, depoYazici, job.BaglantiId, ajan.TenantId, cancellationToken);
        var mod = await depoYazici.TabloBaslaAsync(depoDizesi, request.Sema, request.Tablo,
            [.. request.Kolonlar.Select(Esle)], request.Mod == "Artimli" ? "Artimli" : "Tam", cancellationToken);
        return new AjanTabloBaslaResult(mod);
    }

    internal static DepoKolon Esle(AjanKolonDto k) =>
        new(k.Ad, k.Tip, k.Boyut, k.Hassasiyet, k.Olcek, k.NullOlabilir);
}

public sealed class AjanSatirlarHandler(
    KdsDbContext context, IAjanAnahtarKoruyucu koruyucu, IKiraciKapsami kiraciKapsami,
    ConnectionStringResolver dizeCozucu, DepoCozucu depo, IAjanDepoYazici depoYazici)
    : IRequestHandler<AjanSatirlarRequest, bool>
{
    public async Task<bool> Handle(AjanSatirlarRequest request, CancellationToken cancellationToken)
    {
        var ajan = await AjanKapisi.DogrulaAsync(context, koruyucu, request.Anahtar, cancellationToken);
        using var _ = kiraciKapsami.Baglan(ajan.TenantId);
        var job = await AjanIsOrtak.IsiDogrulaAsync(context, request.IsId, ajan.Id, cancellationToken);

        // JSON → CLR çevirisi bildirilen kolon tipine göre (AjanVeri — ajan tarafındaki yazımla
        // aynı harita). Çözülemeyen değer partiyi reddeder; koşu HATA olarak görünür.
        var satirlar = new List<object?[]>(request.Satirlar.Count);
        foreach (var satir in request.Satirlar)
        {
            var hedef = new object?[request.Kolonlar.Count];
            for (var i = 0; i < request.Kolonlar.Count && i < satir.Count; i++)
            {
                hedef[i] = AjanVeri.Coz(request.Kolonlar[i].Tip, satir[i]);
            }

            satirlar.Add(hedef);
        }

        var depoDizesi = await AjanIsOrtak.DepoDizesiCozAsync(
            context, dizeCozucu, depo, depoYazici, job.BaglantiId, ajan.TenantId, cancellationToken);
        var hedefTablo = request.Hedef == "Staging" ? $"{request.Tablo}__kds_delta" : request.Tablo;
        await depoYazici.SatirlarYazAsync(depoDizesi, request.Sema, hedefTablo,
            [.. request.Kolonlar.Select(AjanTabloBaslaHandler.Esle)], satirlar, cancellationToken);
        return true;
    }
}

public sealed class AjanTabloBitirHandler(
    KdsDbContext context, IAjanAnahtarKoruyucu koruyucu, IKiraciKapsami kiraciKapsami,
    ConnectionStringResolver dizeCozucu, DepoCozucu depo, IAjanDepoYazici depoYazici)
    : IRequestHandler<AjanTabloBitirRequest, AjanTabloBitirResult>
{
    public async Task<AjanTabloBitirResult> Handle(AjanTabloBitirRequest request, CancellationToken cancellationToken)
    {
        var ajan = await AjanKapisi.DogrulaAsync(context, koruyucu, request.Anahtar, cancellationToken);
        using var _ = kiraciKapsami.Baglan(ajan.TenantId);
        var job = await AjanIsOrtak.IsiDogrulaAsync(context, request.IsId, ajan.Id, cancellationToken);

        var depoDizesi = await AjanIsOrtak.DepoDizesiCozAsync(
            context, dizeCozucu, depo, depoYazici, job.BaglantiId, ajan.TenantId, cancellationToken);
        var satir = await depoYazici.TabloBitirAsync(depoDizesi, request.Sema, request.Tablo,
            request.Mod == "Artimli" ? "Artimli" : "Tam",
            [.. request.Kolonlar.Select(AjanTabloBaslaHandler.Esle)], request.Pk, cancellationToken);

        /* Su seviyesi TABLO BAŞINA burada yazılır — verisi gerçekten depoda olan tablonun
         * ilerlemesi, işin geri kalanı hata alsa bile korunur. TAM yüklenen tablo da seviyesini
         * alır (S44 kuralı: bir sonraki koşu artımlı olabilsin). */
        if (!string.IsNullOrWhiteSpace(request.YeniSuSeviyesi))
        {
            var seviyeler = IngestCommon.DeserializeSuSeviyeleri(job.ArtimliDurumJson);
            seviyeler[$"{request.Sema}.{request.Tablo}"] = request.YeniSuSeviyesi;
            job.ArtimliDurumJson = IngestCommon.SerializeSuSeviyeleri(seviyeler);
            await context.SaveChangesAsync(cancellationToken);
        }

        return new AjanTabloBitirResult(satir);
    }
}

public sealed class AjanIsBitirHandler(
    KdsDbContext context, IAjanAnahtarKoruyucu koruyucu, IKiraciKapsami kiraciKapsami)
    : IRequestHandler<AjanIsBitirRequest, bool>
{
    public async Task<bool> Handle(AjanIsBitirRequest request, CancellationToken cancellationToken)
    {
        var ajan = await AjanKapisi.DogrulaAsync(context, koruyucu, request.Anahtar, cancellationToken);
        using var _ = kiraciKapsami.Baglan(ajan.TenantId);
        var job = await AjanIsOrtak.IsiDogrulaAsync(context, request.IsId, ajan.Id, cancellationToken);

        job.SonSenkronTarihi = DateTimeOffset.Now;
        job.SonSenkronSaniye = request.Saniye;
        job.SonSatirSayisi = request.Durum == "Basarili" ? request.ToplamSatir : null;
        job.SonDurum = request.Durum == "Basarili" ? "Basarili" : "Hata";
        job.SonHata = request.Durum == "Basarili" ? null : (request.Hata ?? "Ajan hata bildirdi.");
        job.AjanTetikTarihi = null;   // elle tetik karşılandı
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
