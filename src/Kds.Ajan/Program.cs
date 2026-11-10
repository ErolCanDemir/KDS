using System.Data.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Kds.Contracts.Agents;
using Kds.Execution;
using Microsoft.Data.SqlClient;

/* KDS AJANI (S45) — müşteri ağına kurulan, DIŞARI DOĞRU bağlanan hafif süreç (Power BI gateway
 * modeli, 00-analiz §3). Görevi: KDS'deki içe alım işlerini KENDİ tarafında koşturup veriyi
 * KDS deposuna İTMEK. Böylece:
 *   - SaaS'ın müşteri DB'sine içeri doğru erişimi hiç gerekmez (firewall'da delik açılmaz),
 *   - kaynak bağlantı dizeleri/parolalar bu makinedeki ajan.json'da kalır, KDS'ye GÖNDERİLMEZ.
 *
 * Akış: yokla (isler) → her iş için her tabloyu yerelden oku → partiler hâlinde it → bitir.
 * Artımlı yükleme (S44) ajan tarafında da aynıdır: su seviyesini sunucu söyler, ajan yalnız
 * o dilimi okur; MERGE sunucu tarafında yapılır.
 *
 * Konsol çıktısı bilinçli: bu bir kurulum aracı, arayüzü terminaldir. */

var yapilandirmaYolu = args.Length > 0 ? args[0] : "ajan.json";
if (!File.Exists(yapilandirmaYolu))
{
    Console.Error.WriteLine($"Yapılandırma bulunamadı: {yapilandirmaYolu}");
    Console.Error.WriteLine("KDS → Yönetim → Ajanlar ekranından üretilen ajan.json bu dizine konmalı.");
    return 1;
}

AjanYapilandirma yapilandirma;
try
{
    yapilandirma = JsonSerializer.Deserialize<AjanYapilandirma>(
        File.ReadAllText(yapilandirmaYolu), JsonAyar.Ayarlar)
        ?? throw new JsonException("boş dosya");
}
catch (JsonException hata)
{
    Console.Error.WriteLine($"ajan.json okunamadı: {hata.Message}");
    return 1;
}

if (string.IsNullOrWhiteSpace(yapilandirma.KdsAdres) || string.IsNullOrWhiteSpace(yapilandirma.Anahtar))
{
    Console.Error.WriteLine("ajan.json'da kdsAdres ve anahtar zorunlu.");
    return 1;
}

using var http = new HttpClient
{
    BaseAddress = new Uri(yapilandirma.KdsAdres.TrimEnd('/')),
    Timeout = TimeSpan.FromMinutes(10),   // büyük parti + yavaş hat
};

Console.WriteLine($"KDS Ajanı başladı → {yapilandirma.KdsAdres} (yoklama {yapilandirma.YoklamaSaniye} sn)");
using var iptal = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; iptal.Cancel(); };

while (!iptal.IsCancellationRequested)
{
    try
    {
        await TurAtAsync(http, yapilandirma, iptal.Token);
    }
    catch (OperationCanceledException) when (iptal.IsCancellationRequested)
    {
        break;
    }
    catch (Exception hata)
    {
        // Tur hatası ajanı DÜŞÜRMEZ (ağ kesintisi, KDS yeniden başlıyor olabilir) — bekleyip
        // yeniden denenir. Arka plan süreci sınırında yakalama meşru.
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Tur hatası: {hata.Message}");
    }

    try
    {
        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(yapilandirma.YoklamaSaniye, 10, 3600)), iptal.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

Console.WriteLine("KDS Ajanı durdu.");
return 0;

static async Task TurAtAsync(HttpClient http, AjanYapilandirma yapilandirma, CancellationToken ct)
{
    var yanit = await http.PostAsJsonAsync("/api/ajan/isler",
        new AjanIsleriRequest(yapilandirma.Anahtar), JsonAyar.Ayarlar, ct);
    if (!yanit.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Yoklama reddedildi: HTTP {(int)yanit.StatusCode}" +
            ((int)yanit.StatusCode == 403 ? " — anahtar geçersiz ya da iptal edilmiş olabilir." : ""));
        return;
    }

    var isler = await yanit.Content.ReadFromJsonAsync<AjanIsleriResult>(JsonAyar.Ayarlar, ct);
    if (isler is null || isler.Isler.Count == 0)
    {
        return;
    }

    foreach (var isDto in isler.Isler)
    {
        await IsKosAsync(http, yapilandirma, isDto, ct);
    }
}

static async Task IsKosAsync(HttpClient http, AjanYapilandirma yapilandirma, AjanIsDto isDto, CancellationToken ct)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] İş: {isDto.IsAd} ({isDto.Tablolar.Count} tablo)");
    var kronometre = System.Diagnostics.Stopwatch.StartNew();

    if (!yapilandirma.Baglantilar.TryGetValue(isDto.BaglantiId.ToString(), out var kaynakDizesi))
    {
        // Bağlantı dizesi bu makinede tanımlı değil — en sık kurulum hatası; sunucuya AÇIKÇA bildir
        // ki ekranda "ajan dizeyi bilmiyor" diye görünsün, sessizce atlanmasın.
        await IsBitirAsync(http, yapilandirma, isDto.IsId, "Hata",
            $"Ajan yapılandırmasında '{isDto.BaglantiId}' bağlantısının dizesi yok (ajan.json → baglantilar).",
            0, 0, 0, ct);
        return;
    }

    long toplam = 0;
    var artimliSayisi = 0;
    try
    {
        foreach (var tablo in isDto.Tablolar)
        {
            var (satir, artimliMi) = await TabloAktarAsync(http, yapilandirma, isDto.IsId, kaynakDizesi, tablo, ct);
            toplam += satir;
            if (artimliMi) artimliSayisi++;
        }

        kronometre.Stop();
        await IsBitirAsync(http, yapilandirma, isDto.IsId, "Basarili", null,
            toplam, (int)kronometre.Elapsed.TotalSeconds, artimliSayisi, ct);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   bitti: {toplam:N0} satır · {artimliSayisi} tablo artımlı · {kronometre.Elapsed.TotalSeconds:F1} sn");
    }
    catch (Exception hata) when (hata is not OperationCanceledException)
    {
        kronometre.Stop();
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}]   HATA: {hata.Message}");
        await IsBitirAsync(http, yapilandirma, isDto.IsId, "Hata", hata.Message,
            toplam, (int)kronometre.Elapsed.TotalSeconds, artimliSayisi, ct);
    }
}

static async Task<(long Satir, bool ArtimliMi)> TabloAktarAsync(
    HttpClient http, AjanYapilandirma yapilandirma, Guid isId, string kaynakDizesi,
    AjanTabloDto tablo, CancellationToken ct)
{
    var kolonListesi = tablo.Kolonlar is { Count: > 0 }
        ? string.Join(", ", tablo.Kolonlar.Select(k => $"[{DepoYazimi.Kacir(k)}]"))
        : "*";
    var kaynakTam = $"[{DepoYazimi.Kacir(tablo.Sema)}].[{DepoYazimi.Kacir(tablo.Tablo)}]";

    await using var kaynak = new SqlConnection(kaynakDizesi);
    await kaynak.OpenAsync(ct);

    // 1) Şema + PK + (artımlıysa) yeni sınır — hepsi YEREL kaynaktan.
    var kolonlar = await SemaOkuAsync(kaynak, kolonListesi, kaynakTam, ct);
    var pk = await PkOkuAsync(kaynak, tablo.Sema, tablo.Tablo, ct);
    string? yeniSinir = null;
    var artimliTip = kolonlar.FirstOrDefault(k =>
        string.Equals(k.Ad, tablo.ArtimliKolon, StringComparison.OrdinalIgnoreCase))?.Tip;
    if (tablo.ArtimliKolon is not null && artimliTip is not null)
    {
        // Sınır KOPYADAN ÖNCE okunur (S44'teki karar: hedef kaçarsa pencere hiç kapanmaz).
        await using var maxKomut = kaynak.CreateCommand();
        maxKomut.CommandText = $"SELECT MAX([{DepoYazimi.Kacir(tablo.ArtimliKolon)}]) FROM {kaynakTam}";
        yeniSinir = SuSeviyesi.Bicimle(await maxKomut.ExecuteScalarAsync(ct));
    }

    // 2) Mod kararı (ajan tarafı ön koşullar; sunucu hedefe bakıp yine de TAM'a düşürebilir).
    var artimliOlabilir = tablo.ArtimliKolon is not null && tablo.SuSeviyesi is not null
        && artimliTip is not null && pk.Count > 0
        && pk.All(p => kolonlar.Any(k => string.Equals(k.Ad, p, StringComparison.OrdinalIgnoreCase)));

    var basla = await IstekAsync<AjanTabloBaslaResult>(http, "/api/ajan/tablo-basla",
        new AjanTabloBaslaRequest(yapilandirma.Anahtar, isId, tablo.Sema, tablo.Tablo,
            artimliOlabilir ? "Artimli" : "Tam", kolonlar), ct);
    var mod = basla.Mod;

    // 3) Satırları oku ve partiler hâlinde it.
    await using (var okuKomut = kaynak.CreateCommand())
    {
        if (mod == "Artimli")
        {
            okuKomut.CommandText = $"SELECT {kolonListesi} FROM {kaynakTam} " +
                $"WHERE [{DepoYazimi.Kacir(tablo.ArtimliKolon!)}] >= @eski AND [{DepoYazimi.Kacir(tablo.ArtimliKolon!)}] <= @yeni";
            okuKomut.Parameters.Add(SuSeviyesi.Parametre("@eski", artimliTip!.ToLowerInvariant(), tablo.SuSeviyesi!));
            okuKomut.Parameters.Add(SuSeviyesi.Parametre("@yeni", artimliTip.ToLowerInvariant(), yeniSinir!));
        }
        else
        {
            okuKomut.CommandText = $"SELECT {kolonListesi} FROM {kaynakTam}";
        }

        okuKomut.CommandTimeout = 0;
        await using var okuyucu = await okuKomut.ExecuteReaderAsync(ct);
        var hedef = mod == "Artimli" ? "Staging" : "Dogrudan";
        var parti = new List<IReadOnlyList<JsonElement>>(PartiBoyu);
        while (await okuyucu.ReadAsync(ct))
        {
            var satir = new JsonElement[okuyucu.FieldCount];
            for (var i = 0; i < okuyucu.FieldCount; i++)
            {
                // Değer kodlaması AjanVeri'de tek yerde (sunucu aynı haritayla çözer).
                satir[i] = JsonSerializer.SerializeToElement(
                    AjanVeri.Yaz(okuyucu.IsDBNull(i) ? null : okuyucu.GetValue(i)), JsonAyar.Ayarlar);
            }

            parti.Add(satir);
            if (parti.Count >= PartiBoyu)
            {
                await IstekAsync<bool>(http, "/api/ajan/satirlar",
                    new AjanSatirlarRequest(yapilandirma.Anahtar, isId, tablo.Sema, tablo.Tablo, hedef, kolonlar, parti), ct);
                parti = new List<IReadOnlyList<JsonElement>>(PartiBoyu);
            }
        }

        if (parti.Count > 0)
        {
            await IstekAsync<bool>(http, "/api/ajan/satirlar",
                new AjanSatirlarRequest(yapilandirma.Anahtar, isId, tablo.Sema, tablo.Tablo, hedef, kolonlar, parti), ct);
        }
    }

    // 4) Kapat: MERGE/indeks sunucuda; su seviyesi tablo başına orada yazılır.
    var bitir = await IstekAsync<AjanTabloBitirResult>(http, "/api/ajan/tablo-bitir",
        new AjanTabloBitirRequest(yapilandirma.Anahtar, isId, tablo.Sema, tablo.Tablo, mod, kolonlar, pk, yeniSinir), ct);
    return (bitir.SatirSayisi, mod == "Artimli");
}

static async Task<IReadOnlyList<AjanKolonDto>> SemaOkuAsync(
    SqlConnection kaynak, string kolonListesi, string kaynakTam, CancellationToken ct)
{
    await using var komut = kaynak.CreateCommand();
    komut.CommandText = $"SELECT {kolonListesi} FROM {kaynakTam} WHERE 1 = 0";
    await using var okuyucu = await komut.ExecuteReaderAsync(ct);
    var kolonlar = await okuyucu.GetColumnSchemaAsync(ct);
    return [.. kolonlar.Select(k => new AjanKolonDto(
        k.ColumnName ?? "",
        (k.DataTypeName ?? "sql_variant").ToLowerInvariant(),
        k.ColumnSize,
        k.NumericPrecision is int p ? (byte)Math.Clamp(p, 0, 255) : null,
        k.NumericScale is int s and >= 0 and <= 255 ? (byte)s : null,
        k.AllowDBNull != false))];
}

static async Task<IReadOnlyList<string>> PkOkuAsync(
    SqlConnection kaynak, string sema, string tablo, CancellationToken ct)
{
    await using var komut = kaynak.CreateCommand();
    komut.CommandText = """
        SELECT c.name
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        JOIN sys.tables t ON t.object_id = i.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE i.is_primary_key = 1 AND s.name = @sema AND t.name = @tablo
        ORDER BY ic.key_ordinal;
        """;
    komut.Parameters.AddWithValue("@sema", sema);
    komut.Parameters.AddWithValue("@tablo", tablo);
    var liste = new List<string>();
    await using var okuyucu = await komut.ExecuteReaderAsync(ct);
    while (await okuyucu.ReadAsync(ct))
        liste.Add(okuyucu.GetString(0));
    return liste;
}

static async Task IsBitirAsync(HttpClient http, AjanYapilandirma yapilandirma, Guid isId,
    string durum, string? hata, long toplam, int saniye, int artimliSayisi, CancellationToken ct)
{
    await IstekAsync<bool>(http, "/api/ajan/is-bitir",
        new AjanIsBitirRequest(yapilandirma.Anahtar, isId, durum, hata, toplam, saniye, artimliSayisi), ct);
}

static async Task<T> IstekAsync<T>(HttpClient http, string yol, object govde, CancellationToken ct)
{
    var yanit = await http.PostAsJsonAsync(yol, govde, JsonAyar.Ayarlar, ct);
    if (!yanit.IsSuccessStatusCode)
    {
        var metin = await yanit.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"KDS {yol} HTTP {(int)yanit.StatusCode}: {Kisalt(metin)}");
    }

    return await yanit.Content.ReadFromJsonAsync<T>(JsonAyar.Ayarlar, ct)
        ?? throw new InvalidOperationException($"KDS {yol} boş yanıt döndürdü.");
}

static string Kisalt(string metin) => metin.Length <= 300 ? metin : metin[..300];

file static class JsonAyar
{
    public static readonly JsonSerializerOptions Ayarlar = new(JsonSerializerDefaults.Web);
}

/// <summary>ajan.json — bağlantı dizeleri BURADA, müşteri makinesinde durur; KDS'ye gönderilmez.</summary>
file sealed record AjanYapilandirma(
    string KdsAdres,
    string Anahtar,
    int YoklamaSaniye,
    Dictionary<string, string> Baglantilar)
{
    public AjanYapilandirma() : this("", "", 60, []) { }
}

internal partial class Program
{
    /// <summary>İstek başına satır partisi — JSON yükü birkaç MB'ta kalsın (Kestrel sınırı 30MB).</summary>
    private const int PartiBoyu = 5_000;
}
