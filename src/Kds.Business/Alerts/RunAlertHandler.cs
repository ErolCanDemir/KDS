using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Kds.Business.Reports;
using Kds.Business.Subscriptions;
using Kds.Contracts.Alerts;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Alerts;

/// <summary>Uyarıyı ŞİMDİ değerlendirir: raporu koşar → değeri özetler → eşikle karşılaştırır →
/// <b>gerekiyorsa</b> e-posta gönderir → durumu yazar. Elle "Şimdi Dene" ve zamanlayıcı AYNI
/// handler'ı çağırır (tek doğruluk kaynağı); fark yalnız <c>BildirimGonder</c> bayrağıdır.
///
/// <para>Raporu <b>görünürlük kapısı olmadan</b> koşar — abonelikteki gerekçenin aynısı: uyarı
/// zaten kurulurken rapor erişimi doğrulandı (AlertCommon.RaporErisimDogrula) ve zamanlayıcıda
/// kullanıcı bağlamı yoktur. RLS satır kuralları da bu yüzden uygulanmaz; uyarıyı kuran kendi
/// kiracısının verisine bakar.</para>
///
/// <para>try/catch burada meşru — bu bir JOB icrası (AGENTS §4.6 izinli bölge): hata DOMAIN
/// durumuna (SonDurum="Hata") yazılır ve YENİDEN fırlatılır. Durum yazımı iptal edilmeyen token'la
/// yapılır: iptal/zaman aşımında hatanın kendisi kaybolmasın (18 Ağu 2026 dersi).</para></summary>
public sealed class RunAlertHandler(
    KdsDbContext context, ISender gonderici, IEpostaGonderici epostaGonderici,
    Security.ISatirKuraliCozucu satirKuralCozucu)
    : IRequestHandler<RunAlertRequest, AlertRunResult>
{
    public async Task<AlertRunResult> Handle(RunAlertRequest request, CancellationToken cancellationToken)
    {
        var uyari = await context.Uyarilar.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Uyarı bulunamadı.");

        // RLS kapısı — HER koşumda, kim kurmuş olursa olsun. Kaydetme anındaki kontrol yalnız erken
        // uyarıdır; güvenliği sağlayan burasıdır: kapı eklenmeden önce kurulmuş uyarılar, kural
        // SONRADAN eklendiğinde etkilenen uyarılar ve admin'in başkası adına kurduğu uyarılar ancak
        // böyle kapsanır. Elle "Şimdi Dene" de aynı kapıdan geçer. Metrik uyarısında (S30) kapı
        // metriğin haritasına bakar; metrik/harita bulunamazsa koşum kendi anlaşılır hatasını verir.
        var kosanKimlik = Security.KimlikAnlikGoruntusu.Oku(uyari.SahipKimlikJson);
        if (uyari.RaporId is Guid kapiRaporId)
        {
            await Security.ZamanlanmisIsKapisi.DogrulaAsync(
                context, satirKuralCozucu, kapiRaporId, "uyarı", kosanKimlik, cancellationToken);
        }
        else if (uyari.MetrikId is Guid kapiMetrikId)
        {
            var haritaId = await context.Metrikler.AsNoTracking()
                .Where(m => m.Id == kapiMetrikId).Select(m => (Guid?)m.VeriHaritasiId)
                .FirstOrDefaultAsync(cancellationToken);
            if (haritaId is Guid h)
            {
                await Security.ZamanlanmisIsKapisi.DogrulaHaritaAsync(
                    satirKuralCozucu, h, "uyarı", kosanKimlik, cancellationToken);
            }
        }

        var kronometre = Stopwatch.StartNew();
        try
        {
            var op = AlertCommon.OpCoz(uyari.Op);
            var (sonuc, deger, kosul) = await OlcAsync(uyari, op, kosanKimlik, cancellationToken);
            var tetiklendi = UyariDegerlendirme.Asildi(deger, op, uyari.Esik);
            var durumDegisti = tetiklendi != uyari.Tetiklendi;
            var bildirilmeli = UyariDegerlendirme.BildirilmeliMi(uyari.Tetiklendi, tetiklendi, uyari.SurekliBildir);

            var alicilar = AlertCommon.AlicilariCoz(uyari.AlicilarJson);
            var gonderildi = false;
            if (request.BildirimGonder && bildirilmeli && alicilar.Count > 0)
            {
                gonderildi = await epostaGonderici.GonderAsync(
                    new EpostaMesaji(alicilar, KonuKur(uyari.Ad, tetiklendi),
                        GovdeKur(uyari.Ad, tetiklendi, deger, kosul),
                        Ek: null),
                    cancellationToken);
                // Tarih YALNIZ gerçekten teslim edilince yazılır: SMTP yapılandırılmamışken
                // "bildirildi" demek, ekranın kullanıcıya yalan söylemesi olurdu.
                if (gonderildi)
                {
                    uyari.SonBildirimTarihi = DateTimeOffset.Now;
                }
            }

            // ——— Durum YALNIZ bildirim borcu kapandıysa ilerler ———
            // Tetiklendi bayrağı "bu geçiş için haber verildi" anlamına gelir; koşulsuz yazmak
            // geçişi YUTAR ve gerçek uyarı bir daha hiç gitmez. Üç yol da bunu üretiyordu:
            //   • "Şimdi Dene" (BildirimGonder=false) → durum tetiklenmişe döner, zamanlayıcı
            //     sonraki turda "değişiklik yok" der ve SUSAR. 19 Ağu 2026 canlı denemesinde tam
            //     bu yaşandı: üç zamanlanmış tur sıfır e-posta verdi ve bu yanlışlıkla "susma
            //     kuralı çalışıyor" diye okundu.
            //   • Alıcı listesi boş / SMTP yapılandırılmamış → bildirim hiç doğmadı ama geçiş
            //     tüketildi; ayar düzeltilince kimse haber almaz.
            //   • Gönderim başarısız (SMTP hatası) → aynı kalıcı sessizlik.
            // Kural: bildirim GEREKMİYORSA ya da gerçekten GÖNDERİLDİYSE ilerle; aksi hâlde
            // bayrağı olduğu gibi bırak, bir sonraki koşum tekrar denesin.
            var borcKapandi = !bildirilmeli || gonderildi;
            kronometre.Stop();
            if (request.BildirimGonder && borcKapandi)
            {
                uyari.Tetiklendi = tetiklendi;
            }

            // EKRAN durumu bildirim muhasebesinden AYRIDIR ve her ölçümde güncellenir.
            // İkisi tek koşula bağlanınca şu çelişki doğuyordu: SMTP kapalıyken kart sonsuza dek
            // gri "Hiç kontrol edilmedi" derken hemen altında taze "Son ölçüm" satırı duruyordu —
            // gerçekten tetiklenmiş ve gönderilemeyen uyarı listede hiç öne çıkmıyordu.
            uyari.SonDurum = tetiklendi ? "Tetiklendi" : "Sakin";
            uyari.SonDeger = deger;
            uyari.SonKontrolTarihi = DateTimeOffset.Now;
            uyari.SonHata = null;
            await context.SaveChangesAsync(cancellationToken);

            return new AlertRunResult(deger, tetiklendi, durumDegisti, gonderildi, alicilar.Count,
                sonuc.Satirlar.Count, (int)kronometre.Elapsed.TotalSeconds);
        }
        catch (Exception hata)
        {
            kronometre.Stop();
            uyari.SonKontrolTarihi = DateTimeOffset.Now;
            uyari.SonDurum = "Hata";
            uyari.SonHata = hata.Message;
            // Tetiklendi bayrağına DOKUNULMAZ: koşum düştü diye "toparlandı" demek yanlış olurdu.
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>Uyarının kaynağını koşup tek değere indirger. Rapor yolunda kolon+özet uygulanır;
    /// metrik yolunda (S30) ölçüm <c>MeasureMetric</c> ile aynı kırılımsız modeldir — fark, RLS'in
    /// isteği yapana değil işin SAHİBİNE göre uygulanmasıdır (zamanlanmış koşumda istek bağlamı yok).</summary>
    private async Task<(QueryRunResult Sonuc, decimal? Deger, string Kosul)> OlcAsync(
        Data.Entities.Alert uyari, UyariOp op,
        Kds.Contracts.Identity.CalistiranKullanici? kosanKimlik, CancellationToken cancellationToken)
    {
        if (uyari.MetrikId is Guid metrikId)
        {
            var metrik = await context.Metrikler.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == metrikId, cancellationToken)
                ?? throw new InvalidOperationException("Uyarının izlediği metrik bulunamadı (silinmiş olabilir).");

            var model = new QueryModel(metrik.VeriHaritasiId, Alanlar: [], Filtre: null, Siralama: [], SatirLimiti: null,
                MetrikAlanlar: [new MetricField(metrik.Id)]);
            var metrikSonuc = await gonderici.Send(
                new RunQueryModelRequest(model, KosanKimlik: kosanKimlik), cancellationToken);
            var metrikDeger = UyariDegerlendirme.Ozetle(metrikSonuc, OzetTuru.IlkSatir, metrik.Ad);
            return (metrikSonuc, metrikDeger, UyariDegerlendirme.MetrikKosulMetni(metrik.Ad, op, uyari.Esik));
        }

        var raporSonuc = await RaporuKosAsync(uyari.RaporId!.Value,
            AlertCommon.ParametreCoz(uyari.ParametrelerJson), kosanKimlik, cancellationToken);
        var ozet = AlertCommon.OzetCoz(uyari.Ozet);
        var deger = UyariDegerlendirme.Ozetle(raporSonuc, ozet, uyari.Kolon, op, uyari.Esik);
        return (raporSonuc, deger, UyariDegerlendirme.KosulMetni(ozet, uyari.Kolon, op, uyari.Esik));
    }

    private async Task<QueryRunResult> RaporuKosAsync(
        Guid raporId, IReadOnlyDictionary<string, object?>? parametreDegerleri,
        Kds.Contracts.Identity.CalistiranKullanici? kosanKimlik, CancellationToken cancellationToken)
    {
        var r = await context.Raporlar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == raporId, cancellationToken)
            ?? throw new InvalidOperationException("Uyarının izlediği rapor bulunamadı (silinmiş olabilir).");

        if (r.HamSql is not null && r.BaglantiId is Guid baglantiId)
        {
            return await gonderici.Send(new RunQueryRequest(baglantiId, r.HamSql,
                SatirSiniri: null, Parametreler: new Dictionary<string, object?>(), RaporId: r.Id), cancellationToken);
        }

        var model = r.ModelJson is null ? null
            : JsonSerializer.Deserialize<QueryModel>(r.ModelJson, ReportCommon.JsonAyar);
        if (model is null)
        {
            throw new InvalidOperationException("Rapor modeli çözülemedi.");
        }

        // RLS, işin SAHİBİNİN kimliğine uygulanır (arka planda istek bağlamı yok).
        return await gonderici.Send(
            new RunQueryModelRequest(model, parametreDegerleri, RaporId: r.Id, KosanKimlik: kosanKimlik), cancellationToken);
    }

    private static string KonuKur(string ad, bool tetiklendi) =>
        tetiklendi ? $"UYARI: {ad}" : $"Normale döndü: {ad}";

    /// <summary>Gövde iki soruyu cevaplar: ne oldu, hangi kurala göre. Normale dönüş bildirimi de
    /// gönderilir — "sustu mu, yoksa düzeldi mi?" belirsizliği uyarıya güveni bitiren şeydir.</summary>
    private static string GovdeKur(string ad, bool tetiklendi, decimal? deger, string kosul)
    {
        var ölçüm = deger?.ToString("0.####", CultureInfo.InvariantCulture) ?? "(ölçülemedi)";
        var baslik = tetiklendi
            ? $"<b>{System.Net.WebUtility.HtmlEncode(ad)}</b> uyarısı tetiklendi."
            : $"<b>{System.Net.WebUtility.HtmlEncode(ad)}</b> uyarısı normale döndü.";
        return $"""
                <p>{baslik}</p>
                <p>Ölçülen değer: <b>{ölçüm}</b><br/>Kural: {System.Net.WebUtility.HtmlEncode(kosul)}</p>
                <p style="color:#67727e;font-size:12px">Bu e-posta LST-KDS eşik uyarısıyla otomatik gönderildi;
                yalnız durum değiştiğinde bildirim yapılır.</p>
                """;
    }
}
