using Kds.Contracts.Connections;
using Kds.Contracts.DataMaps;
using Kds.Contracts.QueryModel;
using Kds.Execution;
using Xunit;

namespace Kds.Tests;

/// <summary>Parametreli rapor — üretici çözümü: filtre değeri <c>{ $param }</c> referansı çalışma
/// değerine (yoksa varsayılana) çözülür, kolon tipine çevrilir, yine PARAMETRE olarak bağlanır.
/// TarihAralik iki uca (BETWEEN), zorunlu+eksik → 400. Deterministik, DB gerekmez.</summary>
public sealed class ParametreliRaporTests
{
    private static readonly Guid TS = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly MssqlQueryGenerator Uretici = new();

    private static DataMapDetail Harita() => new(
        Guid.NewGuid(), "H", Guid.NewGuid(), DataMode.Canli,
        [
            new DataMapTableDto(TS, "dbo", "Siparisler", "Siparisler",
            [
                new DataMapColumnDto("SiparisNo", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Tutar", null, ColumnRole.Olcu, "money"),
                new DataMapColumnDto("Tarih", null, ColumnRole.Boyut, "date"),
            ]),
        ],
        []);

    private static Dictionary<string, object?> Ref(string anahtar, string? parca = null)
    {
        var d = new Dictionary<string, object?> { ["$param"] = anahtar };
        if (parca is not null) d["parca"] = parca;
        return d;
    }

    [Fact]
    public void skaler_parametre_calisma_degerine_baglanir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo"), new SelectedField(TS, "Tutar")],
            new FilterCondition(TS, "Tutar", KarsilastirmaOp.BuyukEsit, [Ref("altTutar")]),
            [], null,
            Parametreler: [new ReportParameter("altTutar", "Alt Tutar", ParametreTipi.Sayi)]);

        var r = Uretici.Generate(model, Harita(), new Dictionary<string, object?> { ["altTutar"] = "500" });

        Assert.Contains(">= @p0", r.Sql);
        Assert.Equal(500m, r.Parametreler["@p0"]); // money kolonu → decimal'e çevrildi
    }

    [Fact]
    public void tarih_araligi_between_iki_uca_baglanir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo")],
            new FilterCondition(TS, "Tarih", KarsilastirmaOp.Arasinda, [Ref("donem", "baslangic"), Ref("donem", "bitis")]),
            [], null,
            Parametreler: [new ReportParameter("donem", "Dönem", ParametreTipi.TarihAralik)]);

        var deg = new Dictionary<string, object?>
        {
            ["donem"] = new Dictionary<string, object?> { ["baslangic"] = "2026-01-01", ["bitis"] = "2026-12-31" },
        };
        var r = Uretici.Generate(model, Harita(), deg);

        Assert.Contains("BETWEEN @p0 AND @p1", r.Sql);
        Assert.Equal(new DateTime(2026, 1, 1), r.Parametreler["@p0"]);
        Assert.Equal(new DateTime(2026, 12, 31), r.Parametreler["@p1"]);
    }

    [Fact]
    public void deger_verilmezse_varsayilan_kullanilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo")],
            new FilterCondition(TS, "Tutar", KarsilastirmaOp.BuyukEsit, [Ref("altTutar")]),
            [], null,
            Parametreler: [new ReportParameter("altTutar", "Alt", ParametreTipi.Sayi, Varsayilan: "100")]);

        var r = Uretici.Generate(model, Harita()); // çalışma değeri yok → varsayılan

        Assert.Equal(100m, r.Parametreler["@p0"]);
    }

    [Fact]
    public void zorunlu_parametre_eksikse_anlasilir_hata()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo")],
            new FilterCondition(TS, "Tutar", KarsilastirmaOp.BuyukEsit, [Ref("altTutar")]),
            [], null,
            Parametreler: [new ReportParameter("altTutar", "Alt Tutar", ParametreTipi.Sayi, Zorunlu: true)]);

        var ex = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("Alt Tutar", ex.Message); // etiketle
    }

    [Fact]
    public void tanimsiz_parametre_referansi_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo")],
            new FilterCondition(TS, "Tutar", KarsilastirmaOp.Esit, [Ref("yok")]),
            [], null,
            Parametreler: []);

        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void parametresiz_model_eskisi_gibi_calisir()
    {
        // Geriye uyum: parametre yoksa sabit değer aynen gömülür (regresyon yok). Değer JSON'dan
        // long gelir (tel gerçeği) — üretici money kolonuna decimal bağlar.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo")],
            new FilterCondition(TS, "Tutar", KarsilastirmaOp.BuyukEsit, [1000L]),
            [], null);

        var r = Uretici.Generate(model, Harita());

        Assert.Contains(">= @p0", r.Sql);
        Assert.Equal(1000m, r.Parametreler["@p0"]);
    }
}
