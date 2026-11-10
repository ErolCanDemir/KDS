using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Kds.Execution;

/// <summary>MSSQL→MSSQL içe alım (S12/Mod B): depo DB'yi yoksa yaratır, seçili her tabloyu TAM
/// (DROP+CREATE+<see cref="SqlBulkCopy"/>) ya da ARTIMLI (S44) yükler. Kolon tipleri
/// <c>GetColumnSchema</c>'dan çıkarılır (MSSQL-only, v1). Hata yakalama burada meşrudur —
/// IO adaptörü sınırı (AGENTS §4.6). DDL/MERGE/su-seviyesi üretimi <see cref="DepoYazimi"/> ve
/// <see cref="SuSeviyesi"/> ortak çekirdeğinde (S45'te ajan push yolu da aynısını kullanır).
///
/// ARTIMLI YOL (S44 — su seviyesi + staging + MERGE; CDC bilinçli SEÇİLMEDİ: kaynakta CDC açmak
/// sysadmin ister, ürün kaynağa salt-okunur kullanıcıyla bağlanır):
/// 1. Kaynaktan artımlı kolonun MAX'ı okunur (yeni sınır) — KOPYADAN ÖNCE, hedef kaçmasın.
/// 2. <c>eski su seviyesi &lt;= kolon &lt;= yeni sınır</c> dilimi staging tablosuna bulk-copy edilir.
///    Alt sınır BİLEREK &gt;= : eşit değerli satırlar (aynı tarihe düşen geç kayıtlar) tekrar
///    kopyalanır ama MERGE PK ile tekilleştirir; &gt; olsaydı o satırlar SESSİZCE kaybolurdu.
/// 3. Staging, kaynak PK'sı üzerinden hedefe MERGE edilir (varsa güncelle, yoksa ekle).
/// SİLİNEN satırlar artımlıda taşınmaz — "Tam Yükle" bunun için vardır.
/// TAM yüklemeye kendiliğinden dönüş: su seviyesi yok (ilk koşu) · hedef tablo yok · kolon adları
/// kaymış · kaynakta PK yok ya da PK/artımlı kolon seçili kolonların dışında. Bu düşüş sonuçta
/// raporlanır (ArtimliTabloSayisi) — sessiz davranış farkı bırakılmaz.</summary>
public sealed class SqlIngestRunner : IIngestRunner
{
    public async Task<IngestOutcome> RunAsync(string kaynakDizesi, string depoSunucuDizesi,
        string depoDbAdi, IReadOnlyList<IngestTableSpec> tablolar,
        IReadOnlyDictionary<string, string> suSeviyeleri, CancellationToken cancellationToken)
    {
        await DepoyuYaratAsync(depoSunucuDizesi, depoDbAdi, cancellationToken);
        return await RunHedefeAsync(kaynakDizesi,
            SqlConnectionString.VeritabaniIle(depoSunucuDizesi, depoDbAdi), tablolar, suSeviyeleri, cancellationToken);
    }

    /// <summary>M22b — hedef, kullanıcının seçtiği VAR OLAN veritabanı: DB yaratılmaz;
    /// tablolar tam ya da artımlı yolla tazelenir.</summary>
    public async Task<IngestOutcome> RunHedefeAsync(string kaynakDizesi, string depoDizesi,
        IReadOnlyList<IngestTableSpec> tablolar, IReadOnlyDictionary<string, string> suSeviyeleri,
        CancellationToken cancellationToken)
    {
        long toplam = 0;
        var artimliSayisi = 0;
        var yeniSeviyeler = new Dictionary<string, string>();

        foreach (var t in tablolar)
        {
            if (string.IsNullOrWhiteSpace(t.ArtimliKolon))
            {
                toplam += await BirTabloTamAsync(kaynakDizesi, depoDizesi, t, cancellationToken);
                continue;
            }

            var sonuc = await BirTabloArtimliAsync(kaynakDizesi, depoDizesi, t,
                suSeviyeleri.TryGetValue(TabloAnahtari(t), out var eski) ? eski : null, cancellationToken);
            toplam += sonuc.Satir;
            if (sonuc.ArtimliMi) artimliSayisi++;
            if (sonuc.YeniSeviye is not null) yeniSeviyeler[TabloAnahtari(t)] = sonuc.YeniSeviye;
        }

        return new IngestOutcome(tablolar.Count, toplam, artimliSayisi, yeniSeviyeler);
    }

    /// <summary>Su seviyesi sözlüğünün anahtarı — işin JSON'ındaki yazımla birebir.</summary>
    private static string TabloAnahtari(IngestTableSpec t) => $"{t.Sema}.{t.Tablo}";

    /// <summary>Depo DB'yi yoksa yaratır (master üstünden). Ajan push yolu da kullanır (S45).</summary>
    internal static async Task DepoyuYaratAsync(string sunucuDizesi, string dbAdi, CancellationToken ct)
    {
        await using var baglanti = new SqlConnection(sunucuDizesi);
        await baglanti.OpenAsync(ct);
        await using var komut = baglanti.CreateCommand();
        // dbAdi sunucu üretimli (KdsDepo_<guid>); yine de köşeli-parantez ve tırnak kaçışı uygulanır.
        komut.CommandText =
            $"IF DB_ID(N'{dbAdi.Replace("'", "''")}') IS NULL EXEC('CREATE DATABASE [{DepoYazimi.Kacir(dbAdi)}]')";
        await komut.ExecuteNonQueryAsync(ct);

        /* AUTO_CLOSE KAPATILIR (24 Ağu 2026 ölçümü — raporların asıl yavaşlık nedeni buydu):
         * LocalDB'de CREATE DATABASE ile doğan veritabanı AUTO_CLOSE=ON gelir. Son bağlantı
         * kapanınca veritabanı kapanır ve önbelleği (buffer pool) atılır; sonraki her rapor
         * 2M satırı yeniden DİSKTEN okur. Ölçüm: aynı sorgu AUTO_CLOSE açıkken 9.4 sn
         * (CPU yalnız 62 ms — gerisi saf disk beklemesi), kapalıyken 0.15 sn.
         * Raporlama deposu kısa ömürlü bağlantılarla çalışır; bu ayar tam tersini varsayar. */
        await using var ayar = baglanti.CreateCommand();
        ayar.CommandText = $"ALTER DATABASE [{DepoYazimi.Kacir(dbAdi)}] SET AUTO_CLOSE OFF WITH NO_WAIT";
        await ayar.ExecuteNonQueryAsync(ct);
    }

    // ---------- TAM YÜKLEME (S12'den beri olan yol) ----------

    private static async Task<long> BirTabloTamAsync(string kaynakDizesi, string depoDizesi,
        IngestTableSpec spec, CancellationToken ct)
    {
        var kolonListesi = KolonListesi(spec);
        var kaynakTam = $"[{DepoYazimi.Kacir(spec.Sema)}].[{DepoYazimi.Kacir(spec.Tablo)}]";

        await using var kaynak = new SqlConnection(kaynakDizesi);
        await kaynak.OpenAsync(ct);

        // 1) Şema (satırsız) → depo tablosunu (yeniden) yarat.
        var kolonlar = await KaynakSemasiAsync(kaynak, kolonListesi, kaynakTam, ct);
        await DepodaCalistirAsync(depoDizesi, DepoYazimi.TabloDdl(spec.Sema, spec.Tablo, kolonlar), ct);

        // 2) Kaynak verisini oku → SqlBulkCopy ile depoya yaz.
        await KaynaktanKopyalaAsync(kaynak, depoDizesi, kolonListesi, kaynakTam, spec.Sema, spec.Tablo,
            kosul: null, parametreler: null, ct);

        // 2b) Yükleme BİTTİKTEN sonra indeks kur — kaynakta indeks olsun olmasın depo tablosu
        // aksi hâlde HEAP kalıyordu ve her rapor tam tarama yapıyordu (24 Ağu 2026 ölçümü:
        // 2M satırlık tabloda aylık kırılım 13.2 sn → indeksle 0.33 sn, 40 kat). Sıra önemli:
        // indeks bulk copy'den ÖNCE kurulursa yükleme yavaşlar, sonra kurulunca tek seferde
        // toplu inşa edilir. Başarısızlık içe alımı DÜŞÜRMEZ — veri yüklendi, indeks hızdır.
        foreach (var indeksDdl in DepoYazimi.IndeksDdlUret(spec.Sema, spec.Tablo, kolonlar))
        {
            try
            {
                await DepodaCalistirAsync(depoDizesi, indeksDdl, ct);
            }
            catch (SqlException)
            {
                // İndeks kurulamadı (ör. kolon indekslenemez tipte, izin yok): veri sağlam,
                // yalnız sorgular yavaş kalır — içe alımı bu yüzden başarısız saymayız.
            }
        }

        // 3) Satır sayısı (doğru long) — hedef tablodan.
        return await HedefSatirSayAsync(depoDizesi, spec.Sema, spec.Tablo, ct);
    }

    // ---------- ARTIMLI YÜKLEME (S44) ----------

    private sealed record ArtimliSonuc(long Satir, bool ArtimliMi, string? YeniSeviye);

    private async Task<ArtimliSonuc> BirTabloArtimliAsync(string kaynakDizesi, string depoDizesi,
        IngestTableSpec spec, string? eskiSeviye, CancellationToken ct)
    {
        var kolonListesi = KolonListesi(spec);
        var kaynakTam = $"[{DepoYazimi.Kacir(spec.Sema)}].[{DepoYazimi.Kacir(spec.Tablo)}]";
        var artimliKolon = spec.ArtimliKolon!;

        await using var kaynak = new SqlConnection(kaynakDizesi);
        await kaynak.OpenAsync(ct);

        var kolonlar = await KaynakSemasiAsync(kaynak, kolonListesi, kaynakTam, ct);
        var artimliTip = kolonlar
            .FirstOrDefault(k => string.Equals(k.Ad, artimliKolon, StringComparison.OrdinalIgnoreCase))
            ?.Tip.ToLowerInvariant();
        var pk = await KaynakPkAsync(kaynak, spec, ct);

        // Yeni sınır KOPYADAN ÖNCE okunur: kopya sürerken gelen daha büyük değerler bu koşunun
        // değil SONRAKİ koşunun işidir (hedef kaçarsa pencere hiç kapanmaz).
        var yeniSinir = SuSeviyesi.Bicimle(await TekDegerAsync(kaynak,
            $"SELECT MAX([{DepoYazimi.Kacir(artimliKolon)}]) FROM {kaynakTam}", ct));

        // Artımlı yol mümkün mü? Değilse TAM yüklemeye düş ve yeni seviyeyi yine kaydet
        // (bir SONRAKİ koşu artımlı olabilsin diye).
        var kopyalananAdlar = kolonlar.Select(k => k.Ad).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dusmeNedeni =
            eskiSeviye is null ? "ilk koşu"
            : artimliTip is null ? "artımlı kolon seçilen kolonlar arasında değil"
            : pk.Count == 0 ? "kaynak tabloda PK yok"
            : !pk.All(kopyalananAdlar.Contains) ? "PK kolonları seçilen kolonlar arasında değil"
            : !await HedefKolonlarUyusuyorAsync(depoDizesi, spec.Sema, spec.Tablo, kopyalananAdlar, ct) ? "hedef tablo yok ya da kolonlar kaymış"
            : null;

        if (dusmeNedeni is not null)
        {
            var satir = await BirTabloTamAsync(kaynakDizesi, depoDizesi, spec, ct);
            return new ArtimliSonuc(satir, ArtimliMi: false, yeniSinir);
        }

        // Kaynak boş: taşınacak dilim yok; eski seviye korunur (null'a düşürmek sonraki koşuyu
        // gereksiz TAM yüklemeye çevirirdi).
        if (yeniSinir is null)
        {
            return new ArtimliSonuc(await HedefSatirSayAsync(depoDizesi, spec.Sema, spec.Tablo, ct),
                ArtimliMi: true, eskiSeviye);
        }

        // 1) Delta dilimini staging tablosuna kopyala (hedefle aynı DDL, indekssiz).
        var stagingAd = $"{spec.Tablo}__kds_delta";
        await DepodaCalistirAsync(depoDizesi, DepoYazimi.TabloDdl(spec.Sema, stagingAd, kolonlar), ct);

        var kosul = $"[{DepoYazimi.Kacir(artimliKolon)}] >= @eskiSeviye AND [{DepoYazimi.Kacir(artimliKolon)}] <= @yeniSinir";
        SqlParameter[] parametreler;
        try
        {
            parametreler =
            [
                SuSeviyesi.Parametre("@eskiSeviye", artimliTip!, eskiSeviye!),
                SuSeviyesi.Parametre("@yeniSinir", artimliTip!, yeniSinir),
            ];
        }
        catch (FormatException)
        {
            // Saklı seviye bu kolonun tipine çözülemiyor (kolon değişmiş / durum bozulmuş):
            // dar-tipli parse başarısızlığı sessiz veri kaybı riskidir → TAM yüklemeye düş.
            var satir = await BirTabloTamAsync(kaynakDizesi, depoDizesi, spec, ct);
            return new ArtimliSonuc(satir, ArtimliMi: false, yeniSinir);
        }

        await KaynaktanKopyalaAsync(kaynak, depoDizesi, kolonListesi, kaynakTam, spec.Sema, stagingAd,
            kosul, parametreler, ct);

        // 2) Staging → hedef MERGE (PK üzerinden; varsa güncelle, yoksa ekle) ve staging'i bırak.
        await DepodaCalistirAsync(depoDizesi,
            DepoYazimi.MergeUret(spec.Sema, spec.Tablo, stagingAd, kolonlar.Select(k => k.Ad).ToList(), pk), ct);
        await DepodaCalistirAsync(depoDizesi,
            $"DROP TABLE IF EXISTS [{DepoYazimi.Kacir(spec.Sema)}].[{DepoYazimi.Kacir(stagingAd)}]", ct);

        return new ArtimliSonuc(await HedefSatirSayAsync(depoDizesi, spec.Sema, spec.Tablo, ct),
            ArtimliMi: true, yeniSinir);
    }

    /// <summary>Kaynağın kolon şemasını nötr <see cref="DepoKolon"/> listesine çıkarır
    /// (satırsız SELECT + <c>GetColumnSchema</c>).</summary>
    private static async Task<IReadOnlyList<DepoKolon>> KaynakSemasiAsync(
        SqlConnection kaynak, string kolonListesi, string kaynakTam, CancellationToken ct)
    {
        await using var semaKomut = kaynak.CreateCommand();
        semaKomut.CommandText = $"SELECT {kolonListesi} FROM {kaynakTam} WHERE 1 = 0";
        await using var okuyucu = await semaKomut.ExecuteReaderAsync(ct);
        var kolonlar = await okuyucu.GetColumnSchemaAsync(ct);
        return [.. kolonlar.Select(Esle)];
    }

    private static DepoKolon Esle(DbColumn k) => new(
        k.ColumnName ?? "",
        (k.DataTypeName ?? "sql_variant").ToLowerInvariant(),
        k.ColumnSize,
        k.NumericPrecision is int p ? (byte)Math.Clamp(p, 0, 255) : null,
        k.NumericScale is int s and >= 0 and <= 255 ? (byte)s : null,
        k.AllowDBNull != false);

    /// <summary>Kaynak tablonun PK kolonları (sıralı). Boş liste = PK yok.</summary>
    private static async Task<IReadOnlyList<string>> KaynakPkAsync(
        SqlConnection kaynak, IngestTableSpec spec, CancellationToken ct)
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
        komut.Parameters.AddWithValue("@sema", spec.Sema);
        komut.Parameters.AddWithValue("@tablo", spec.Tablo);

        var liste = new List<string>();
        await using var okuyucu = await komut.ExecuteReaderAsync(ct);
        while (await okuyucu.ReadAsync(ct))
            liste.Add(okuyucu.GetString(0));
        return liste;
    }

    /// <summary>Hedef tablo var mı ve kolon ADLARI kopyalanan kümeyle birebir mi? (Ad kayması —
    /// eklenen/silinen/yeniden adlanan kolon — en yaygın şema kaymasıdır ve TAM yüklemeyle çözülür.
    /// Yalnız TİP değişimi ada dokunmaz; o durum MERGE/bulk-copy hatası olarak görünür ve
    /// kullanıcı "Tam Yükle" ile düzeltir — sessizce yanlış tipe dökmekten iyidir.)</summary>
    internal static async Task<bool> HedefKolonlarUyusuyorAsync(string depoDizesi,
        string sema, string tablo, IReadOnlySet<string> kopyalananAdlar, CancellationToken ct)
    {
        await using var depo = new SqlConnection(depoDizesi);
        await depo.OpenAsync(ct);
        await using var komut = depo.CreateCommand();
        komut.CommandText = "SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@tam)";
        komut.Parameters.AddWithValue("@tam", $"[{DepoYazimi.Kacir(sema)}].[{DepoYazimi.Kacir(tablo)}]");

        var hedef = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var okuyucu = await komut.ExecuteReaderAsync(ct);
        while (await okuyucu.ReadAsync(ct))
            hedef.Add(okuyucu.GetString(0));
        return hedef.Count > 0 && hedef.SetEquals(kopyalananAdlar);
    }

    private static async Task<object?> TekDegerAsync(SqlConnection baglanti, string sql, CancellationToken ct)
    {
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sql;
        komut.CommandTimeout = 0;
        return await komut.ExecuteScalarAsync(ct);
    }

    internal static async Task DepodaCalistirAsync(string depoDizesi, string sql, CancellationToken ct)
    {
        await using var depo = new SqlConnection(depoDizesi);
        await depo.OpenAsync(ct);
        await using var komut = depo.CreateCommand();
        komut.CommandText = sql;
        komut.CommandTimeout = 0; // büyük tabloda MERGE/indeks inşası uzun sürebilir
        await komut.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Kaynaktan (isteğe bağlı WHERE dilimiyle) okuyup depodaki hedef tabloya bulk-copy.</summary>
    private static async Task KaynaktanKopyalaAsync(SqlConnection kaynak, string depoDizesi,
        string kolonListesi, string kaynakTam, string hedefSema, string hedefTablo,
        string? kosul, SqlParameter[]? parametreler, CancellationToken ct)
    {
        await using var okuKomut = kaynak.CreateCommand();
        okuKomut.CommandText = $"SELECT {kolonListesi} FROM {kaynakTam}"
            + (kosul is null ? "" : $" WHERE {kosul}");
        okuKomut.CommandTimeout = 0; // büyük tablo
        if (parametreler is not null)
            okuKomut.Parameters.AddRange(parametreler);
        await using var okuyucu = await okuKomut.ExecuteReaderAsync(ct);

        await using var depo = new SqlConnection(depoDizesi);
        await depo.OpenAsync(ct);
        using var kopyala = new SqlBulkCopy(depo)
        {
            DestinationTableName = $"[{DepoYazimi.Kacir(hedefSema)}].[{DepoYazimi.Kacir(hedefTablo)}]",
            BulkCopyTimeout = 0,
            BatchSize = 10_000,
        };
        for (var i = 0; i < okuyucu.FieldCount; i++)
            kopyala.ColumnMappings.Add(okuyucu.GetName(i), okuyucu.GetName(i));
        await kopyala.WriteToServerAsync(okuyucu, ct);
    }

    internal static async Task<long> HedefSatirSayAsync(string depoDizesi, string sema, string tablo,
        CancellationToken ct)
    {
        await using var depo = new SqlConnection(depoDizesi);
        await depo.OpenAsync(ct);
        await using var say = depo.CreateCommand();
        say.CommandText = $"SELECT COUNT_BIG(*) FROM [{DepoYazimi.Kacir(sema)}].[{DepoYazimi.Kacir(tablo)}]";
        return (long)(await say.ExecuteScalarAsync(ct) ?? 0L);
    }

    private static string KolonListesi(IngestTableSpec spec) =>
        spec.Kolonlar is { Count: > 0 }
            ? string.Join(", ", spec.Kolonlar.Select(k => $"[{DepoYazimi.Kacir(k)}]"))
            : "*";
}
