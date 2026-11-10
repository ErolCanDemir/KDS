using System.Diagnostics;
using Kds.Business.DataMaps;
using Kds.Contracts.Ingest;
using Kds.Data;
using Kds.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Ingest;

/// <summary>İçe alım işini ŞİMDİ çalıştırır. Elle tetik (Faz 1) ve zamanlayıcı (Faz 2 ·
/// <c>IceAlimZamanlayici</c>) AYNI handler'ı ISender ile çağırır — tek doğruluk kaynağı; zamanlayıcı
/// tenant'ı ambient bağlar. Kaynak dizesini çözer, depo sunucusunda tenant DB'sine tam yükleme yaptırır,
/// işe son senkron durumunu yazar. try/catch burada meşru — bu bir JOB icrası (AGENTS §4.6 izinli
/// bölge): hata DOMAIN durumuna yazılır ve YENİDEN fırlatılır (yutulmaz; global handler + olay
/// günlüğü görür). _logger kullanılmaz.</summary>
public sealed class RunIngestJobHandler(
    KdsDbContext context, ConnectionStringResolver dizeCozucu, IIngestRunner kosucu,
    ITenantContext tenant, DepoCozucu depo)
    : IRequestHandler<RunIngestJobRequest, IngestRunResult>
{
    public async Task<IngestRunResult> Handle(RunIngestJobRequest request, CancellationToken cancellationToken)
    {
        var job = await context.IceAlimIsleri.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("İçe alım işi bulunamadı.");

        var kaynakDizesi = await dizeCozucu.ResolveAsync(job.BaglantiId, cancellationToken);
        var tablolar = IngestCommon.Deserialize(job.TablolarJson)
            .Select(t => new IngestTableSpec(t.Sema, t.Tablo, t.Kolonlar, t.ArtimliKolon)).ToList();

        // S44: su seviyeleri işin satırında yaşar (zamanlama durumuyla aynı ilke — ayrı depo yok).
        // "Tam Yükle" seviyeleri SIFIRLAR: her tablo baştan yüklenir, artımlının taşımadığı
        // silinmiş satırlar da böyle temizlenir.
        var suSeviyeleri = request.TamYukleme
            ? new Dictionary<string, string>()
            : IngestCommon.DeserializeSuSeviyeleri(job.ArtimliDurumJson);

        // M22b: kaynak bağlantıda depo HEDEFİ seçiliyse çekilenler o (Canlı) bağlantının
        // veritabanına yazılır (DB hazır kabul edilir); yoksa varsayılan tenant deposu.
        var kaynakBaglanti = await context.Baglantilar.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == job.BaglantiId, cancellationToken);
        var hedefBaglantiId = kaynakBaglanti?.DepoBaglantiId;

        /* S45: kaynak AJANLI ise sunucu bu kaynağa DOĞRUDAN BAĞLANAMAZ (bağlantı dizesi müşteri
         * ağında, ajanın yerel yapılandırmasında). "Şimdi Çalıştır" burada koşmaz; elle tetik
         * damgalanır ve ajan bir sonraki yoklamasında işi alır. Sonuç bunu AÇIKÇA söyler —
         * sessizce 0 satır dönmek "çalıştı ama boş" diye okunurdu. */
        if (kaynakBaglanti?.AjanId is not null)
        {
            job.AjanTetikTarihi = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return new IngestRunResult(0, 0, 0, 0, AjanaDevredildi: true);
        }
        // S16/A3: depo sunucusu ve DB adı deseni Sistem Ayarları'ndan (yoksa kurulumdan).
        var depoDb = depo.DbAdi(tenant.CurrentTenantId ?? Guid.Empty);

        var kronometre = Stopwatch.StartNew();
        try
        {
            var sonuc = hedefBaglantiId is Guid hedefId
                ? await kosucu.RunHedefeAsync(kaynakDizesi,
                    await dizeCozucu.ResolveAsync(hedefId, cancellationToken), tablolar, suSeviyeleri, cancellationToken)
                : await kosucu.RunAsync(kaynakDizesi, depo.SunucuDizesi(), depoDb, tablolar, suSeviyeleri, cancellationToken);
            kronometre.Stop();
            DurumYaz(job, kronometre, "Basarili", sonuc.ToplamSatir, null);
            // Yeni su seviyeleri yalnız BAŞARILI koşuda yazılır: yarıda kalan koşunun seviyesini
            // ilerletmek, kopyalanamayan dilimi bir daha hiç istememek demek olurdu.
            job.ArtimliDurumJson = IngestCommon.SerializeSuSeviyeleri(sonuc.YeniSuSeviyeleri);
            await context.SaveChangesAsync(cancellationToken);
            return new IngestRunResult(sonuc.TabloSayisi, sonuc.ToplamSatir,
                (int)kronometre.Elapsed.TotalSeconds, sonuc.ArtimliTabloSayisi);
        }
        catch (Exception hata)
        {
            kronometre.Stop();
            DurumYaz(job, kronometre, "Hata", null, hata.Message);
            // Hata durumu İPTAL EDİLMEYEN token'la yazılır: iş iptal/zaman aşımı yüzünden düştüyse
            // (istemci vazgeçti, uygulama kapanıyor) buradaki kaydı da iptal edilmiş token'la yapmak
            // hatayı SESSİZCE yutardı — iş "hiç koşmamış" görünür, en çok kaydedilmesi gereken
            // başarısızlık hiç kaydedilmezdi.
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static void DurumYaz(Kds.Data.Entities.IngestJob job, Stopwatch k, string durum, long? satir, string? hata)
    {
        job.SonSenkronTarihi = DateTimeOffset.Now;
        job.SonSenkronSaniye = (int)k.Elapsed.TotalSeconds;
        job.SonSatirSayisi = satir;
        job.SonDurum = durum;
        job.SonHata = hata;
    }
}
