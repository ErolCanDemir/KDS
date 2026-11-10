using System.Data;
using Microsoft.Data.SqlClient;

namespace Kds.Execution;

/// <summary>S45 — ajanın İTTİĞİ veriyi tenant deposuna yazan sunucu tarafı. Saf ADO.NET;
/// Kds.Data'a referans yok. DDL/MERGE üretimi <see cref="DepoYazimi"/> ortak çekirdeğinden —
/// doğrudan içe alımla (SqlIngestRunner) aynı depo şeması, aynı staging adı, aynı MERGE.</summary>
public interface IAjanDepoYazici
{
    /// <summary>Varsayılan depo DB'yi yoksa yaratır (M22b hedef bağlantı seçiliyse çağrılmaz).</summary>
    Task DepoyuHazirlaAsync(string sunucuDizesi, string dbAdi, CancellationToken ct);

    /// <summary>Tablo yüklemesini hazırlar; GERÇEKLEŞEN modu döner: artımlı istendi ama hedef
    /// uygun değilse (tablo yok / kolon adları kaymış) "Tam"a düşürülür — ajan dönen moda uyar.</summary>
    Task<string> TabloBaslaAsync(string depoDizesi, string sema, string tablo,
        IReadOnlyList<DepoKolon> kolonlar, string mod, CancellationToken ct);

    /// <summary>Bir satır partisini hedefe (tam) ya da staging'e (artımlı) bulk-copy eder.</summary>
    Task SatirlarYazAsync(string depoDizesi, string sema, string hedefTablo,
        IReadOnlyList<DepoKolon> kolonlar, IReadOnlyList<object?[]> satirlar, CancellationToken ct);

    /// <summary>Tabloyu kapatır: Artimli → MERGE + staging düşer; Tam → indeksler.
    /// Hedefteki satır sayısı döner.</summary>
    Task<long> TabloBitirAsync(string depoDizesi, string sema, string tablo, string mod,
        IReadOnlyList<DepoKolon> kolonlar, IReadOnlyList<string> pk, CancellationToken ct);
}

public sealed class AjanDepoYazici : IAjanDepoYazici
{
    /// <summary>Staging adı — SqlIngestRunner'la AYNI desen (iki yol aynı tabloyu paylaşabilsin).</summary>
    private static string StagingAd(string tablo) => $"{tablo}__kds_delta";

    public Task DepoyuHazirlaAsync(string sunucuDizesi, string dbAdi, CancellationToken ct) =>
        SqlIngestRunner.DepoyuYaratAsync(sunucuDizesi, dbAdi, ct);

    public async Task<string> TabloBaslaAsync(string depoDizesi, string sema, string tablo,
        IReadOnlyList<DepoKolon> kolonlar, string mod, CancellationToken ct)
    {
        if (mod == "Artimli")
        {
            var adlar = kolonlar.Select(k => k.Ad).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (await SqlIngestRunner.HedefKolonlarUyusuyorAsync(depoDizesi, sema, tablo, adlar, ct))
            {
                await SqlIngestRunner.DepodaCalistirAsync(depoDizesi,
                    DepoYazimi.TabloDdl(sema, StagingAd(tablo), kolonlar), ct);
                return "Artimli";
            }

            // Hedef yok ya da kolonlar kaymış → TAM'a düş (S44'teki kuralın push karşılığı).
            mod = "Tam";
        }

        await SqlIngestRunner.DepodaCalistirAsync(depoDizesi, DepoYazimi.TabloDdl(sema, tablo, kolonlar), ct);
        return mod;
    }

    public async Task SatirlarYazAsync(string depoDizesi, string sema, string hedefTablo,
        IReadOnlyList<DepoKolon> kolonlar, IReadOnlyList<object?[]> satirlar, CancellationToken ct)
    {
        // DataTable kolonları bildirilen tipe göre KURULUR (AjanVeri.ClrTipi) — SqlBulkCopy hedef
        // tipe güvenli çevirir; her şeyi metin taşımak örtük dönüşüm hatalarını koşu ortasına atardı.
        using var veri = new DataTable();
        foreach (var k in kolonlar)
        {
            veri.Columns.Add(k.Ad, AjanVeri.ClrTipi(k.Tip));
        }

        foreach (var satir in satirlar)
        {
            var hedef = veri.NewRow();
            for (var i = 0; i < kolonlar.Count && i < satir.Length; i++)
            {
                hedef[i] = satir[i] ?? DBNull.Value;
            }

            veri.Rows.Add(hedef);
        }

        await using var depo = new SqlConnection(depoDizesi);
        await depo.OpenAsync(ct);
        using var kopyala = new SqlBulkCopy(depo)
        {
            DestinationTableName = $"[{DepoYazimi.Kacir(sema)}].[{DepoYazimi.Kacir(hedefTablo)}]",
            BulkCopyTimeout = 0,
            BatchSize = 10_000,
        };
        foreach (var k in kolonlar)
        {
            kopyala.ColumnMappings.Add(k.Ad, k.Ad);
        }

        await kopyala.WriteToServerAsync(veri, ct);
    }

    public async Task<long> TabloBitirAsync(string depoDizesi, string sema, string tablo, string mod,
        IReadOnlyList<DepoKolon> kolonlar, IReadOnlyList<string> pk, CancellationToken ct)
    {
        if (mod == "Artimli")
        {
            await SqlIngestRunner.DepodaCalistirAsync(depoDizesi,
                DepoYazimi.MergeUret(sema, tablo, StagingAd(tablo), kolonlar.Select(k => k.Ad).ToList(), pk), ct);
            await SqlIngestRunner.DepodaCalistirAsync(depoDizesi,
                $"DROP TABLE IF EXISTS [{DepoYazimi.Kacir(sema)}].[{DepoYazimi.Kacir(StagingAd(tablo))}]", ct);
        }
        else
        {
            // Tam yüklemede indeksler yükleme bitince kurulur (S12'deki ölçülü karar);
            // başarısızlık koşuyu düşürmez — veri sağlam, indeks hızdır.
            foreach (var indeksDdl in DepoYazimi.IndeksDdlUret(sema, tablo, kolonlar))
            {
                try
                {
                    await SqlIngestRunner.DepodaCalistirAsync(depoDizesi, indeksDdl, ct);
                }
                catch (SqlException)
                {
                    // bilinçli: indeks kurulamadıysa yalnız sorgular yavaş kalır
                }
            }
        }

        return await SqlIngestRunner.HedefSatirSayAsync(depoDizesi, sema, tablo, ct);
    }
}
