using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Kds.Tests.Integration;

// LST-KDS API yük testi (bağımlılıksız — lisans-güvenli). NBomber 6.x ticari kullanımda lisans
// istediğinden (K4/MediatR deseniyle tutarlı) kendi hafif koşucumuzu kullanırız. API'yi bellekte
// (WebApplicationFactory) sahte "Test" kimliğiyle ayağa kaldırır; Keycloak/canlı sunucu GEREKMEZ.
// Metadata deposu paylaşımlı-önbellek SQLite → EF her istekte kendi bağlantısını açar, eşzamanlılık
// gerçektir. Ölçülen: API → MediatR → EF boru hattı verimi (p50/p95/p99, RPS, hata oranı). Gerçek
// hedef-DB (KdsDemo) sorgu ÇALIŞTIRMA yükü token gerektirdiğinden E2E/canlı ortama aittir; burada
// sorgu SQL'i ÜRETİMİ (CPU + harita okuma) yüklenir.
//
// Kullanım:  dotnet run --project tests/Kds.LoadTests [saniye]   (varsayılan 10 sn/senaryo)

var saniye = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 10;

using var fabrika = new KdsWebFactory();
var tenant = await fabrika.TenantTohumlaAsync();

var istemci = fabrika.CreateClient();
istemci.DefaultRequestHeaders.Add("X-Test-Auth", "1");
istemci.DefaultRequestHeaders.Add("X-Test-Tenant", tenant);
istemci.DefaultRequestHeaders.Add("X-Test-Sub", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
istemci.DefaultRequestHeaders.Add("X-Test-Roles", "admin");

static async Task<Guid> Id(HttpResponseMessage r) =>
    JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

// --- Yük öncesi tohum: bağlantı + tek tablolu harita (SQL üretimi) + birkaç pano (liste) ---
var connId = await Id(await istemci.PostAsJsonAsync("/api/baglantilar", new
{
    ad = "Yük", sunucu = "s", veritabani = "d",
    kullaniciAdi = (string?)null, sifre = (string?)null, mod = "Canli",
}));

var tableId = Guid.NewGuid();
var mapId = await Id(await istemci.PostAsJsonAsync("/api/haritalar", new
{
    id = (Guid?)null, ad = "Yük Haritası", baglantiId = connId, mod = "Canli",
    tablolar = new object[]
    {
        new
        {
            id = tableId, sema = "dbo", hamAd = "Satislar", dostcaAd = "Satışlar",
            kolonlar = new object[]
            {
                new { hamAd = "Id", dostcaAd = (string?)null, rol = "Anahtar", veriTipi = "int" },
                new { hamAd = "Tutar", dostcaAd = "Tutar", rol = "Olcu", veriTipi = "decimal" },
                new { hamAd = "Sehir", dostcaAd = "Şehir", rol = "Boyut", veriTipi = "nvarchar" },
            },
        },
    },
    iliskiler = Array.Empty<object>(),
}));

for (var i = 0; i < 5; i++)
    await istemci.PostAsJsonAsync("/api/panolar", new
    {
        id = (Guid?)null, ad = $"Pano {i}", paylasim = "Ozel", raporlar = Array.Empty<Guid>(),
    });

var model = new
{
    haritaId = mapId,
    alanlar = new object[]
    {
        new { tabloId = tableId, kolon = "Sehir", toplama = "Yok", takmaAd = (string?)null },
        new { tabloId = tableId, kolon = "Tutar", toplama = "Topla", takmaAd = "ToplamTutar" },
    },
    filtre = (object?)null,
    siralama = Array.Empty<object>(),
    satirLimiti = (int?)100,
};

Console.WriteLine($"\n=== LST-KDS API Yük Testi (in-memory) — {saniye} sn/senaryo ===\n");

await Senaryo("panolar_listele  (GET /api/panolar)", 100, saniye,
    async () => (await istemci.GetAsync("/api/panolar")).IsSuccessStatusCode);

await Senaryo("baglantilar_liste (GET /api/baglantilar)", 100, saniye,
    async () => (await istemci.GetAsync("/api/baglantilar")).IsSuccessStatusCode);

await Senaryo("sorgu_sql_uret    (POST /api/sorgu/sql)", 50, saniye,
    async () => (await istemci.PostAsJsonAsync("/api/sorgu/sql", model)).IsSuccessStatusCode);

Console.WriteLine("Tamamlandı.\n");
return;

// --- Hafif açık-döngü yük koşucusu: her saniye `rps` eşzamanlı istek, `saniye` boyunca ---
static async Task Senaryo(string ad, int rps, int saniye, Func<Task<bool>> islem)
{
    var gecikmeler = new ConcurrentBag<double>();
    var ok = 0;
    var hata = 0;
    var gorevler = new List<Task>(rps * saniye);
    var sw = Stopwatch.StartNew();

    for (var sn = 0; sn < saniye; sn++)
    {
        var turBasi = sw.Elapsed;
        for (var i = 0; i < rps; i++)
        {
            gorevler.Add(Task.Run(async () =>
            {
                var t = Stopwatch.GetTimestamp();
                bool basarili;
                try { basarili = await islem(); }
                catch { basarili = false; }
                gecikmeler.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
                if (basarili) Interlocked.Increment(ref ok); else Interlocked.Increment(ref hata);
            }));
        }
        var kalan = TimeSpan.FromSeconds(1) - (sw.Elapsed - turBasi);
        if (kalan > TimeSpan.Zero) await Task.Delay(kalan);
    }

    await Task.WhenAll(gorevler);
    sw.Stop();

    var dizi = gecikmeler.OrderBy(x => x).ToArray();
    double Yuzdelik(double p) => dizi.Length == 0 ? 0
        : dizi[Math.Clamp((int)Math.Ceiling(p / 100.0 * dizi.Length) - 1, 0, dizi.Length - 1)];
    var toplam = ok + hata;
    var gercekRps = toplam / sw.Elapsed.TotalSeconds;

    Console.WriteLine($"● {ad}");
    Console.WriteLine($"    istek: {toplam}  ok: {ok}  hata: {hata}   gerçek RPS: {gercekRps:F0}");
    Console.WriteLine($"    gecikme(ms):  min {dizi.FirstOrDefault():F2}  ort {(dizi.Length > 0 ? dizi.Average() : 0):F2}  max {dizi.LastOrDefault():F2}");
    Console.WriteLine($"    yüzdelik(ms): p50 {Yuzdelik(50):F2}  p95 {Yuzdelik(95):F2}  p99 {Yuzdelik(99):F2}\n");
}
