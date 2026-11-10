using Kds.Execution;
using Xunit;

namespace Kds.Tests;

/// <summary>M33 tanı kural motoru — canlı DB gerektirmez: temsilî plan XML'i verilir, bulgular
/// doğrulanır. (Kuralların gerçek KdsDemo planlarında da tetiklendiği elle doğrulandı: eksik
/// index önerisi + 2M satır tarama; SHOWPLAN yetkisi olmayan kullanıcıda yetki bulgusu.)</summary>
public class QueryPlanRulesTests
{
    private const string PlanBasi =
        "<ShowPlanXML xmlns=\"http://schemas.microsoft.com/sqlserver/2004/07/showplan\">" +
        "<BatchSequence><Batch><Statements><StmtSimple><QueryPlan>";
    private const string PlanSonu = "</QueryPlan></StmtSimple></Statements></Batch></BatchSequence></ShowPlanXML>";

    private static string Plan(string ic) => PlanBasi + ic + PlanSonu;

    [Fact]
    public void eksik_index_bulgusu_kopyalanabilir_create_index_uretir()
    {
        var tani = QueryPlanRules.PlandanTani(Plan(
            "<MissingIndexes><MissingIndexGroup Impact=\"92.5\">" +
            "<MissingIndex Database=\"[KdsDemo]\" Schema=\"[arac]\" Table=\"[AracKilometreOkumalari]\">" +
            "<ColumnGroup Usage=\"EQUALITY\"><Column Name=\"[OkumaKaynagi]\" /></ColumnGroup>" +
            "<ColumnGroup Usage=\"INCLUDE\"><Column Name=\"[Kilometre]\" /></ColumnGroup>" +
            "</MissingIndex></MissingIndexGroup></MissingIndexes>"));

        var b = Assert.Single(tani.Bulgular);
        Assert.Equal("yuksek", b.Siddet);
        Assert.Contains("Eksik index", b.Baslik);
        Assert.Contains("~%92", b.Aciklama);
        Assert.Equal("CREATE INDEX [IX_Oneri_AracKilometreOkumalari] ON [KdsDemo].[arac].[AracKilometreOkumalari] " +
                     "([OkumaKaynagi]) INCLUDE ([Kilometre]);", b.KopyalanabilirSql);
        Assert.Contains("veritabanında", tani.Ozet);
    }

    [Fact]
    public void buyuk_tarama_okunan_satira_bakar_columnstore_taramasi_sayilmaz()
    {
        // Rowstore: ÇIKAN satır az (filtre eliyor) ama OKUNAN 2M → bulgu. Columnstore: BI'da normal → bulgu YOK.
        var tani = QueryPlanRules.PlandanTani(Plan(
            "<RelOp PhysicalOp=\"Table Scan\" EstimateRows=\"120\" EstimatedRowsRead=\"2000000\">" +
            "<Object Schema=\"[arac]\" Table=\"[AracKilometreOkumalari]\" /></RelOp>" +
            "<RelOp PhysicalOp=\"Index Scan\" EstimateRows=\"2000000\" EstimatedRowsRead=\"2000000\">" +
            "<Object Schema=\"[talep]\" Table=\"[Talepler]\" Storage=\"ColumnStore\" /></RelOp>"));

        var b = Assert.Single(tani.Bulgular);
        Assert.Contains("AracKilometreOkumalari", b.Baslik);
        Assert.Contains("2.000.000", b.Aciklama);
        Assert.DoesNotContain(tani.Bulgular, x => x.Baslik.Contains("Talepler"));
    }

    [Fact]
    public void top_ve_buyuk_siralama_row_goal_ipucu_onerir()
    {
        var tani = QueryPlanRules.PlandanTani(Plan(
            "<RelOp PhysicalOp=\"Top\" EstimateRows=\"50\" />" +
            "<RelOp PhysicalOp=\"Sort\" EstimateRows=\"800000\" />"));

        var b = Assert.Single(tani.Bulgular);
        Assert.Contains("row-goal", b.Baslik);
        Assert.Equal("OPTION (USE HINT('DISABLE_OPTIMIZER_ROWGOAL'))", b.KopyalanabilirSql);
    }

    [Fact]
    public void kosulsuz_join_uyarisi_yuksek_siddetle_bildirilir()
    {
        var tani = QueryPlanRules.PlandanTani(Plan("<Warnings NoJoinPredicate=\"1\" />"));

        var b = Assert.Single(tani.Bulgular);
        Assert.Equal("yuksek", b.Siddet);
        Assert.Contains("kartezyen", b.Baslik);
    }

    [Fact]
    public void temiz_planda_uydurma_bulgu_uretilmez()
    {
        var tani = QueryPlanRules.PlandanTani(Plan("<RelOp PhysicalOp=\"Index Seek\" EstimateRows=\"12\" />"));

        Assert.Empty(tani.Bulgular);
        Assert.Contains("bariz bir sorun görünmüyor", tani.Ozet);
    }

    [Fact]
    public void plan_yoksa_sql_metninden_genel_oneriler_cikar()
    {
        // Plana yetki olmayan durumun (Msg 262) düştüğü yol: kesin teşhis yok, hepsi "bilgi".
        var bulgular = QueryPlanRules.MetindenIpuclari(
            "SELECT TOP (50) * FROM t WHERE ad LIKE N'%x' ORDER BY tutar DESC");

        Assert.All(bulgular, b => Assert.Equal("bilgi", b.Siddet));
        Assert.Contains(bulgular, b => b.Baslik.Contains("Satır limiti + sıralama"));
        Assert.Contains(bulgular, b => b.Baslik.Contains("joker"));
    }
}
