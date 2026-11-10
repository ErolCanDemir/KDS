using Kds.Business.DataMaps;
using Kds.Business.Nlq;
using Kds.Contracts.DataMaps;
using Kds.Contracts.Nlq;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using Kds.Data;
using Kds.Data.Entities;
using Kds.Execution;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S23 Soru Sor — saf çekirdeğin testleri. Kurtarıcı kuralları 20 Ağu 2026'daki GERÇEK
/// model (qwen2.5-coder:7b) fizibilite koşumlarıyla kalibre edildi; buradaki senaryolar o
/// koşumlarda gözlenen davranışların birebir kopyasıdır (çit, çift hesap, kova+toplama).</summary>
public sealed class SoruSorTests
{
    private static readonly Guid HaritaId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid TaleplerId = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly Guid ServislerId = Guid.Parse("33333333-0000-0000-0000-000000000003");

    /// <summary>Fizibilitedeki şemanın küçük kopyası: Talepler (dostça ad farklı) + Servisler.</summary>
    private static DataMapDetail Harita() => new(
        HaritaId, "Satış Haritası", Guid.NewGuid(), Contracts.Connections.DataMode.Canli,
        Tablolar:
        [
            new DataMapTableDto(TaleplerId, "dbo", "Talepler", "Talepler",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("ServisId", "Servis No", ColumnRole.Boyut, "int"),
                new DataMapColumnDto("ToplamTutar", "Tutar", ColumnRole.Olcu, "money"),
                new DataMapColumnDto("AcilisTarihi", "Açılış Tarihi", ColumnRole.Boyut, "datetime"),
                new DataMapColumnDto("TalepDurumId", null, ColumnRole.Boyut, "int"),
            ]),
            new DataMapTableDto(ServislerId, "dbo", "Servisler", "Servisler",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Ad", "Servis Adı", ColumnRole.Boyut, "nvarchar"),
            ]),
        ],
        Iliskiler: []);

    private static Metric Metrik(string ad = "Toplam Talep Tutari", string ifade = "SUM({Talepler.Tutar})") => new()
    {
        Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Ad = ad, Ifade = ifade,
        VeriHaritasiId = HaritaId, Bicim = "Para", Aktif = true,
    };

    // ---------- SoruIstemleri (istem kurucu) ----------

    [Fact]
    public void istem_sema_ve_katalogu_tasir_ama_formul_tasimaz()
    {
        var istem = SoruIstemleri.Kur("servis başına ciro", Harita(), [Metrik()]);

        // Dostça adlar + tipler gider; metrik yalnız AD+açıklama olarak gider (formül gitmez —
        // modele referans yetiyor, gizlilik yüzeyi küçük kalıyor).
        Assert.Contains("Talepler (", istem);
        Assert.Contains("Tutar money", istem);
        Assert.Contains("Servis Adı nvarchar", istem);
        Assert.Contains("- Toplam Talep Tutari", istem);
        Assert.DoesNotContain("SUM({Talepler.Tutar})", istem);
        Assert.Contains("## Soru", istem);
        Assert.EndsWith("servis başına ciro", istem);
    }

    [Fact]
    public void istem_ILISKILERI_ve_adla_suzme_kuralini_tasir()
    {
        /* S39 — kullanıcı bulgusu (25 Ağu 2026): çok tablolu sorular çevrilemiyordu. "AUDİ marka
         * araçlar" için model, marka ADINI MarkaId (int) kolonuna yazıyor ve sorgu "tipine
         * çevrilemedi" diye düşüyordu; çünkü hangi tablonun hangisine bağlı olduğunu GÖRMÜYORDU.
         * Test hem ilişki listesinin hem de yaşanan hatayı örnekleyen kuralın istemde durduğunu
         * kilitler. */
        var iliskili = Harita() with
        {
            Iliskiler = [new DataMapRelationshipDto(TaleplerId, "ServisId", ServislerId, "Id", true)],
        };

        var istem = SoruIstemleri.Kur("audi marka araçların tutarı", iliskili, []);

        Assert.Contains("## Tablo ilişkileri", istem);
        Assert.Contains("Talepler.ServisId → Servisler.Id", istem);
        // Model JOIN YAZMAZ (üretici yolu kendisi kurar) — istem bunu açıkça söylemeli.
        Assert.Contains("birleştirmeyi SİSTEM yapar", istem);
        // Yaşanan hata örnekle yasaklanır: ad → Id kolonuna yazılmaz.
        Assert.Contains("\"kolon\":\"MarkaId\",\"op\":\"Esit\",\"degerler\":[\"AUDİ\"]", istem);
        Assert.Contains("\"tablo\":\"Markalar\",\"kolon\":\"Ad\"", istem);
        Assert.Contains("hangi tabloyu", istem);   // 'Tutar' belirsizliği
    }

    [Fact]
    public void kurtarici_Id_kolonuna_yazilan_SAYIYI_tahmin_diye_isaretler()
    {
        /* S39 — SESSİZ YANLIŞ: kullanıcı "AUDİ marka" der, model 'MarkaId = 1' kurar; sorgu
         * hatasız çalışır ve YANLIŞ markanın rakamını döndürür. Çevrilemeyen sorudan tehlikelidir
         * (hata yok, sayı doğru görünür), o yüzden "Nasıl hesaplandı"da açıkça işaretlenir. */
        var iliskili = Harita() with
        {
            Iliskiler = [new DataMapRelationshipDto(TaleplerId, "ServisId", ServislerId, "Id", true)],
        };

        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Topla" } ], "filtreler": [ { "tablo": "Talepler", "kolon": "ServisId", "op": "Esit", "degerler": ["1"] } ] }""",
            iliskili, []);

        var uyari = Assert.Single(k.NasilHesaplandi, n => n.Contains("MODELİN TAHMİNİDİR"));
        Assert.Contains("Servis No", uyari);     // dostça ad kullanılır
        Assert.Contains("Servisler", uyari);     // hangi tablodan ad ile süzülebileceği söylenir
        // Sorgu ENGELLENMEZ: kullanıcı bilerek Id ile süzüyor olabilir.
        Assert.NotNull(k.Model.Filtre);
    }

    [Fact]
    public void kurtarici_METIN_esitligini_icerire_cevirir()
    {
        /* S39 son halka: model isteme rağmen metin kolonunda "Esit" kullanıyor; veride "Audi"
         * yazılıyken kullanıcı "AUDİ" deyince eşitlik SESSİZCE 0 satır döndürüyordu (canlı ölçüm:
         * sorgu 200, toplam null). 'İçerir' üreticide Türkçe harf duyarsızdır. */
        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Topla" } ], "filtreler": [ { "tablo": "Servisler", "kolon": "Servis Adı", "op": "Esit", "degerler": ["AUDİ"] } ] }""",
            Harita(), []);

        var kosul = Assert.IsType<FilterCondition>(k.Model.Filtre);
        Assert.Equal(KarsilastirmaOp.Icerir, kosul.Op);
        Assert.Contains(k.NasilHesaplandi, n => n.Contains("\"içerir\" ile süzüldü"));
    }

    [Fact]
    public void kurtarici_SAYISAL_kolonda_esitligi_BOZMAZ()
    {
        // Yalnız metin kolonlarında dönüşür: sayıda "içerir" hem anlamsız hem yanlış olurdu
        // (7 → '%7%' ile 17, 27 de eşleşirdi).
        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Topla" } ], "filtreler": [ { "tablo": "Talepler", "kolon": "TalepDurumId", "op": "Esit", "degerler": ["3"] } ] }""",
            Harita(), []);

        var kosul = Assert.IsType<FilterCondition>(k.Model.Filtre);
        Assert.Equal(KarsilastirmaOp.Esit, kosul.Op);
    }

    [Fact]
    public void kurtarici_belirsiz_kolonu_BAGLAMDAN_cozer()
    {
        /* S39 — "Tutar" iki tabloda birden var; model bir alanda tabloyu yazmayı unutmuş ama
         * BAŞKA bir alanda "Servisler" demiş. Eskiden bu "birden çok tabloda var" hatasıyla
         * düşüyor ve kullanıcı soruyu yeniden yazmak zorunda kalıyordu. */
        var ikiTutarli = Harita() with
        {
            Tablolar =
            [
                Harita().Tablolar[0],
                Harita().Tablolar[1] with
                {
                    Kolonlar = [.. Harita().Tablolar[1].Kolonlar,
                        new DataMapColumnDto("ToplamTutar", "Tutar", ColumnRole.Olcu, "money")],
                },
            ],
        };

        // Tablosuz "Tutar" + tablosu YAZILI "Servis Adı" → bağlam Servisler'i işaret eder.
        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "kolon": "Tutar", "toplama": "Topla" }, { "tablo": "Servisler", "kolon": "Servis Adı", "toplama": "Yok" } ] }""",
            ikiTutarli, []);

        Assert.Contains(k.Model.Alanlar, a => a.TabloId == ServislerId && a.Toplama == ToplamaTuru.Topla);
    }

    [Fact]
    public void kurtarici_baglam_belirsizse_ANLAMLI_hata_verir()
    {
        // Bağlamda iki aday da varsa seçim yine TAHMİN olurdu; yanlış tablodan gelen rakam sessiz
        // yanlıştır. Bu durumda hata vermek doğrudur — kullanıcı tabloyu söyleyebilir.
        var ikiTutarli = Harita() with
        {
            Tablolar =
            [
                Harita().Tablolar[0],
                Harita().Tablolar[1] with
                {
                    Kolonlar = [.. Harita().Tablolar[1].Kolonlar,
                        new DataMapColumnDto("ToplamTutar", "Tutar", ColumnRole.Olcu, "money")],
                },
            ],
        };

        var hata = Assert.Throws<QueryModelException>(() => ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "kolon": "Tutar", "toplama": "Topla" }, { "tablo": "Talepler", "kolon": "Servis No" }, { "tablo": "Servisler", "kolon": "Servis Adı" } ] }""",
            ikiTutarli, []));

        Assert.Contains("birden çok tabloda var", hata.Message);
    }

    [Fact]
    public void kurtarici_Id_kolonuna_yazilan_METNI_ad_kolonuna_TASIR()
    {
        /* S39 — modelin en sık ve en yıkıcı hatası: kullanıcı "AUDİ marka" der, model
         * 'MarkaId = "AUDİ"' kurar ve sorgu "int'e çevrilemedi" diye ÖLÜR. İstem kuralı yazıldı
         * ama model tutarsız kaldı; onarım burada deterministik. */
        var iliskili = Harita() with
        {
            Iliskiler = [new DataMapRelationshipDto(TaleplerId, "ServisId", ServislerId, "Id", true)],
        };

        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Topla" } ], "filtreler": [ { "tablo": "Talepler", "kolon": "ServisId", "op": "Esit", "degerler": ["Etimesgut Servis"] } ] }""",
            iliskili, []);

        var kosul = Assert.IsType<FilterCondition>(k.Model.Filtre);
        Assert.Equal(ServislerId, kosul.TabloId);                      // filtre AD tablosuna taşındı
        Assert.Equal("Ad", kosul.Kolon);
        Assert.Equal(KarsilastirmaOp.Icerir, kosul.Op);                // yazım farkına dayanıklı
        Assert.Equal("Etimesgut Servis", Assert.Single(kosul.Degerler));
        Assert.Contains(k.NasilHesaplandi, n => n.Contains("üzerinden süzüldü"));
    }

    [Fact]
    public void kurtarici_SAYISAL_Id_degerini_tasimaz()
    {
        // Karşı taraf: gerçek bir Id verilmişse dokunulmaz — taşımak yanlış tabloyu süzer ve
        // "çalışan ama yanlış" sonuç üretirdi. (Tahmin uyarısı ayrıca çalışır.)
        var iliskili = Harita() with
        {
            Iliskiler = [new DataMapRelationshipDto(TaleplerId, "ServisId", ServislerId, "Id", true)],
        };

        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Topla" } ], "filtreler": [ { "tablo": "Talepler", "kolon": "ServisId", "op": "Esit", "degerler": ["7"] } ] }""",
            iliskili, []);

        var kosul = Assert.IsType<FilterCondition>(k.Model.Filtre);
        Assert.Equal(TaleplerId, kosul.TabloId);
        Assert.Equal("ServisId", kosul.Kolon);
    }

    [Fact]
    public void kurtarici_tarih_OLMAYAN_kolondaki_kovayi_dusurur()
    {
        /* S39: model 'ModelYili' (smallint) ve hatta 'Ad' (nvarchar) kolonlarına tarih kovası
         * uygulayıp sorguyu 400'e düşürüyordu. İstem kuralı denendi ve TERS TEPTİ (yasak cümlesi
         * yasakladığını öğretiyor), garanti bu yüzden kodda: kova düşer, sorgu ÇALIŞIR, not yazılır. */
        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Yok", "kova": "Ay" } ] }""",
            Harita(), []);

        Assert.All(k.Model.Alanlar, a => Assert.Null(a.Kova));         // kova düştü
        Assert.Contains(k.NasilHesaplandi, n => n.Contains("tarih kolonu olmadığı için"));
        Assert.NotEmpty(k.Model.Alanlar);                              // sorgu yaşamaya devam eder
    }

    [Fact]
    public void kurtarici_TARIH_kolonundaki_kovayi_korur()
    {
        // Karşı taraf: gerçek tarih kolonunda kova dokunulmadan geçmeli, yoksa zaman kırılımı biter.
        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Açılış Tarihi", "toplama": "Yok", "kova": "Ay" } ] }""",
            Harita(), []);

        Assert.Contains(k.Model.Alanlar, a => a.Kova == TarihKovasi.Ay);
        Assert.DoesNotContain(k.NasilHesaplandi, n => n.Contains("tarih kolonu olmadığı için"));
    }

    [Fact]
    public void kurtarici_ADLA_suzmede_uyarmaz()
    {
        // Doğru yol (ad kolonuna filtre) uyarı üretmemeli — yoksa uyarı gürültüye dönüşür ve
        // kullanıcı gerçek tahminleri fark etmez.
        var iliskili = Harita() with
        {
            Iliskiler = [new DataMapRelationshipDto(TaleplerId, "ServisId", ServislerId, "Id", true)],
        };

        var k = ModelKurtarici.Kurtar(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Topla" } ], "filtreler": [ { "tablo": "Servisler", "kolon": "Ad", "op": "Icerir", "degerler": ["Etimesgut"] } ] }""",
            iliskili, []);

        Assert.DoesNotContain(k.NasilHesaplandi, n => n.Contains("MODELİN TAHMİNİDİR"));
    }

    [Fact]
    public void istem_iliski_yoksa_o_bolum_HIC_yazilmaz()
    {
        // Tek tablolu haritada ilişki bölümü sadece token israfı olurdu; ayrıca "birleştirme"
        // kavramı olmayan bir haritada modeli yanlış yola sokar.
        var istem = SoruIstemleri.Kur("soru", Harita(), []);
        Assert.DoesNotContain("## Tablo ilişkileri", istem);
    }

    [Fact]
    public void istem_metrik_yoksa_katalog_bos_der()
    {
        /* Boş katalogda model kolon adını "metrikler"e yazıp bekçiye takılıyordu (S30) ve
         * "KISA YAZ" kuralı eklenince yasak yeniden delindi (24 Ağu 2026 ölçümü: 'ToplamTutar'
         * metrik sanıldı). Test hem YASAĞIN hem de DOĞRU YOLUN istemde durduğunu tutar. */
        var istem = SoruIstemleri.Kur("soru", Harita(), []);
        Assert.Contains("katalogda metrik YOK", istem);
        Assert.Contains("Kolon adı metrik DEĞİLDİR", istem);
        Assert.Contains("\"toplama\":\"Topla\"", istem);   // doğru yol örnekle gösterilir
        /* ASIL KORUMA — alan ŞEMADA HİÇ YOK: iki tur istem sertleştirmesi (yasak cümlesi) modeli
         * durduramadı; görmediği alanı yazamaz. Metrik kuralları da katalog boşken gizlenir. */
        Assert.DoesNotContain("\"metrikler\": [ \"metrik adı\" ]", istem);
        Assert.DoesNotContain("ÖNCE metrik kataloğuna bak", istem);

        // Katalog DOLUYKEN alan ve kurallar geri gelir.
        var doluIstem = SoruIstemleri.Kur("soru", Harita(), [Metrik()]);
        Assert.Contains("\"metrikler\": [ \"metrik adı\" ]", doluIstem);
        Assert.Contains("ÖNCE metrik kataloğuna bak", doluIstem);
    }

    // ---------- ModelKurtarici (normalizasyon) ----------

    [Fact]
    public void cit_ayiklanir_ve_model_kurulur()
    {
        // format:json kipine RAĞMEN model ```json çiti basabiliyor (fizibilite koşumu 3'ün ham çıktısı).
        var ham = """
            ```json
            { "alanlar": [ { "tablo": "Servisler", "kolon": "Servis Adı", "toplama": "Yok" } ],
              "metrikler": ["Toplam Talep Tutari"], "filtreler": [], "siralama": [], "limit": 3 }
            ```
            """;
        var metrik = Metrik();
        var k = ModelKurtarici.Kurtar(ham, Harita(), [metrik]);

        var alan = Assert.Single(k.Model.Alanlar);
        Assert.Equal(ServislerId, alan.TabloId);
        Assert.Equal("Ad", alan.Kolon);              // dostça ad → HAM ada çözüldü
        var referans = Assert.Single(k.Model.MetrikAlanlar!);
        Assert.Equal(metrik.Id, referans.MetrikId);
        Assert.Equal(3, k.Model.SatirLimiti);
        Assert.Null(k.Model.Gruplamalar);            // üretici otomatik gruplar — bilinçli karar
    }

    [Fact]
    public void metrikle_cakisan_toplamali_alan_dusurulur_toplamasiz_kirilim_kalir()
    {
        // Fizibilite koşumu 2'nin birebir gözlemi: model metriği DE seçti, SUM'ı DA kurdu.
        var ham = """
            { "alanlar": [ { "tablo": "Talepler", "kolon": "ServisId", "toplama": "Yok" },
                           { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Topla" } ],
              "metrikler": ["Toplam Talep Tutari"], "filtreler": [], "siralama": [], "limit": null }
            """;
        var k = ModelKurtarici.Kurtar(ham, Harita(), [Metrik()]);

        var alan = Assert.Single(k.Model.Alanlar);   // SUM(Tutar) düştü, kırılım kaldı
        Assert.Equal("ServisId", alan.Kolon);
        Assert.Contains(k.NasilHesaplandi, n => n.Contains("çakıştığı için atlandı"));
    }

    [Fact]
    public void kova_ile_toplama_ayni_alanda_gelirse_ikiye_ayrilir()
    {
        // Fizibilite koşumu 3: "aylık talep sayısı" → tek alana kova:Ay + toplama:Say sıkıştı
        // (Y4 üreticide red). Kurtarıcı kırılım + ölçü diye ayırır.
        var ham = """
            { "alanlar": [ { "tablo": "Talepler", "kolon": "AcilisTarihi", "toplama": "Say", "kova": "Ay" } ],
              "metrikler": [], "filtreler": [], "siralama": [], "limit": null }
            """;
        var k = ModelKurtarici.Kurtar(ham, Harita(), []);

        Assert.Equal(2, k.Model.Alanlar.Count);
        Assert.Contains(k.Model.Alanlar, a => a.Toplama == ToplamaTuru.Yok && a.Kova == TarihKovasi.Ay);
        Assert.Contains(k.Model.Alanlar, a => a.Toplama == ToplamaTuru.Say && a.Kova is null);
    }

    [Fact]
    public void filtre_op_eslenir_ve_cok_degerli_esit_icindeye_cevrilir()
    {
        var ham = """
            { "alanlar": [ { "tablo": "Talepler", "kolon": "ServisId", "toplama": "Yok" } ],
              "metrikler": [],
              "filtreler": [ { "tablo": "Talepler", "kolon": "TalepDurumId", "op": "Esit", "degerler": ["1", "2"] },
                             { "tablo": "Talepler", "kolon": "Açılış Tarihi", "op": "between", "degerler": ["2026-01-01", "2026-12-31"] } ],
              "siralama": [], "limit": null }
            """;
        var k = ModelKurtarici.Kurtar(ham, Harita(), []);

        var grup = Assert.IsType<FilterGroup>(k.Model.Filtre);
        Assert.Equal(MantikBaglaci.Ve, grup.Baglac);
        var durum = Assert.IsType<FilterCondition>(grup.Cocuklar[0]);
        Assert.Equal(KarsilastirmaOp.Icinde, durum.Op);         // Esit + 2 değer → Icinde
        var tarih = Assert.IsType<FilterCondition>(grup.Cocuklar[1]);
        Assert.Equal(KarsilastirmaOp.Arasinda, tarih.Op);       // İngilizce takma ad da eşlenir
        Assert.Equal("AcilisTarihi", tarih.Kolon);              // dostça ad → ham ad
    }

    [Fact]
    public void siralama_metrik_adina_cikis_adiyla_baglanir_bilinmeyen_hedef_atlanir()
    {
        var ham = """
            { "alanlar": [ { "tablo": "Talepler", "kolon": "ServisId", "toplama": "Yok" } ],
              "metrikler": ["Toplam Talep Tutari"], "filtreler": [],
              "siralama": [ { "hedef": "Toplam Talep Tutari", "azalan": true },
                            { "hedef": "Uydurma Kolon", "azalan": false } ],
              "limit": null }
            """;
        var k = ModelKurtarici.Kurtar(ham, Harita(), [Metrik()]);

        var sira = Assert.Single(k.Model.Siralama);
        Assert.Equal("Toplam Talep Tutari", sira.CikisAdi);     // çıkış adı — tablo/kolon yok sayılır
        Assert.True(sira.Azalan);
        Assert.Contains(k.NasilHesaplandi, n => n.Contains("Uydurma Kolon") && n.Contains("atlandı"));
    }

    [Fact]
    public void uydurma_kolon_ve_metrik_acik_hatayla_reddedilir()
    {
        var kolonYok = """{ "alanlar": [ { "tablo": "Talepler", "kolon": "OlmayanKolon" } ], "metrikler": [], "filtreler": [], "siralama": [], "limit": null }""";
        var h1 = Assert.Throws<QueryModelException>(() => ModelKurtarici.Kurtar(kolonYok, Harita(), []));
        Assert.Contains("OlmayanKolon", h1.Message);

        var metrikYok = """{ "alanlar": [], "metrikler": ["Hayali Metrik"], "filtreler": [], "siralama": [], "limit": null }""";
        var h2 = Assert.Throws<QueryModelException>(() => ModelKurtarici.Kurtar(metrikYok, Harita(), [Metrik()]));
        Assert.Contains("Hayali Metrik", h2.Message);
    }

    [Fact]
    public void tablo_verilmemis_tekil_kolon_cozulur_cok_tabloda_olan_reddedilir()
    {
        // "ServisId" yalnız Talepler'de → tablo söylenmese de bulunur; "Id" iki tabloda → belirsiz.
        var tekil = """{ "alanlar": [ { "kolon": "ServisId" } ], "metrikler": [], "filtreler": [], "siralama": [], "limit": null }""";
        var k = ModelKurtarici.Kurtar(tekil, Harita(), []);
        Assert.Equal(TaleplerId, Assert.Single(k.Model.Alanlar).TabloId);

        var belirsiz = """{ "alanlar": [ { "kolon": "Id" } ], "metrikler": [], "filtreler": [], "siralama": [], "limit": null }""";
        var hata = Assert.Throws<QueryModelException>(() => ModelKurtarici.Kurtar(belirsiz, Harita(), []));
        Assert.Contains("birden çok tabloda", hata.Message);
    }

    [Fact]
    public void hata_kacisi_ve_bos_cevap_okunur_mesaj_verir()
    {
        var kacis = """{ "hata": "Bu soruda hangi tablo kastedildiği anlaşılmıyor" }""";
        var h1 = Assert.Throws<QueryModelException>(() => ModelKurtarici.Kurtar(kacis, Harita(), []));
        Assert.Contains("anlaşılmıyor", h1.Message);

        var h2 = Assert.Throws<QueryModelException>(() => ModelKurtarici.Kurtar("model saçmaladı", Harita(), []));
        Assert.Contains("JSON bulunamadı", h2.Message);

        var bos = """{ "alanlar": [], "metrikler": [], "filtreler": [], "siralama": [], "limit": null }""";
        var h3 = Assert.Throws<QueryModelException>(() => ModelKurtarici.Kurtar(bos, Harita(), []));
        Assert.Contains("ne bir alan ne bir metrik", h3.Message);
    }

    [Fact]
    public void nasil_hesaplandi_dostca_adlarla_ozetler()
    {
        var ham = """
            { "alanlar": [ { "tablo": "Talepler", "kolon": "Açılış Tarihi", "kova": "Ay" } ],
              "metrikler": ["Toplam Talep Tutari"],
              "filtreler": [ { "tablo": "Talepler", "kolon": "TalepDurumId", "op": "Esit", "degerler": ["1"] } ],
              "siralama": [], "limit": 12 }
            """;
        var k = ModelKurtarici.Kurtar(ham, Harita(), [Metrik()]);

        Assert.Contains(k.NasilHesaplandi, n => n.StartsWith("Metrik: Toplam Talep Tutari"));
        Assert.Contains(k.NasilHesaplandi, n => n == "Kırılım: Açılış Tarihi (aylık)");
        Assert.Contains(k.NasilHesaplandi, n => n.StartsWith("Filtre:") && n.Contains("= 1"));
        Assert.Contains(k.NasilHesaplandi, n => n == "Limit: ilk 12 satır");
    }

    // ---------- AskQuestionHandler (uçtan uca, sahte çevirici ile) ----------

    private sealed class SahteCevirici(string cevap) : ISoruCevirici
    {
        public string? GidenIstem;
        public string? IsitilanOnek;

        public Task<string> CevirAsync(string istem, CancellationToken ct)
        {
            GidenIstem = istem;
            return Task.FromResult(cevap);
        }

        public Task<NlqStatusResult> DurumAsync(CancellationToken ct) =>
            Task.FromResult(new NlqStatusResult(true, "test", null));

        public Task IsitAsync(string onek, CancellationToken ct)
        {
            IsitilanOnek = onek;
            return Task.CompletedTask;
        }
    }

    private sealed class SahteHaritalar : IDataMapReader
    {
        public Task<DataMapDetail?> GetDetailAsync(Guid haritaId, CancellationToken cancellationToken) =>
            Task.FromResult<DataMapDetail?>(haritaId == HaritaId ? Harita() : null);
    }

    private sealed class SahteGonderici : ISender
    {
        public RunQueryModelRequest? Kosulan;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is RunQueryModelRequest k)
            {
                Kosulan = k;
                return Task.FromResult((TResponse)(object)new QueryRunResult([], [], false, 0));
            }

            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task handler_istem_kurar_kurtarir_normal_hattan_kosar_ve_seffaflik_alanlarini_doldurur()
    {
        var kiraci = Guid.NewGuid();
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Haritalar.Add(new DataMap { Id = HaritaId, TenantId = kiraci, Ad = "H", BaglantiId = Guid.NewGuid() });
        var metrik = Metrik();
        metrik.TenantId = kiraci;
        ctx.Metrikler.Add(metrik);
        // Pasif metrik kataloğa GİRMEZ — istemde görünmemeli.
        var pasif = Metrik(ad: "Pasif Metrik");
        pasif.TenantId = kiraci;
        pasif.Aktif = false;
        ctx.Metrikler.Add(pasif);
        await ctx.SaveChangesAsync();

        var cevirici = new SahteCevirici(
            """{ "alanlar": [ { "tablo": "Servisler", "kolon": "Servis Adı" } ], "metrikler": ["Toplam Talep Tutari"], "filtreler": [], "siralama": [], "limit": 3 }""");
        var gonderici = new SahteGonderici();
        var handler = new AskQuestionHandler(new SahteHaritalar(), ctx, cevirici, gonderici);

        var sonuc = await handler.Handle(new AskQuestionRequest(HaritaId, "servis başına ciro, ilk 3"), CancellationToken.None);

        // Show Prompt: dönen istem, çeviriciye gidenin TA KENDİSİ.
        Assert.Equal(cevirici.GidenIstem, sonuc.GidenIstem);
        Assert.Contains("- Toplam Talep Tutari", sonuc.GidenIstem);
        Assert.DoesNotContain("Pasif Metrik", sonuc.GidenIstem);
        // Koşum NORMAL hattan: RunQueryModelRequest ile ve kurtarılan modelle.
        Assert.NotNull(gonderici.Kosulan);
        Assert.Equal(sonuc.Model, gonderici.Kosulan!.Model);
        Assert.Equal(metrik.Id, Assert.Single(sonuc.Model.MetrikAlanlar!).MetrikId);
        Assert.Equal(3, sonuc.Model.SatirLimiti);
        Assert.NotEmpty(sonuc.NasilHesaplandi);
    }

    /* ---------- S30c: düzeltme turunda modele KENDİ ara biçimi gösterilir ----------
     * Ölçüm (23 Ağu 2026, gerçek qwen2.5-coder:7b): iç model (GUID'li tabloId + null alanlar)
     * gösterilince model onu TAKLİT ediyor → üretim 79 → 542 token, süre 26.6 sn → 193.7 sn ve
     * limit gibi alanlar kayboluyor. Bu testler o regresyonu kapıda tutar. */

    [Fact]
    public void araya_cevirme_ic_modeli_MODELIN_KENDI_bicimine_dondurur()
    {
        var model = new QueryModel(HaritaId,
            [new SelectedField(TaleplerId, "ServisId"), new SelectedField(TaleplerId, "ToplamTutar", ToplamaTuru.Topla)],
            Filtre: null,
            [new SortItem(TaleplerId, "ToplamTutar", ToplamaTuru.Topla, Azalan: true, CikisAdi: "Tutar")],
            SatirLimiti: 3);

        var ara = ModelKurtarici.AraJsonaCevir(model, Harita(), []);

        // Dostça adlar var, GUID ve iç alan adları YOK — model kendi dilinde konuşturulur.
        Assert.Contains("\"tablo\":\"Talepler\"", ara);
        Assert.Contains("\"kolon\":\"Servis No\"", ara);          // dostça ad (istemde de bu geçiyor)
        Assert.Contains("\"limit\":3", ara);
        Assert.DoesNotContain("tabloId", ara);
        Assert.DoesNotContain(TaleplerId.ToString(), ara);
        Assert.DoesNotContain("haritaId", ara);
        Assert.DoesNotContain("takmaAd", ara);                    // null gürültüsü de gitmeli
    }

    [Fact]
    public async Task handler_duzeltmede_isteme_ic_model_DEGIL_ara_bicim_koyar()
    {
        var kiraci = Guid.NewGuid();
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Haritalar.Add(new DataMap { Id = HaritaId, TenantId = kiraci, Ad = "H", BaglantiId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();

        var onceki = new QueryModel(HaritaId,
            [new SelectedField(TaleplerId, "ServisId"), new SelectedField(TaleplerId, "ToplamTutar", ToplamaTuru.Topla)],
            Filtre: null, [], SatirLimiti: 3);
        var cevirici = new SahteCevirici(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Servis No" }, { "tablo": "Talepler", "kolon": "Tutar", "toplama": "Topla" } ], "limit": 2 }""");
        var handler = new AskQuestionHandler(new SahteHaritalar(), ctx, cevirici, new SahteGonderici());

        var sonuc = await handler.Handle(
            new AskQuestionRequest(HaritaId, "ilk 2yi göster",
                System.Text.Json.JsonSerializer.Serialize(onceki)), CancellationToken.None);

        Assert.Contains("## Mevcut sorgu", sonuc.GidenIstem);
        Assert.Contains("\"tablo\":\"Talepler\"", sonuc.GidenIstem);
        Assert.DoesNotContain("tabloId", sonuc.GidenIstem);        // iç biçim SIZMAZ
        Assert.DoesNotContain(TaleplerId.ToString(), sonuc.GidenIstem);
        Assert.Equal(2, sonuc.Model.SatirLimiti);                  // düzeltme uygulandı
    }

    [Fact]
    public async Task handler_duzeltmede_gosterilemeyen_filtreyi_KORUR()
    {
        // VEYA'lı filtre ara biçimde temsil edilemez → isteme konmaz, modelden de gelemez;
        // koruma olmasaydı kullanıcının filtresi SESSİZCE düşerdi (en sinsi yanlış rakam yolu).
        var kiraci = Guid.NewGuid();
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Haritalar.Add(new DataMap { Id = HaritaId, TenantId = kiraci, Ad = "H", BaglantiId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();

        var veya = new FilterGroup(MantikBaglaci.Veya, [
            new FilterCondition(TaleplerId, "TalepDurumId", KarsilastirmaOp.Esit, [1]),
            new FilterCondition(TaleplerId, "TalepDurumId", KarsilastirmaOp.Esit, [2]),
        ]);
        var onceki = new QueryModel(HaritaId, [new SelectedField(TaleplerId, "ServisId")], veya, [], SatirLimiti: null);
        var cevirici = new SahteCevirici(
            """{ "alanlar": [ { "tablo": "Talepler", "kolon": "Servis No" } ], "filtreler": [] }""");
        var handler = new AskQuestionHandler(new SahteHaritalar(), ctx, cevirici, new SahteGonderici());

        var sonuc = await handler.Handle(
            new AskQuestionRequest(HaritaId, "sırala",
                System.Text.Json.JsonSerializer.Serialize(onceki)), CancellationToken.None);

        Assert.Contains("filtreler\" alanını BOŞ bırak", sonuc.GidenIstem);   // istem uyarıyor
        // Filtre korundu. Karşılaştırma İÇERİK üzerinden: model JSON gidiş-dönüşünden geçtiği için
        // koleksiyon tipi değişir (ReadOnlyArray → List) ve record eşitliği buna takılır.
        var korunan = Assert.IsType<FilterGroup>(sonuc.Model.Filtre);
        Assert.Equal(MantikBaglaci.Veya, korunan.Baglac);
        Assert.Equal(2, korunan.Cocuklar.Count);
        Assert.All(korunan.Cocuklar, c => Assert.Equal("TalepDurumId", Assert.IsType<FilterCondition>(c).Kolon));
        Assert.Contains(sonuc.NasilHesaplandi, n => n.Contains("filtreler korundu"));
    }

    [Fact]
    public async Task isitma_oneki_gercek_istemin_BASLANGICIYLA_birebir_ayni()
    {
        /* S34 — ısıtmanın TEK işe yarama koşulu bu: Ollama öneki ancak bayt bayt aynıysa
         * yeniden kullanır (ölçüm: aynı önek 49.7 sn → 0.2 sn). Önek üretimi ile istem üretimi
         * ayrı yerlerde yaşasaydı biri değişince ısıtma sessizce ETKİSİZ kalırdı — kimse fark
         * etmezdi, çünkü ürün yine doğru çalışır, sadece yavaşlar. Test bu sessiz kaybı yakalar. */
        var kiraci = Guid.NewGuid();
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Haritalar.Add(new DataMap { Id = HaritaId, TenantId = kiraci, Ad = "H", BaglantiId = Guid.NewGuid() });
        ctx.Metrikler.Add(new Metric
        {
            Id = Guid.NewGuid(), TenantId = kiraci, VeriHaritasiId = HaritaId,
            Ad = "Toplam Ciro", Ifade = "SUM({Talepler.ToplamTutar})", Aktif = true,
        });
        await ctx.SaveChangesAsync();

        var cevirici = new SahteCevirici("""{ "alanlar": [ { "tablo": "Talepler", "kolon": "Servis No" } ] }""");
        await new NlqIsitHandler(new SahteHaritalar(), ctx, cevirici)
            .Handle(new NlqIsitRequest(HaritaId), CancellationToken.None);
        await new AskQuestionHandler(new SahteHaritalar(), ctx, cevirici, new SahteGonderici())
            .Handle(new AskQuestionRequest(HaritaId, "servis başına talep sayısı"), CancellationToken.None);

        Assert.NotNull(cevirici.IsitilanOnek);
        Assert.StartsWith(cevirici.IsitilanOnek, cevirici.GidenIstem, StringComparison.Ordinal);
        // Önek soruyu İÇERMEZ; içerseydi her soru farklı önek olur, önbellek hiç tutmazdı.
        Assert.DoesNotContain("servis başına talep sayısı", cevirici.IsitilanOnek);
        // Metrik kataloğu önekin İÇİNDE: harita başına sabit olduğu için önbelleğe girmeli.
        Assert.Contains("Toplam Ciro", cevirici.IsitilanOnek);
    }

    [Fact]
    public async Task isitma_duzeltme_turunda_da_ayni_onekle_baslar()
    {
        // Düzeltme istemi öneke "## Mevcut sorgu" ekler ama BAŞLANGICI aynıdır — yani bir düzeltme
        // turu, ısıtmanın kazancını korur (yalnız eklenen kısım yeniden işlenir).
        var kiraci = Guid.NewGuid();
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(kiraci));
        await ctx.Database.EnsureCreatedAsync();
        ctx.Haritalar.Add(new DataMap { Id = HaritaId, TenantId = kiraci, Ad = "H", BaglantiId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();

        var onceki = new QueryModel(HaritaId, [new SelectedField(TaleplerId, "ServisId")], null, [], SatirLimiti: 10);
        var cevirici = new SahteCevirici("""{ "alanlar": [ { "tablo": "Talepler", "kolon": "Servis No" } ], "limit": 2 }""");
        await new NlqIsitHandler(new SahteHaritalar(), ctx, cevirici)
            .Handle(new NlqIsitRequest(HaritaId), CancellationToken.None);
        await new AskQuestionHandler(new SahteHaritalar(), ctx, cevirici, new SahteGonderici())
            .Handle(new AskQuestionRequest(HaritaId, "ilk 2yi göster",
                System.Text.Json.JsonSerializer.Serialize(onceki)), CancellationToken.None);

        Assert.StartsWith(cevirici.IsitilanOnek!, cevirici.GidenIstem, StringComparison.Ordinal);
        Assert.Contains("## Mevcut sorgu", cevirici.GidenIstem);
    }

    [Fact]
    public async Task isitma_bilinmeyen_haritada_HATA_FIRLATMAZ()
    {
        // Isıtma iyileştirmedir: harita bulunamazsa ekranı hata ile karşılamaz, sessizce geçer.
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Guid.NewGuid()));
        await ctx.Database.EnsureCreatedAsync();
        var cevirici = new SahteCevirici("{}");

        var sonuc = await new NlqIsitHandler(new SahteHaritalar(), ctx, cevirici)
            .Handle(new NlqIsitRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.False(sonuc.Isitildi);
        Assert.Null(cevirici.IsitilanOnek);
    }

    [Fact]
    public async Task handler_bilinmeyen_harita_404_uretir()
    {
        await using var c = new SqliteConnection("DataSource=:memory:");
        await c.OpenAsync();
        await using var ctx = new KdsDbContext(
            new DbContextOptionsBuilder<KdsDbContext>().UseSqlite(c).Options, new FixedTenantContext(Guid.NewGuid()));
        await ctx.Database.EnsureCreatedAsync();
        var handler = new AskQuestionHandler(new SahteHaritalar(), ctx, new SahteCevirici("{}"), new SahteGonderici());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AskQuestionRequest(Guid.NewGuid(), "soru"), CancellationToken.None));
    }
}
