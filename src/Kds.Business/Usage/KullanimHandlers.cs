using Kds.Business.Settings;
using Kds.Contracts.Usage;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Usage;

/// <summary>S41 — Yönetim → Kullanım. Kaynak: S5'te zaten yazılan DENETİM KAYITLARI; yeni bir ölçüm
/// altyapısı kurulmaz. Tenant izolasyonu global query filter'dan gelir.
///
/// <para>"Yavaş" eşiği burada TANIMLANMAZ, Sistem Ayarları'ndan okunur — aynı eşik ekranda
/// "Sorun ne?" düğmesini de açar; iki yerde iki farklı sayı tutmak, birinin diğerini sessizce
/// yalanlaması demektir (S35'te limitlerde verilen kararın aynısı).</para></summary>
public sealed class GetUsageHandler(KdsDbContext context, ISistemAyarlari ayarlar)
    : IRequestHandler<GetUsageRequest, UsageResult>
{
    /// <summary>Listelerde gösterilecek en fazla satır — ekran "en kötüler"i sorar, tam dökümü
    /// Denetim ekranı verir.</summary>
    private const int ListeSiniri = 20;

    public async Task<UsageResult> Handle(GetUsageRequest request, CancellationToken cancellationToken)
    {
        var gun = Math.Clamp(request.GunSayisi, 1, 365);
        var baslangic = DateTimeOffset.UtcNow.AddDays(-gun);
        var yavasEsik = ayarlar.Sayi(AyarTanimlari.TaniYavasEsikMs);

        var kayitlar = context.DenetimKayitlari.AsNoTracking().Where(k => k.OlusturmaTarihi >= baslangic);

        /* ÖZET — sayaçlar AYRI sorgularla alınır. Tek turda `GroupBy(_ => 1)` denendi; SQL Server'da
         * çalışıyor ama SQLite'a çevrilemiyor ve testler bu sağlayıcıyla koşuyor. Bölmek burada
         * bedava sayılır: hepsi toplama sorgusu, satır çekilmiyor ve ekran yönetim ekranı (sık
         * çağrılmaz). Ortalama/en uzun BOŞ kümede patlar — kayıt yoksa hiç sorulmaz. */
        var toplam = await kayitlar.CountAsync(cancellationToken);
        var basarisiz = await kayitlar.CountAsync(k => !k.Basarili, cancellationToken);
        var yavasSayi = await kayitlar.CountAsync(k => k.SureMs >= yavasEsik, cancellationToken);
        var ortalama = toplam == 0 ? 0 : (int)await kayitlar.AverageAsync(k => (double)k.SureMs, cancellationToken);
        var enUzun = toplam == 0 ? 0 : await kayitlar.MaxAsync(k => k.SureMs, cancellationToken);

        var aktifKullanici = await kayitlar
            .Where(k => k.KullaniciId != null)
            .Select(k => k.KullaniciId)
            .Distinct()
            .CountAsync(cancellationToken);

        var ozet = new UsageOzet(toplam, basarisiz, yavasSayi, yavasEsik, ortalama, enUzun, aktifKullanici);

        // YAVAŞ KOŞUMLAR — rapor ve kullanıcı adları yumuşak referansla çözülür (silinmişse null).
        var yavaslar = await kayitlar
            .Where(k => k.SureMs >= yavasEsik)
            .OrderByDescending(k => k.SureMs)
            .Take(ListeSiniri)
            .Select(k => new YavasKosum(
                k.Id,
                k.OlusturmaTarihi,
                k.SureMs,
                k.SatirSayisi,
                context.Raporlar.Where(r => r.Id == k.RaporId).Select(r => r.Ad).FirstOrDefault(),
                context.Kullanicilar.Where(u => u.Id == k.KullaniciId)
                    .Select(u => u.AdSoyad ?? u.KullaniciAdi).FirstOrDefault(),
                k.Sql.Length > 160 ? k.Sql.Substring(0, 160) : k.Sql))
            .ToListAsync(cancellationToken);

        /* RAPOR KULLANIMI — yalnız rapor üzerinden gelen koşumlar (tasarımcı/SQL Editörü
         * koşumlarında RaporId null'dur ve bir raporun "kullanımı" sayılmaz).
         *
         * Raporlarla JOIN'lenir, yani SİLİNMİŞ raporlar listeye girmez. İlk sürümde adlar sonradan
         * eşlenip bulunamayanlara "(silinmiş rapor)" yazılıyordu; canlı ekranda liste bu satırlarla
         * doldu (E2E artıkları) ve asıl raporları gizledi. Silinmiş bir raporun kullanım sayısı
         * kullanıcıya hiçbir şey KAZANDIRMAZ — açılamaz, düzeltilemez, silinemez. Özet sayaçları
         * yine TÜM koşumları sayar; kaybolan bir rakam yok. */
        var kullanimlar = await kayitlar
            .Where(k => k.RaporId != null)
            .Join(context.Raporlar, k => k.RaporId!.Value, r => r.Id, (k, r) => new { Kayit = k, r.Ad })
            .GroupBy(x => new { x.Kayit.RaporId, x.Ad })
            .Select(g => new
            {
                RaporId = g.Key.RaporId!.Value,
                g.Key.Ad,
                KosumSayisi = g.Count(),
                Ortalama = (int)g.Average(x => (double)x.Kayit.SureMs),
                SonKosum = g.Max(x => x.Kayit.OlusturmaTarihi),
                Basarisiz = g.Count(x => !x.Kayit.Basarili),
            })
            .OrderByDescending(x => x.KosumSayisi)
            .Take(ListeSiniri)
            .ToListAsync(cancellationToken);

        var raporKullanimlari = kullanimlar
            .Select(x => new RaporKullanimi(
                x.RaporId, x.Ad, x.KosumSayisi, x.Ortalama, x.SonKosum, x.Basarisiz))
            .ToList();

        /* ÖLÜ RAPORLAR: dönem içinde HİÇ koşmamış olanlar. "Az kullanılan içerik" sorusunun asıl
         * muhatabı bunlar — sıfır koşum, gruplama sonucunda hiç GÖRÜNMEZ, o yüzden ayrıca sorulur. */
        var kosanIdler = await kayitlar.Where(k => k.RaporId != null)
            .Select(k => k.RaporId!.Value).Distinct().ToListAsync(cancellationToken);

        var oluRaporlar = await context.Raporlar.AsNoTracking()
            .Where(r => !kosanIdler.Contains(r.Id))
            .OrderBy(r => r.OlusturmaTarihi)
            .Take(ListeSiniri)
            .Select(r => new OluRapor(r.Id, r.Ad, r.OlusturmaTarihi))
            .ToListAsync(cancellationToken);

        return new UsageResult(gun, ozet, yavaslar, raporKullanimlari, oluRaporlar);
    }
}
