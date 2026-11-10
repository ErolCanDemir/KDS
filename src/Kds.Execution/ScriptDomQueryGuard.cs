using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Kds.Execution;

/// <summary>ScriptDom (resmî T-SQL ayrıştırıcı, MIT) ile SELECT-only denetimi. AST tabanlı —
/// string/regex kontrolü değil (00-analiz §7).
/// <para>KABUL: tek batch + tek ifade + o ifade INTO'suz bir SELECT.</para>
/// <para>RED: ayrıştırma hatası · çoklu ifade · SELECT dışı ifade (INSERT/UPDATE/DELETE/DDL/
/// EXEC…) · SELECT ... INTO (tablo oluşturur) · ve tablo kaynağı BEYAZ-listede olmayan her şey
/// (OPENROWSET/OPENQUERY/OPENDATASOURCE, tablo-değerli fonksiyon örn. <c>fn_get_audit_file</c>/
/// <c>fn_trace_gettable</c> = dosya/UNC okuma, iç içe DML…).</para>
/// <para>NOT: bu SÖZDİZİMSEL bir bekçidir — HANGİ tablo/şema okunacağını kısıtlamaz; kiracı/
/// kapsam izolasyonu bağlantı-düzeyi salt-okunur login + (ileride) RLS ile sağlanır. Skaler
/// UDF çağrıları (ifade düzeyi) engellenmez — önkoşulu (UNSAFE/EXTERNAL_ACCESS CLR) yüksek ve
/// salt-okunur login ile sınırlıdır; artık risk olarak izlenir.</para></summary>
public sealed class ScriptDomQueryGuard : IQueryGuard
{
    public GuardResult Inspect(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return GuardResult.Ret("Boş sorgu çalıştırılamaz.");

        var ayirici = new TSql160Parser(initialQuotedIdentifiers: true);
        TSqlFragment agac;
        IList<ParseError> hatalar;
        using (var okuyucu = new StringReader(sql))
            agac = ayirici.Parse(okuyucu, out hatalar);

        if (hatalar.Count > 0)
            return GuardResult.Ret($"SQL ayrıştırılamadı: {hatalar[0].Message}");

        if (agac is not TSqlScript betik)
            return GuardResult.Ret("Beklenmeyen sorgu yapısı.");

        // Tek ifade olmalı (çoklu statement/GO ile ayrılmış batch'ler dahil).
        var ifadeler = betik.Batches.SelectMany(b => b.Statements).ToList();
        if (ifadeler.Count == 0)
            return GuardResult.Ret("Çalıştırılabilir bir ifade yok.");
        if (ifadeler.Count > 1)
            return GuardResult.Ret("Yalnız tek bir SELECT ifadesine izin verilir; çoklu ifade reddedildi.");

        // O tek ifade bir SELECT olmalı.
        if (ifadeler[0] is not SelectStatement select)
            return GuardResult.Ret($"Yalnız SELECT çalıştırılabilir; '{ifadeler[0].GetType().Name}' reddedildi.");

        // SELECT ... INTO newEntity tablo oluşturur → yazma.
        if (select.Into is not null)
            return GuardResult.Ret("SELECT ... INTO (tablo oluşturma) reddedildi.");

        // Derinlemesine: SELECT içindeki TÜM tablo kaynakları BEYAZ-LİSTEDE olmalı (kara-liste
        // değil — listede olmayan her tip reddedilir → fail-closed). Böylece OPENROWSET/OPENQUERY/
        // OPENDATASOURCE, tablo-değerli fonksiyonlar (fn_trace_gettable/fn_get_audit_file → dosya/UNC
        // okuma) ve iç içe DML (SELECT ... FROM (DELETE ...)) gibi yan-etkili kaynaklar da düşer.
        var denetci = new TableReferenceAllowlist();
        agac.Accept(denetci);
        if (denetci.Sebep is not null)
            return GuardResult.Ret(denetci.Sebep);

        return GuardResult.Gecer;
    }

    /// <summary>Ağaçtaki her <c>TableReference</c>'ı bilinen-güvenli tiplerle karşılaştırır;
    /// listede olmayan bir tablo kaynağı (tablo-değerli fonksiyon, OPENROWSET/OPENQUERY, iç içe
    /// DML, CHANGETABLE, OPENXML, SEMANTIC*…) bulunursa reddeder. Beyaz-liste → gelecekte eklenen
    /// riskli düğümler otomatik kapalı kalır.</summary>
    private sealed class TableReferenceAllowlist : TSqlFragmentVisitor
    {
        private static readonly HashSet<string> Guvenli = new(StringComparer.Ordinal)
        {
            "NamedTableReference",            // tablo / görünüm / CTE adı
            "QueryDerivedTable",              // (SELECT ...) AS x
            "InlineDerivedTable",             // (VALUES ...) AS t(c)
            "QualifiedJoin",                  // A JOIN B ON ...
            "UnqualifiedJoin",                // A CROSS JOIN B
            "JoinParenthesisTableReference",  // parantezli join
            "PivotedTableReference",          // PIVOT
            "UnpivotedTableReference",        // UNPIVOT
            "OpenJsonTableReference",         // OPENJSON (JSON ayrıştırma, salt-okuma)
            "VariableTableReference",         // @tabloDegiskeni
        };

        public string? Sebep { get; private set; }

        public override void Visit(TSqlFragment node)
        {
            if (Sebep is not null) return;
            if (node is TableReference && !Guvenli.Contains(node.GetType().Name))
                Sebep = $"Güvenli olmayan tablo kaynağı reddedildi ({node.GetType().Name}): " +
                        "OPENROWSET/OPENQUERY, tablo-değerli fonksiyon veya iç içe DML olabilir.";
        }
    }
}
