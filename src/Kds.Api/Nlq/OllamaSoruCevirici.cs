using System.Text;
using System.Text.Json;
using Kds.Business.Nlq;
using Kds.Business.Settings;
using Kds.Contracts.Nlq;

namespace Kds.Api.Nlq;

/// <summary>S23 — Ollama adaptörü: SQLST <c>AsistanYonlendirici</c>'nin KDS eşi. Yerel Ollama'ya
/// (varsayılan 127.0.0.1:11434, model qwen2.5-coder:7b) konuşur; soru ve şema adları makineden
/// ÇIKMAZ. Ayarlar her çağrıda Sistem Ayarları'ndan okunur (EpostaGonderici ile aynı karar:
/// ekrandan değişince yeniden başlatma gerekmez).
///
/// <para><b>Süre beklentisi (SQLST dersi):</b> 7B model CPU'da soğukta ~100 sn, ısınmışken ~60 sn
/// düşünebilir — bu bir hata değil çalışma biçimidir; zaman aşımı bu yüzden 300 sn'dir ve ekran
/// "yerel model düşünüyor" der. IO adaptörü — try/catch burada meşru (AGENTS §4.6).</para></summary>
public sealed class OllamaSoruCevirici(ISistemAyarlari ayarlar, IHttpClientFactory istemciler) : ISoruCevirici
{
    /// <summary>Fizibilitede kalibre edilen üretim ayarları: düşük sıcaklık (yaratıcılık değil
    /// tutarlılık istiyoruz), 8192 bağlam (şema + katalog + soru rahat sığar) — SQLST Modelfile
    /// değerlerinin birebir aynısı. format:json kod bloğu sarmalamasını da azaltır (tamamen
    /// kesmiyor; kurtarıcı çite ayrıca dayanıklı).</summary>
    private static readonly TimeSpan CeviriZamanAsimi = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan DurumZamanAsimi = TimeSpan.FromSeconds(3);

    public async Task<string> CevirAsync(string istem, CancellationToken ct)
    {
        var (taban, model) = Ayarlar();
        var govde = JsonSerializer.Serialize(new
        {
            model,
            stream = false,
            format = "json",
            /* keep_alive (S30c): Ollama varsayılanı 5 dk — iki soru arasında bu süre geçince model
             * bellekten düşüyor ve sonraki soru YÜKLEME bekliyordu (23 Ağu 2026: soğuk çeviri
             * 108 sn, sıcak aynı soru 24 sn). 30 dk bir çalışma oturumunu kapsar; RAM'de ~5.5 GB
             * tutar, makinede yer kalmazsa Ollama zaten kendisi tahliye eder.
             * num_predict: kaçak üretim sigortası — üretim CPU'da ~5.5 tok/s, sınırsız bırakılınca
             * model kendini tekrar edip dakikalarca yazabiliyor. Sorgu modeli JSON'u ~150 token;
             * 700 fazlasıyla yeter ve en kötü hâli ~2 dk'da keser. */
            keep_alive = "30m",
            options = new { temperature = 0.2, num_ctx = 8192, num_predict = 700 },
            messages = new[] { new { role = "user", content = istem } },
        });

        try
        {
            using var istemci = Istemci(CeviriZamanAsimi);
            using var yanit = await istemci.PostAsync(
                $"{taban}/api/chat", new StringContent(govde, Encoding.UTF8, "application/json"), ct);
            var metin = await yanit.Content.ReadAsStringAsync(ct);
            if (!yanit.IsSuccessStatusCode)
            {
                throw new SoruCeviriHatasi(HataMesaji(yanit.StatusCode, metin, model));
            }

            using var belge = JsonDocument.Parse(metin);
            return belge.RootElement.TryGetProperty("message", out var mesaj)
                && mesaj.TryGetProperty("content", out var icerik)
                    ? icerik.GetString() ?? ""
                    : throw new SoruCeviriHatasi("Yerel model beklenmedik bir yanıt biçimi döndürdü.");
        }
        catch (HttpRequestException hata)
        {
            throw new SoruCeviriHatasi(
                $"Yerel modele ulaşılamadı ({taban}). Ollama çalışmıyor olabilir — kurulum: " +
                "ollama.com'dan indirin, sonra 'ollama pull " + model + "'.", hata);
        }
        catch (TaskCanceledException hata) when (!ct.IsCancellationRequested)
        {
            throw new SoruCeviriHatasi(
                "Yerel model izin verilen sürede (300 sn) cevap veremedi. CPU'da büyük modeller " +
                "yavaş düşünür — daha küçük bir model seçmeyi deneyin (Sistem Ayarları → Soru Sor).", hata);
        }
    }

    public async Task IsitAsync(string onek, CancellationToken ct)
    {
        var (taban, model) = Ayarlar();
        /* num_predict = 1: amaç cevap almak DEĞİL, öneki modele işletmek. Ollama üretimden önce
         * istemi baştan sona işler; tek token üretip durunca önek önbellekte kalır ve asıl soru
         * geldiğinde yalnız SORU kısmı işlenir (ölçüm: 49.7 sn → 0.2 sn).
         * Önek ile asıl istemin başlangıcı BİREBİR aynı olmak zorundadır — bu yüzden ikisi de
         * SoruIstemleri.OnekKur'dan gelir, burada metne hiçbir şey eklenmez. */
        var govde = JsonSerializer.Serialize(new
        {
            model,
            stream = false,
            keep_alive = "30m",
            options = new { temperature = 0.2, num_ctx = 8192, num_predict = 1 },
            messages = new[] { new { role = "user", content = onek } },
        });

        try
        {
            using var istemci = Istemci(CeviriZamanAsimi);
            using var yanit = await istemci.PostAsync(
                $"{taban}/api/chat", new StringContent(govde, Encoding.UTF8, "application/json"), ct);
            _ = yanit.IsSuccessStatusCode; // sonuç ilgilendirmiyor: yan etki (önbellek) için çağrılıyor
        }
        catch (Exception hata) when (hata is HttpRequestException or TaskCanceledException)
        {
            // Isıtma başarısızlığı kullanıcıya YANSITILMAZ: soru sorulduğunda normal yol zaten
            // aynı hatayı anlamlı mesajla üretecek. Burada susmak doğru davranıştır.
        }
    }

    public async Task<NlqStatusResult> DurumAsync(CancellationToken ct)
    {
        var (taban, model) = Ayarlar();
        try
        {
            using var istemci = Istemci(DurumZamanAsimi);
            using var yanit = await istemci.GetAsync($"{taban}/api/tags", ct);
            if (!yanit.IsSuccessStatusCode)
            {
                return new NlqStatusResult(false, model, $"Ollama beklenmedik cevap verdi (HTTP {(int)yanit.StatusCode}).");
            }

            using var belge = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync(ct));
            var kurulu = belge.RootElement.TryGetProperty("models", out var modeller)
                && modeller.EnumerateArray().Any(m =>
                    m.TryGetProperty("name", out var ad) &&
                    string.Equals(ad.GetString(), model, StringComparison.OrdinalIgnoreCase));

            return kurulu
                ? new NlqStatusResult(true, model, null)
                : new NlqStatusResult(false, model,
                    $"Ollama çalışıyor ama '{model}' modeli indirilmemiş — terminalde: ollama pull {model}");
        }
        catch (Exception hata) when (hata is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new NlqStatusResult(false, model,
                $"Yerel modele ulaşılamadı ({taban}). Ollama kurulu değilse ollama.com'dan indirin; " +
                $"kuruluysa servisi başlatın, sonra: ollama pull {model}");
        }
    }

    private (string Taban, string Model) Ayarlar()
    {
        var taban = ayarlar.Metin(AyarTanimlari.SoruSorTabanAdres).TrimEnd('/');
        var model = ayarlar.Metin(AyarTanimlari.SoruSorModel);
        return (taban, model);
    }

    private HttpClient Istemci(TimeSpan zamanAsimi)
    {
        var istemci = istemciler.CreateClient(nameof(OllamaSoruCevirici));
        istemci.Timeout = zamanAsimi;
        return istemci;
    }

    private static string HataMesaji(System.Net.HttpStatusCode kod, string govde, string model)
    {
        // Ollama model bulunamayınca 404 + {"error":"model ... not found"} döner — en olası
        // kurulum eksiği; mesaj doğrudan komutu söyler.
        if (kod == System.Net.HttpStatusCode.NotFound && govde.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return $"'{model}' modeli Ollama'da yok — terminalde: ollama pull {model}";
        }

        return $"Yerel model hata döndürdü (HTTP {(int)kod}).";
    }
}
