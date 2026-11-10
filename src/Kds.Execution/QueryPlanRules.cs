using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Kds.Contracts.Queries;

namespace Kds.Execution;

/// <summary>M33 tanı KURAL MOTORU — saf: plan XML'i (ve plan yoksa SQL metni) → Türkçe bulgular.
/// IO yok (bağlantı/plan çekme <see cref="SqlQueryDiagnostician"/>'da); böylece kurallar canlı DB
/// olmadan birim test edilebilir.</summary>
public static class QueryPlanRules
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";
    // S16/A3: eşikler Sistem Ayarları'ndan gelir; buradakiler yalnız ARAYAN VERMEZSE geçerli
    // olan makul varsayılanlardır (ortama bağlı değer değil, ürün varsayılanı).
    public const double VarsayilanTaramaEsigi = 500_000;
    public const double VarsayilanSiralamaEsigi = 100_000;

    /// <summary>Plan XML'inden tanı: bulgular şiddet sırasına dizilir, özet en şiddetliden türer.
    /// Eşikler çağıran tarafından (ayarlardan) verilebilir.</summary>
    public static QueryDiagnosis PlandanTani(
        string planXml,
        double buyukTaramaSatirEsigi = VarsayilanTaramaEsigi,
        double buyukSiralamaSatirEsigi = VarsayilanSiralamaEsigi)
    {
        var plan = XDocument.Parse(planXml);
        var bulgular = new List<DiagnoseFinding>();

        // 1) Eksik index'ler (en değerli sinyal — SQL Server önerisiyle birlikte gelir).
        foreach (var grup in plan.Descendants(Ns + "MissingIndexGroup"))
        {
            var etki = grup.Attribute("Impact")?.Value;
            foreach (var eksik in grup.Elements(Ns + "MissingIndex"))
            {
                var db = eksik.Attribute("Database")?.Value ?? "";
                var sema = eksik.Attribute("Schema")?.Value ?? "";
                var tablo = eksik.Attribute("Table")?.Value ?? "?";
                string Kolonlar(string kullanim) => string.Join(", ",
                    eksik.Elements(Ns + "ColumnGroup").Where(c => c.Attribute("Usage")?.Value == kullanim)
                        .SelectMany(c => c.Elements(Ns + "Column")).Select(c => c.Attribute("Name")?.Value));
                var anahtar = string.Join(", ", new[] { Kolonlar("EQUALITY"), Kolonlar("INEQUALITY") }
                    .Where(s => s.Length > 0));
                var dahil = Kolonlar("INCLUDE");
                var ddl = $"CREATE INDEX [IX_Oneri_{tablo.Trim('[', ']')}] ON {db}.{sema}.{tablo} ({anahtar})"
                    + (dahil.Length > 0 ? $" INCLUDE ({dahil})" : "") + ";";
                bulgular.Add(new DiagnoseFinding(
                    $"Eksik index: {sema}.{tablo}",
                    $"SQL Server bu sorgu için {sema}.{tablo} tablosunda index eksik olduğunu bildiriyor" +
                    (etki is not null ? $" (tahmini etki ~%{etki.Split('.')[0]})" : "") + ".",
                    "yuksek",
                    "Aşağıdaki index önerisini DBA'ya iletin — KDS kaynak veritabanına kendisi yazmaz.",
                    ddl));
            }
        }

        // 2) Büyük tablo taraması (columnstore taramaları BI'da normaldir, sayılmaz).
        // Ölçüt OKUNAN satırdır (EstimatedRowsRead): filtre çoğu satırı elese bile tarama maliyeti
        // okunan satır kadardır — EstimateRows (ÇIKAN satır) burada yanıltıcıdır.
        foreach (var op in plan.Descendants(Ns + "RelOp"))
        {
            var fiziksel = op.Attribute("PhysicalOp")?.Value;
            if (fiziksel is not ("Table Scan" or "Clustered Index Scan" or "Index Scan")) continue;
            var okunan = Sayi(op.Attribute("EstimatedRowsRead")) ?? Sayi(op.Attribute("EstimateRows"));
            if (okunan is not double satir || satir < buyukTaramaSatirEsigi) continue;
            var nesne = op.Descendants(Ns + "Object").FirstOrDefault();
            if (nesne?.Attribute("Storage")?.Value == "ColumnStore") continue;
            var tablo = $"{nesne?.Attribute("Schema")?.Value}.{nesne?.Attribute("Table")?.Value}";
            bulgular.Add(new DiagnoseFinding(
                $"Büyük tablo taranıyor: {tablo}",
                $"{tablo} tablosu index kullanılmadan taranıyor — tahmini ~{satir:N0} satır okunuyor.",
                "orta",
                "Filtre kolonlarına index ekletmeyi ya da sorguya daraltıcı filtre (ör. tarih aralığı) eklemeyi deneyin."));
        }

        // 3) TOP + büyük Sort birleşimi — row-goal tuzağı (test turu M7'de kanıtlandı: 182sn → 7.5sn).
        var topVar = plan.Descendants(Ns + "RelOp").Any(o => o.Attribute("PhysicalOp")?.Value == "Top");
        var buyukSort = plan.Descendants(Ns + "RelOp").Any(o =>
            o.Attribute("PhysicalOp")?.Value == "Sort" && Sayi(o.Attribute("EstimateRows")) >= buyukSiralamaSatirEsigi);
        if (topVar && buyukSort)
            bulgular.Add(new DiagnoseFinding(
                "Satır limiti + sıralama birleşimi (row-goal)",
                "Sorguda satır limiti (TOP) ile büyük bir sıralama bir arada — SQL Server bu birleşimde " +
                "bazen çok yavaş bir plan seçer.",
                "orta",
                "Ham SQL'de sorgunun sonuna aşağıdaki ipucunu ekleyip deneyin; tasarımcıda sıralamayı kaldırıp " +
                "ya da satır limitini artırıp/azaltıp fark edip etmediğine bakın.",
                "OPTION (USE HINT('DISABLE_OPTIMIZER_ROWGOAL'))"));

        // 4) Plan uyarıları: koşulsuz join (kartezyen) + index'i devre dışı bırakan örtük tip dönüşümü.
        if (plan.Descendants(Ns + "Warnings").Any(w => w.Attribute("NoJoinPredicate")?.Value is "1" or "true"))
            bulgular.Add(new DiagnoseFinding(
                "Koşulsuz tablo birleşimi (kartezyen çarpım)",
                "İki tablo aralarında koşul olmadan birleşiyor — satır sayısı çarpılarak patlar.",
                "yuksek",
                "Join koşullarını kontrol edin: tasarımcıda Join Yolları adımına, ham SQL'de ON satırlarına bakın."));
        if (plan.Descendants(Ns + "PlanAffectingConvert")
                .Any(c => c.Attribute("ConvertIssue")?.Value?.Contains("Seek") == true))
            bulgular.Add(new DiagnoseFinding(
                "Örtük tip dönüşümü index'i devre dışı bırakıyor",
                "Bir filtre/join kolonu, karşılaştırılan değerle farklı tipte — SQL Server dönüşüm yüzünden " +
                "index'i kullanamıyor.",
                "orta",
                "Karşılaştırılan değerin tipini kolonla eşleyin (ör. metin kolona sayı vermeyin)."));

        var siraDegeri = (string s) => s switch { "yuksek" => 0, "orta" => 1, _ => 2 };
        bulgular.Sort((a, b) => siraDegeri(a.Siddet).CompareTo(siraDegeri(b.Siddet)));

        var ozet = bulgular.Count == 0
            ? "Planda bariz bir sorun görünmüyor — yavaşlık sunucu yükünden ya da soğuk önbellekten olabilir; tekrar deneyin."
            : bulgular[0].Siddet == "yuksek" && bulgular[0].Baslik.StartsWith("Eksik index")
                ? "Sorun büyük olasılıkla veritabanında: eksik index. Öneriyi DBA'ya iletin."
                : $"Öne çıkan bulgu: {bulgular[0].Baslik}.";
        return new QueryDiagnosis(ozet, bulgular);
    }

    /// <summary>Plan YOKKEN (yetki yok / boş plan) SQL metninden genel ipuçları — kesin teşhis
    /// değildir, hepsi "bilgi" şiddetindedir.</summary>
    public static List<DiagnoseFinding> MetindenIpuclari(string sql)
    {
        var bulgular = new List<DiagnoseFinding>();
        var duz = Regex.Replace(sql, @"\s+", " ");

        if (Regex.IsMatch(duz, @"\bTOP\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(duz, @"\bORDER\s+BY\b", RegexOptions.IgnoreCase))
            bulgular.Add(new DiagnoseFinding("Satır limiti + sıralama bir arada",
                "TOP ile ORDER BY birleşimi bazı planlarda çok yavaşlar (row-goal).", "bilgi",
                "Sorgunun sonuna deneme amaçlı ekleyin:", "OPTION (USE HINT('DISABLE_OPTIMIZER_ROWGOAL'))"));
        if (Regex.IsMatch(duz, @"LIKE\s+N?'%", RegexOptions.IgnoreCase))
            bulgular.Add(new DiagnoseFinding("Başında joker olan LIKE",
                "LIKE '%…' deseni index kullanamaz; tablo taranır.", "bilgi",
                "Mümkünse joker karakteri başa değil sona koyun (LIKE 'değer%')."));
        if (!Regex.IsMatch(duz, @"\bWHERE\b", RegexOptions.IgnoreCase))
            bulgular.Add(new DiagnoseFinding("Filtresiz sorgu",
                "WHERE olmadan tablolar komple okunur.", "bilgi",
                "Daraltıcı bir filtre (ör. tarih aralığı) eklemeyi deneyin."));
        var joinSayisi = Regex.Matches(duz, @"\bJOIN\b", RegexOptions.IgnoreCase).Count;
        if (joinSayisi >= 5)
            bulgular.Add(new DiagnoseFinding($"Çok tablolu birleşim ({joinSayisi} JOIN)",
                "Join sayısı arttıkça plan seçimi zorlaşır ve maliyet büyür.", "bilgi",
                "Kullanılmayan tabloları sorgudan çıkarın; ara sonuçları daraltın."));
        bulgular.Add(new DiagnoseFinding("Satır limitini düşürerek deneyin",
            "Sorunun sorguda mı veri hacminde mi olduğunu ayırt etmek için satır limitini küçültüp yeniden koşun.",
            "bilgi"));
        return bulgular;
    }

    /// <summary>Plan sayısal özniteliği (kültürden bağımsız); öznitelik yoksa/çözülemezse null.</summary>
    private static double? Sayi(XAttribute? oznitelik) =>
        double.TryParse(oznitelik?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
}
