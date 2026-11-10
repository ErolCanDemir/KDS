using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kds.Contracts.DataMaps;
using Kds.Contracts.Metrics;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using YamlDotNet.RepresentationModel;

namespace Kds.Business.Metrics;

/* S46 — OSI (Open Semantic Interchange / Apache Ossie) UYUMU: semantik katmanın satıcıdan
 * bağımsız değişimi (rakip analizi §5 — "kapalı ada" riskinden kaçınma). Biçim, OSI çekirdek
 * spesifikasyonundan (v0.1, Oca 2026'da Apache 2.0 ile yayımlandı; ASF kuluçkasında):
 *
 *   version: <spec sürümü>
 *   semantic_model:
 *     - name/description + datasets[] + relationships[] + metrics[] + custom_extensions[]
 *
 * Eşleme: Veri Haritası = semantic_model (tablolar=datasets, ilişkiler=relationships),
 * Metrikler = metrics. Metrik ifadesi ANSI_SQL lehçesiyle yazılır (Y1'in yer tutucuları
 * açılmış hâli) ve ORİJİNAL Y1 + biçim, spec'in resmi uzantı mekanizmasıyla
 * (custom_extensions: vendor_name=LST_KDS) kayıpsız taşınır — KDS→KDS round-trip birebirdir,
 * başka ürünler ANSI ifadesini okur. */

/// <summary>OSI YAML dışa aktarımı. YAML ELLE üretilir (yalnız yazım — parse değil): çıktı
/// yüzeyi dar ve deterministiktir, kaçış tek fonksiyonda toplanır.</summary>
public sealed class ExportOsiMetricsHandler(KdsDbContext context, DataMaps.IDataMapReader haritalar)
    : IRequestHandler<ExportOsiMetricsRequest, string>
{
    public async Task<string> Handle(ExportOsiMetricsRequest request, CancellationToken cancellationToken)
    {
        var haritaIdler = await context.Haritalar.AsNoTracking()
            .Where(h => request.VeriHaritasiId == null || h.Id == request.VeriHaritasiId)
            .OrderBy(h => h.Ad)
            .Select(h => h.Id)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("# OSI (Open Semantic Interchange / Apache Ossie) — LST-KDS dışa aktarımı");
        sb.AppendLine("version: 0.1.0");
        sb.AppendLine("semantic_model:");

        foreach (var haritaId in haritaIdler)
        {
            var harita = await haritalar.GetDetailAsync(haritaId, cancellationToken);
            if (harita is null)
            {
                continue;
            }

            var metrikler = await context.Metrikler.AsNoTracking()
                .Where(m => m.VeriHaritasiId == haritaId)
                .OrderBy(m => m.Ad)
                .ToListAsync(cancellationToken);
            ModelYaz(sb, harita, metrikler);
        }

        return sb.ToString();
    }

    private static void ModelYaz(StringBuilder sb, DataMapDetail harita,
        IReadOnlyList<Data.Entities.Metric> metrikler)
    {
        var tabloAdi = harita.Tablolar.ToDictionary(t => t.Id, t => t.DostcaAd);
        sb.AppendLine($"  - name: {Y(harita.Ad)}");

        sb.AppendLine("    datasets:");
        foreach (var t in harita.Tablolar)
        {
            sb.AppendLine($"      - name: {Y(t.DostcaAd)}");
            sb.AppendLine($"        source: {Y($"{t.Sema}.{t.HamAd}")}");
            /* primary_key BİLEREK YAZILMAZ: haritadaki "Anahtar" rolü PK+FK karışımıdır ("join'de
             * kullanılır"), gerçek PK değil. Yanlış PK beyanı alıcı üründe join/teklik çıkarımını
             * SESSİZCE bozar; elde olmayan bilgiyi beyan etmemek doğrusu (ilişkiler zaten
             * relationships bölümünde açık). */
            sb.AppendLine("        fields:");
            foreach (var k in t.Kolonlar.Where(k => k.Rol != ColumnRole.Gizli))
            {
                sb.AppendLine($"          - name: {Y(string.IsNullOrWhiteSpace(k.DostcaAd) ? k.HamAd : k.DostcaAd!)}");
                if (!string.IsNullOrWhiteSpace(k.DostcaAd) && k.DostcaAd != k.HamAd)
                {
                    sb.AppendLine($"            description: {Y($"kaynak kolon: {k.HamAd}")}");
                }
            }
        }

        if (harita.Iliskiler.Count > 0)
        {
            sb.AppendLine("    relationships:");
            foreach (var i in harita.Iliskiler)
            {
                if (!tabloAdi.TryGetValue(i.KaynakTabloId, out var kaynak)
                    || !tabloAdi.TryGetValue(i.HedefTabloId, out var hedef))
                {
                    continue;   // öksüz ilişki (tablo haritadan çıkarılmış) — dosyayı bozmasın
                }

                sb.AppendLine($"      - name: {Y($"{kaynak}_{i.KaynakKolon}__{hedef}_{i.HedefKolon}")}");
                sb.AppendLine($"        from: {Y(kaynak)}");
                sb.AppendLine($"        to: {Y(hedef)}");
                sb.AppendLine($"        from_columns: [{Y(i.KaynakKolon)}]");
                sb.AppendLine($"        to_columns: [{Y(i.HedefKolon)}]");
            }
        }

        if (metrikler.Count > 0)
        {
            sb.AppendLine("    metrics:");
            foreach (var m in metrikler)
            {
                sb.AppendLine($"      - name: {Y(m.Ad)}");
                if (!string.IsNullOrWhiteSpace(m.Aciklama))
                {
                    sb.AppendLine($"        description: {Y(m.Aciklama!)}");
                }

                sb.AppendLine("        expression:");
                sb.AppendLine("          dialects:");
                sb.AppendLine("            - dialect: ANSI_SQL");
                sb.AppendLine($"              expression: {Y(OsiIfade.Y1denAnsi(m.Ifade))}");
                // Orijinal Y1 + biçim, spec'in resmi uzantısıyla: KDS→KDS round-trip kayıpsız.
                var veri = JsonSerializer.Serialize(new { y1 = m.Ifade, bicim = m.Bicim });
                sb.AppendLine("        custom_extensions:");
                sb.AppendLine("          - vendor_name: LST_KDS");
                sb.AppendLine($"            data: {Y(veri)}");
            }
        }
    }

    /// <summary>YAML skaler kaçışı: her değer çift tırnaklı yazılır (tırnak ve ters bölü
    /// kaçırılır) — "hangi karakter tırnak ister" kural listesiyle uğraşmaktan güvenlidir.</summary>
    private static string Y(string deger) =>
        "\"" + deger.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
}

/// <summary>OSI YAML içe alımı: her <c>semantic_model</c> öğesi ADIYLA haritaya eşlenir (S30
/// deseni — GUID kiracıya özeldir, ad taşınır), metrikler normal <c>SaveMetric</c> yolundan
/// geçer (formül üreticiyle doğrulanır, güncelleme sürüm geçmişine yazılır). LST_KDS uzantısı
/// varsa Y1 aynen alınır (kayıpsız); yoksa ANSI ifadesi Y1'e çevrilmeyi denenir — çevrilemeyen
/// öğe, S30 sözleşmesi gereği yalnız KENDİSİ hata alır. YAML ayrıştırması dar-tipli parse
/// (AGENTS §4.6 izinli bölge).</summary>
public sealed class ImportOsiMetricsHandler(KdsDbContext context, ISender gonderici)
    : IRequestHandler<ImportOsiMetricsRequest, IReadOnlyList<MetricImportItemResult>>
{
    public async Task<IReadOnlyList<MetricImportItemResult>> Handle(ImportOsiMetricsRequest request, CancellationToken cancellationToken)
    {
        YamlMappingNode kok;
        try
        {
            var akis = new YamlStream();
            akis.Load(new StringReader(request.Yaml));
            kok = akis.Documents.Count > 0 && akis.Documents[0].RootNode is YamlMappingNode m
                ? m
                : throw new YamlDotNet.Core.YamlException("kök nesne yok");
        }
        catch (YamlDotNet.Core.YamlException hata)
        {
            return [new MetricImportItemResult("(dosya)", MetrikIceAlmaSonucu.Hata,
                $"YAML okunamadı: {hata.Message}")];
        }

        if (Al(kok, "semantic_model") is not YamlSequenceNode modeller)
        {
            return [new MetricImportItemResult("(dosya)", MetrikIceAlmaSonucu.Hata,
                "Dosyada 'semantic_model' listesi yok — bu bir OSI semantik model dosyası değil.")];
        }

        var sonuclar = new List<MetricImportItemResult>();
        foreach (var modelNode in modeller.OfType<YamlMappingNode>())
        {
            sonuclar.AddRange(await ModelAlAsync(modelNode, request.CakismadaGuncelle, cancellationToken));
        }

        if (sonuclar.Count == 0)
        {
            sonuclar.Add(new MetricImportItemResult("(dosya)", MetrikIceAlmaSonucu.Hata,
                "Dosyada içe alınacak metrik bulunamadı."));
        }

        return sonuclar;
    }

    private async Task<List<MetricImportItemResult>> ModelAlAsync(
        YamlMappingNode model, bool guncelle, CancellationToken ct)
    {
        var sonuclar = new List<MetricImportItemResult>();
        var haritaAd = Metin(Al(model, "name")) ?? "";

        var metrikler = Al(model, "metrics") as YamlSequenceNode;
        if (metrikler is null || !metrikler.Children.OfType<YamlMappingNode>().Any())
        {
            return sonuclar;   // metriksiz model (yalnız dataset taşıyan dosya) — sessizce boş
        }

        var haritaId = await context.Haritalar.AsNoTracking()
            .Where(h => h.Ad == haritaAd)
            .Select(h => (Guid?)h.Id)
            .FirstOrDefaultAsync(ct);
        if (haritaId is null)
        {
            sonuclar.Add(new MetricImportItemResult(haritaAd.Length > 0 ? haritaAd : "(adsız model)",
                MetrikIceAlmaSonucu.Hata,
                $"'{haritaAd}' adlı veri haritası bu kurulumda yok — önce haritayı kurun."));
            return sonuclar;
        }

        foreach (var metrik in metrikler.Children.OfType<YamlMappingNode>())
        {
            sonuclar.Add(await MetrikAlAsync(metrik, haritaId.Value, guncelle, ct));
        }

        return sonuclar;
    }

    private async Task<MetricImportItemResult> MetrikAlAsync(
        YamlMappingNode metrik, Guid haritaId, bool guncelle, CancellationToken ct)
    {
        var ad = Metin(Al(metrik, "name"))?.Trim() ?? "";
        if (ad.Length == 0)
        {
            return new MetricImportItemResult("(adsız)", MetrikIceAlmaSonucu.Hata, "Metrik adı boş.");
        }

        // 1) Y1 kaynağı: LST_KDS uzantısı (kayıpsız yol) → yoksa ANSI_SQL lehçesinden çeviri.
        var (y1, bicim) = Uzantidan(metrik);
        if (y1 is null)
        {
            var ansi = AnsiIfade(metrik);
            if (ansi is null)
            {
                return new MetricImportItemResult(ad, MetrikIceAlmaSonucu.Hata,
                    "Metrikte ANSI_SQL lehçesinde bir ifade yok (KDS bu lehçeyi okur).");
            }

            y1 = OsiIfade.AnsidenY1(ansi);
        }

        var mevcutId = await context.Metrikler.AsNoTracking()
            .Where(m => m.Ad == ad)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);
        if (mevcutId is not null && !guncelle)
        {
            return new MetricImportItemResult(ad, MetrikIceAlmaSonucu.Atlandi,
                "Aynı adla metrik zaten var (güncelleme kapalı).");
        }

        try
        {
            await gonderici.Send(new SaveMetricRequest(
                mevcutId, ad, haritaId, y1, bicim, Metin(Al(metrik, "description"))), ct);
        }
        catch (Exception hata) when (hata is FluentValidation.ValidationException
            or Execution.QueryModelException or KeyNotFoundException)
        {
            return new MetricImportItemResult(ad, MetrikIceAlmaSonucu.Hata, hata.Message);
        }

        return new MetricImportItemResult(ad,
            mevcutId is null ? MetrikIceAlmaSonucu.Eklendi : MetrikIceAlmaSonucu.Guncellendi, null);
    }

    /// <summary>LST_KDS custom_extensions verisi: {"y1": "...", "bicim": "Para"} — bozuksa
    /// (elle kurcalanmış dosya) yok sayılır ve ANSI yoluna düşülür.</summary>
    private static (string? Y1, MetrikBicimi Bicim) Uzantidan(YamlMappingNode metrik)
    {
        if (Al(metrik, "custom_extensions") is not YamlSequenceNode uzantilar)
        {
            return (null, MetrikBicimi.Sayi);
        }

        foreach (var u in uzantilar.OfType<YamlMappingNode>())
        {
            if (!string.Equals(Metin(Al(u, "vendor_name")), "LST_KDS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var veri = JsonDocument.Parse(Metin(Al(u, "data")) ?? "");
                var y1 = veri.RootElement.TryGetProperty("y1", out var e) ? e.GetString() : null;
                var bicim = veri.RootElement.TryGetProperty("bicim", out var b)
                    && Enum.TryParse<MetrikBicimi>(b.GetString(), out var cozulen)
                        ? cozulen
                        : MetrikBicimi.Sayi;
                return (string.IsNullOrWhiteSpace(y1) ? null : y1, bicim);
            }
            catch (JsonException)
            {
                return (null, MetrikBicimi.Sayi);
            }
        }

        return (null, MetrikBicimi.Sayi);
    }

    private static string? AnsiIfade(YamlMappingNode metrik)
    {
        if (Al(metrik, "expression") is not YamlMappingNode ifade
            || Al(ifade, "dialects") is not YamlSequenceNode lehceler)
        {
            return null;
        }

        foreach (var l in lehceler.OfType<YamlMappingNode>())
        {
            if (string.Equals(Metin(Al(l, "dialect")), "ANSI_SQL", StringComparison.OrdinalIgnoreCase))
            {
                return Metin(Al(l, "expression"));
            }
        }

        return null;
    }

    private static YamlNode? Al(YamlMappingNode harita, string anahtar) =>
        harita.Children.TryGetValue(new YamlScalarNode(anahtar), out var deger) ? deger : null;

    private static string? Metin(YamlNode? dugum) => (dugum as YamlScalarNode)?.Value;
}

/// <summary>Y1 ↔ ANSI SQL ifade dönüşümü. Y1 zaten SQL'e çok yakındır; tek yapısal fark kolon
/// referansının <c>{Tablo.Kolon}</c> yer tutucusunda olmasıdır. Tek-tırnaklı metin sabitleri her
/// iki yönde de DOKUNULMADAN geçirilir (içleri veri, kod değil — IfadeDenetimi'ndeki kural).</summary>
public static class OsiIfade
{
    /// <summary>Y1 → ANSI: <c>SUM({Odemeler.Tutar})</c> → <c>SUM(Odemeler.Tutar)</c>.
    /// Adlar haritanın DOSTÇA adlarıdır — dışa aktarımın dataset adlarıyla aynı ad uzayı.</summary>
    public static string Y1denAnsi(string y1) =>
        LiteralKoruyarak(y1, parca => Regex.Replace(parca, @"\{([^{}]+)\}", "$1"));

    /// <summary>ANSI → Y1: <c>Tablo.Kolon</c> çiftleri yer tutucuya alınır. Çevrilemeyen yapı
    /// (tablosuz kolon adı vb.) olduğu gibi kalır — SaveMetric üretici doğrulaması yakalar ve
    /// öğe, açıklayıcı hatayla düşer (sessiz yanlış tanım kaydedilmez).</summary>
    public static string AnsidenY1(string ansi) =>
        LiteralKoruyarak(ansi, parca => Regex.Replace(parca,
            @"(?<![\w{.])([\p{L}_][\w]*)\.([\p{L}_][\w]*)(?![\w.(])", "{$1.$2}"));

    /// <summary>Metni tek-tırnaklı literallere göre bölüp dönüşümü yalnız KOD parçalarına uygular.</summary>
    private static string LiteralKoruyarak(string metin, Func<string, string> donustur)
    {
        var sb = new StringBuilder(metin.Length + 16);
        var i = 0;
        foreach (Match literal in Regex.Matches(metin, @"'([^']|'')*'"))
        {
            sb.Append(donustur(metin[i..literal.Index]));
            sb.Append(literal.Value);
            i = literal.Index + literal.Length;
        }

        sb.Append(donustur(metin[i..]));
        return sb.ToString();
    }
}
