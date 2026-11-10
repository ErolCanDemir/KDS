using System.Text.Json;
using System.Text.Json.Serialization;
using Kds.Execution;
using Kds.Contracts.Connections;
using Kds.Contracts.DataMaps;
using Kds.Contracts.QueryModel;
using Xunit;

namespace Kds.Tests;

/// <summary>S7 SQL üretici — ALTIN-ÖRNEK test seti (JSON model → beklenen parametreli SQL) +
/// güvenlik (kimlik enjeksiyonu escape) + model/dataMap tutarlılık redleri. Üretici deterministiktir;
/// DB gerekmez — model + DataMapDetail doğrudan kurulur.</summary>
public sealed class QueryGeneratorTests
{
    private static readonly Guid TM = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"); // Musteriler
    private static readonly Guid TS = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"); // Siparisler
    private static readonly Guid TU = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"); // Urunler

    private static readonly MssqlQueryGenerator Uretici = new();

    private static string D(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n');

    private static DataMapDetail Harita() => new(
        Guid.NewGuid(), "Satis Haritasi", Guid.NewGuid(), DataMode.Canli,
        [
            new DataMapTableDto(TM, "dbo", "Musteriler", "Musteriler",
            [
                new DataMapColumnDto("MusteriNo", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Ad", "Musteri Adi", ColumnRole.Boyut, "nvarchar"),
                new DataMapColumnDto("Sehir", null, ColumnRole.Boyut, "nvarchar"),
            ]),
            new DataMapTableDto(TS, "dbo", "Siparisler", "Siparisler",
            [
                new DataMapColumnDto("SiparisNo", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("MusteriId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("UrunId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Tutar", null, ColumnRole.Olcu, "money"),
                new DataMapColumnDto("Tarih", null, ColumnRole.Boyut, "date"),
            ]),
            new DataMapTableDto(TU, "dbo", "Urunler", "Urunler",
            [
                new DataMapColumnDto("UrunNo", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Kategori", null, ColumnRole.Boyut, "nvarchar"),
            ]),
        ],
        [
            new DataMapRelationshipDto(TS, "MusteriId", TM, "MusteriNo", true),
            new DataMapRelationshipDto(TS, "UrunId", TU, "UrunNo", true),
        ]);

    // ---- Test turu M4/M13/M15: kullanılan-öncelikli join ağacı ----
    // Kullanıcının servis haritasının küçültülmüşü: Kisiler—KisiAdresleri—Adresler—Iller zinciri +
    // Kisiler.DogumYeriIlId→Iller KESTİRMESİ + Adresler—Ilceler—Iller alternatif ara yolu.

    private static readonly Guid JK = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"); // Kisiler
    private static readonly Guid JA = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"); // KisiAdresleri
    private static readonly Guid JD = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003"); // Adresler
    private static readonly Guid JI = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000004"); // Iller
    private static readonly Guid JC = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000005"); // Ilceler
    private static readonly Guid JT = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000006"); // Talepler

    private static DataMapDetail ServisHarita() => new(
        Guid.NewGuid(), "Servis", Guid.NewGuid(), DataMode.Canli,
        [
            new DataMapTableDto(JK, "kisi", "Kisiler", "Kisiler",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Ad", null, ColumnRole.Boyut, "nvarchar"),
                new DataMapColumnDto("DogumYeriIlId", null, ColumnRole.Anahtar, "int"),
            ]),
            new DataMapTableDto(JA, "kisi", "KisiAdresleri", "KisiAdresleri",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("KisiId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("AdresId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("AktifMi", null, ColumnRole.Boyut, "bit"),
            ]),
            new DataMapTableDto(JT, "talep", "Talepler", "Talepler",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("GetirenKisiId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("ToplamTutar", null, ColumnRole.Olcu, "money"),
            ]),
            new DataMapTableDto(JD, "adres", "Adresler", "Adresler",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("IlceId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("IlId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("AdresTipiId", null, ColumnRole.Boyut, "int"),
            ]),
            new DataMapTableDto(JI, "ortak", "Iller", "Iller",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Ad", null, ColumnRole.Boyut, "nvarchar"),
                new DataMapColumnDto("BolgeAdi", null, ColumnRole.Boyut, "nvarchar"),
            ]),
            new DataMapTableDto(JC, "ortak", "Ilceler", "Ilceler",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("IlId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Ad", null, ColumnRole.Boyut, "nvarchar"),
            ]),
        ],
        [
            // BİLEREK yanıltıcı sıra: kestirme + ara yollar önce — eski kör BFS bu sırayla yanılıyordu.
            new DataMapRelationshipDto(JK, "DogumYeriIlId", JI, "Id", true),
            new DataMapRelationshipDto(JC, "IlId", JI, "Id", true),
            new DataMapRelationshipDto(JD, "IlceId", JC, "Id", true),
            new DataMapRelationshipDto(JA, "KisiId", JK, "Id", true),
            new DataMapRelationshipDto(JA, "AdresId", JD, "Id", true),
            new DataMapRelationshipDto(JD, "IlId", JI, "Id", true),
            new DataMapRelationshipDto(JT, "GetirenKisiId", JK, "Id", true),
        ]);

    [Fact]
    public void il_bazinda_aggregate_sorgu_uretilir_ve_gruplanir()
    {
        // M19: kullanıcının referansı — il bazında COUNT DISTINCT + SUM + GROUP BY + LEFT Talepler.
        var model = new QueryModel(Guid.NewGuid(),
            [
                new SelectedField(JI, "Ad", TakmaAd: "Il"),
                new SelectedField(JI, "BolgeAdi", TakmaAd: "Bolge"),
                new SelectedField(JK, "Id", ToplamaTuru.BenzersizSay, "KisiSayisi"),
                new SelectedField(JT, "Id", ToplamaTuru.BenzersizSay, "TalepSayisi"),
                new SelectedField(JT, "ToplamTutar", ToplamaTuru.Topla, "ToplamTutar"),
            ],
            new FilterGroup(MantikBaglaci.Ve, [new FilterCondition(JA, "AktifMi", KarsilastirmaOp.Esit, [true])]),
            [new SortItem(JT, "ToplamTutar", ToplamaTuru.Topla, Azalan: true)], null,
            KokTabloId: JK,
            JoinAyarlari: [new JoinSetting(JT, JK, JoinType.Left)]);

        var s = Uretici.Generate(model, ServisHarita()).Sql;

        Assert.Contains("COUNT(DISTINCT [k].[Id])", s);
        Assert.Contains("SUM(", s);
        Assert.Contains("GROUP BY [i].[Ad], [i].[BolgeAdi]", s);
        Assert.Contains("LEFT JOIN [talep].[Talepler]", s);
    }

    [Fact]
    public void case_kolonu_satir_duzeyi_uretilir_aggregate_sorguda_group_by_a_girer()
    {
        // M29: satır-düzeyi CASE WHEN — sabit sonuçlar parametre; koşul tablosu otomatik join'lenir;
        // aggregate'li sorguda CASE ifadesi GROUP BY'a da girer (boyut ifadesi).
        var durum = new CaseColumn(
            [new CaseBranch(new FilterCondition(JA, "AktifMi", KarsilastirmaOp.Esit, [true]), Sabit: "Aktif")],
            TakmaAd: "Durum", VarsayilanSabit: "Pasif");

        var duz = new QueryModel(Guid.NewGuid(), [new SelectedField(JK, "Ad")], null, [], null, CaseAlanlar: [durum]);
        var s1 = Uretici.Generate(duz, ServisHarita()).Sql;
        Assert.Contains("CASE WHEN [k1].[AktifMi] = @p", s1);
        Assert.Contains("END AS [Durum]", s1);
        Assert.Contains("JOIN [kisi].[KisiAdresleri]", s1); // koşul tablosu join'lendi
        Assert.DoesNotContain("GROUP BY", s1);

        var grup = new QueryModel(Guid.NewGuid(),
            [new SelectedField(JK, "Ad"), new SelectedField(JK, "Id", ToplamaTuru.Say, "Adet")],
            null, [], null, CaseAlanlar: [durum]);
        var s2 = Uretici.Generate(grup, ServisHarita()).Sql;
        Assert.Contains("GROUP BY", s2);
        Assert.Contains("ELSE @p", s2);
        Assert.True(s2.LastIndexOf("CASE WHEN") > s2.IndexOf("CASE WHEN"), "CASE ifadesi GROUP BY'da tekrar etmeli");
    }

    [Fact]
    public void join_agaci_kanvas_komsusunu_secer_ara_tablo_yoluna_sapmaz()
    {
        // M13: Kisiler → KisiAdresleri → Adresler — Adresler, (ilişki sırası yüzünden) Iller/Ilceler
        // ara yolu yerine kanvastaki KisiAdresleri'ne bağlanmalı; plana ara tablo hiç girmemeli.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(JK, "Ad"), new SelectedField(JA, "AdresId"), new SelectedField(JD, "AdresTipiId")],
            null, [], null);

        var s = Uretici.Generate(model, ServisHarita()).Sql;

        Assert.Contains("JOIN [kisi].[KisiAdresleri] AS [k1] ON [k1].[KisiId] = [k].[Id]", s);
        Assert.Contains("JOIN [adres].[Adresler] AS [a] ON [a].[Id] = [k1].[AdresId]", s);
        Assert.DoesNotContain("Ilceler", s);
        Assert.DoesNotContain("Iller", s);
    }

    [Fact]
    public void tablo_sirasi_otomatik_ebeveyn_secim_onceligini_degistirir()
    {
        // M18: kullanıcı Iller'i SIRADA öne alırsa, Iller adres zinciri yerleşmeden işlenir →
        // tek yerleşik aday Kisiler (DogumYeriIlId) olur — sıra gerçek bir tercihtir.
        var model = new QueryModel(Guid.NewGuid(),
            [
                new SelectedField(JK, "Ad"), new SelectedField(JA, "AdresId"),
                new SelectedField(JD, "AdresTipiId"), new SelectedField(JI, "Ad", ToplamaTuru.Yok, "Il"),
            ],
            null, [], null,
            TabloSirasi: [JK, JI, JA, JD]);

        var s = Uretici.Generate(model, ServisHarita()).Sql;

        Assert.Contains("DogumYeriIlId", s); // sırasızken Adresler.IlId seçilirdi (üstteki test)
    }

    [Fact]
    public void join_agaci_en_derin_ebeveyni_secer_kestirme_fk_degil()
    {
        // M15: Iller kanvasta; adaylar Kisiler.DogumYeriIlId (derinlik 0) ve Adresler.IlId
        // (derinlik 2) → EN DERİN kazanır: Iller adres zincirinin ucuna bağlanır.
        var model = new QueryModel(Guid.NewGuid(),
            [
                new SelectedField(JK, "Ad"), new SelectedField(JA, "AdresId"),
                new SelectedField(JD, "AdresTipiId"), new SelectedField(JI, "Ad", ToplamaTuru.Yok, "Il"),
            ],
            null, [], null);

        var s = Uretici.Generate(model, ServisHarita()).Sql;

        Assert.Contains("JOIN [ortak].[Iller] AS [i] ON [i].[Id] = [a].[IlId]", s);
        Assert.DoesNotContain("DogumYeriIlId", s);
    }

    // ---- Test turu M5: çok-tablolu join koşulu (koşul, ana ebeveyn dışındaki tabloya başvurur) ----
    // Kullanıcının senaryosu: LEFT JOIN stok.ServisStoklari ss
    //   ON ss.ServisId = t.ServisId AND ss.ParcaId = tp.ParcaId
    // Guid'ler BİLEREK ters: ServisStoklari < TalepParcalari — eski (derinlik,Guid) sıralaması
    // ServisStoklari'yı öne alırdı; topolojik sıra, koşulun başvurduğu TalepParcalari'yı öne almalı.

    private static readonly Guid MT = Guid.Parse("cccccccc-0000-0000-0000-000000000001"); // Talepler
    private static readonly Guid MS = Guid.Parse("cccccccc-0000-0000-0000-000000000002"); // ServisStoklari
    private static readonly Guid MP = Guid.Parse("cccccccc-0000-0000-0000-000000000003"); // TalepParcalari

    private static DataMapDetail StokHarita() => new(
        Guid.NewGuid(), "Stok", Guid.NewGuid(), DataMode.Canli,
        [
            new DataMapTableDto(MT, "talep", "Talepler", "Talepler",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("ServisId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("TalepNo", null, ColumnRole.Boyut, "nvarchar"),
            ]),
            new DataMapTableDto(MS, "stok", "ServisStoklari", "ServisStoklari",
            [
                new DataMapColumnDto("ServisId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("ParcaId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("MevcutAdet", null, ColumnRole.Olcu, "int"),
            ]),
            new DataMapTableDto(MP, "talep", "TalepParcalari", "TalepParcalari",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("TalepId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("ParcaId", null, ColumnRole.Anahtar, "int"),
                new DataMapColumnDto("Adet", null, ColumnRole.Olcu, "int"),
            ]),
        ],
        [
            // ServisStoklari'nın sorgudaki tablolara HARİTADA ilişkisi yok — kullanıcı elle bağlar.
            new DataMapRelationshipDto(MP, "TalepId", MT, "Id", true),
        ]);

    [Fact]
    public void cok_tablolu_join_kosulu_iki_ayri_tabloya_baglanir_ve_sira_dogru()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(MT, "TalepNo"), new SelectedField(MP, "Adet"), new SelectedField(MS, "MevcutAdet")],
            null, [], null,
            JoinAyarlari:
            [
                new JoinSetting(MS, MT, JoinType.Left,
                [
                    new JoinCondition("ServisId", "ServisId"),           // ana ebeveyn (Talepler)
                    new JoinCondition("ParcaId", "ParcaId", MP),          // BAŞKA tablo (TalepParcalari)
                ]),
            ]);

        var s = Uretici.Generate(model, StokHarita()).Sql;

        Assert.Contains(
            "LEFT JOIN [stok].[ServisStoklari] AS [s] ON [s].[ServisId] = [t].[ServisId] AND [s].[ParcaId] = [t1].[ParcaId]", s);
        // Sıra: koşulun başvurduğu TalepParcalari, ServisStoklari'dan ÖNCE join'lenmeli.
        Assert.True(s.IndexOf("[TalepParcalari]", StringComparison.Ordinal) < s.IndexOf("[ServisStoklari]", StringComparison.Ordinal),
            "TalepParcalari, ServisStoklari'dan önce join'lenmeliydi:\n" + s);
    }

    [Fact]
    public void kosulun_basvurdugu_tablo_secili_alan_olmasa_da_otomatik_joinlenir()
    {
        // TalepParcalari'dan alan seçilmedi; ama koşul ona başvuruyor → yine join'lenir.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(MT, "TalepNo"), new SelectedField(MS, "MevcutAdet")],
            null, [], null,
            JoinAyarlari:
            [
                new JoinSetting(MS, MT, JoinType.Left,
                [
                    new JoinCondition("ServisId", "ServisId"),
                    new JoinCondition("ParcaId", "ParcaId", MP),
                ]),
            ]);

        var s = Uretici.Generate(model, StokHarita()).Sql;

        Assert.Contains("[talep].[TalepParcalari]", s);
        Assert.Contains("AND [s].[ParcaId] = [t1].[ParcaId]", s);
    }

    [Fact]
    public void kosul_tablosuyla_iliskisi_olan_otomatik_tablo_ayarliya_degil_kendi_yoluna_baglanir()
    {
        // Marka deseni (E2E'nin yakaladığı gerileme): Araclar AYARLI (Markalar'a, koşulu Modeller'e
        // başvuruyor); Modeller'in Markalar'a BAĞIMSIZ yolu var. Ayarlı tablo erken yerleşirse
        // Modeller "en derin" diye Araclar'a bağlanıp döngü doğuruyordu — ayarlı tablo artık koşul
        // tabloları yerleşmeden yerleşmez.
        var mk = Guid.Parse("dddddddd-0000-0000-0000-000000000001"); // Markalar
        var md = Guid.Parse("dddddddd-0000-0000-0000-000000000002"); // Modeller
        var ar = Guid.Parse("dddddddd-0000-0000-0000-000000000003"); // Araclar
        var harita = new DataMapDetail(Guid.NewGuid(), "Arac", Guid.NewGuid(), DataMode.Canli,
            [
                new DataMapTableDto(mk, "ortak", "Markalar", "Markalar",
                    [new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"), new DataMapColumnDto("Ad", null, ColumnRole.Boyut, "nvarchar")]),
                new DataMapTableDto(md, "ortak", "Modeller", "Modeller",
                    [new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"), new DataMapColumnDto("MarkaId", null, ColumnRole.Anahtar, "int"), new DataMapColumnDto("SegmentKodu", null, ColumnRole.Boyut, "nvarchar")]),
                new DataMapTableDto(ar, "arac", "Araclar", "Araclar",
                    [new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int"), new DataMapColumnDto("MarkaId", null, ColumnRole.Anahtar, "int"), new DataMapColumnDto("ModelId", null, ColumnRole.Anahtar, "int"), new DataMapColumnDto("Plaka", null, ColumnRole.Boyut, "nvarchar")]),
            ],
            [
                new DataMapRelationshipDto(md, "MarkaId", mk, "Id", true),
                new DataMapRelationshipDto(ar, "MarkaId", mk, "Id", true),
                new DataMapRelationshipDto(ar, "ModelId", md, "Id", true),
            ]);

        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(mk, "Ad"), new SelectedField(md, "SegmentKodu"), new SelectedField(ar, "Plaka")],
            null, [], null,
            JoinAyarlari:
            [
                new JoinSetting(ar, mk, JoinType.Left,
                [
                    new JoinCondition("MarkaId", "Id"),
                    new JoinCondition("ModelId", "Id", md),
                ]),
            ]);

        var s = Uretici.Generate(model, harita).Sql;

        Assert.Contains("JOIN [ortak].[Modeller] AS [m1] ON [m1].[MarkaId] = [m].[Id]", s);      // Markalar'a, Araclar'a değil
        Assert.Contains("LEFT JOIN [arac].[Araclar] AS [a] ON [a].[MarkaId] = [m].[Id] AND [a].[ModelId] = [m1].[Id]", s);
    }

    [Fact]
    public void karsilikli_kosul_bagimliligi_donguyu_acikca_reddeder()
    {
        // MS'nin koşulu MP'ye, MP'nin koşulu MS'ye başvuruyor → topolojik sıra kurulamaz.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(MT, "TalepNo"), new SelectedField(MP, "Adet"), new SelectedField(MS, "MevcutAdet")],
            null, [], null,
            JoinAyarlari:
            [
                new JoinSetting(MS, MT, JoinType.Inner, [new JoinCondition("ParcaId", "ParcaId", MP)]),
                new JoinSetting(MP, MT, JoinType.Inner, [new JoinCondition("ParcaId", "ParcaId", MS)]),
            ]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, StokHarita()));
        Assert.Contains("döngü", hata.Message);
    }

    [Fact]
    public void filtre_degerinde_dinamik_tarih_kosuma_gore_cozulur()
    {
        // Y5: filtreye doğrudan "$bugun-6ay" yazılabilir — parametreye bağlamaya gerek yok.
        // Kaydedilen rapor her çalıştırmada KAYAN pencereyi gösterir (sabit tarih yazılmaz).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo")],
            new FilterCondition(TS, "Tarih", KarsilastirmaOp.BuyukEsit, ["$bugun-6ay"]),
            [], null);

        var sonuc = Uretici.Generate(model, Harita());

        Assert.Contains("WHERE [s].[Tarih] >= @p0", sonuc.Sql);
        Assert.Equal(DateTime.Today.AddMonths(-6).Date, sonuc.Parametreler["@p0"]);
    }

    [Fact]
    public void dinamik_gorunumlu_metin_degeri_bozulmaz()
    {
        // "$100" gibi normal bir değer dinamik tarih SANILMAMALI (dilbilgisine uymaz → aynen kalır).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")],
            new FilterCondition(TM, "Sehir", KarsilastirmaOp.Esit, ["$100"]),
            [], null);

        Assert.Equal("$100", Uretici.Generate(model, Harita()).Parametreler["@p0"]);
    }

    [Fact]
    public void tarih_araligi_bugun_belirteci_calistirma_gunune_cozulur()
    {
        // M25: TarihAralik ucu "$bugun" → gönderim/çalıştırma GÜNÜNÜN tarihi (dinamik varsayılan).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")],
            new FilterGroup(MantikBaglaci.Ve,
            [
                new FilterCondition(TS, "Tarih", KarsilastirmaOp.Arasinda,
                [
                    new Dictionary<string, object?> { ["$param"] = "don", ["parca"] = "baslangic" },
                    new Dictionary<string, object?> { ["$param"] = "don", ["parca"] = "bitis" },
                ]),
            ]),
            [], null,
            Parametreler: [new ReportParameter("don", "Dönem", ParametreTipi.TarihAralik, Zorunlu: true)]);
        var degerler = new Dictionary<string, object?>
        {
            ["don"] = new Dictionary<string, object?> { ["baslangic"] = "2026-01-01", ["bitis"] = "$bugun" },
        };

        var sonuc = Uretici.Generate(model, Harita(), degerler);

        Assert.Contains(sonuc.Parametreler.Values, v => v is DateTime t && t.Date == DateTime.Today);
    }

    [Fact]
    public void alansiz_ek_tablo_joine_katilir_selecte_girmez()
    {
        // M17: kullanıcı tabloyu alan seçmeden sorguya ekler — JOIN'e katılır, SELECT'e girmez.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")],
            null, [], null,
            EkTablolar: [TS]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("JOIN [dbo].[Siparisler]", s);
        Assert.DoesNotContain("[s].[SiparisNo]", s); // SELECT'te Siparisler kolonu yok
        Assert.StartsWith("SELECT [m].[Sehir] AS [Sehir]", s.Replace("\r\n", "\n"));
    }

    [Fact]
    public void basit_tek_tablo_secim_group_ve_join_uretmez()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TM, "Ad")],
            null, [], null);

        var result = Uretici.Generate(model, Harita());

        Assert.Equal(D("""
            SELECT [m].[Sehir] AS [Sehir], [m].[Ad] AS [Ad]
            FROM [dbo].[Musteriler] AS [m]
            """), D(result.Sql));
        Assert.Empty(result.Parametreler);
    }

    // İki tablolu join + gruplu + filtreli senaryo (00-analiz §8 başarı ölçütü #1'in modeli).
    private static QueryModel BayrakModel() => new(Guid.NewGuid(),
        [
            new SelectedField(TM, "Sehir"),
            new SelectedField(TU, "Kategori"),
            new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "ToplamSatis"),
            new SelectedField(TS, "SiparisNo", ToplamaTuru.Say, "SiparisSayisi"),
        ],
        new FilterGroup(MantikBaglaci.Ve,
        [
            new FilterCondition(TS, "Tarih", KarsilastirmaOp.BuyukEsit, ["2026-01-01"]),
            new FilterCondition(TM, "Sehir", KarsilastirmaOp.Icinde, ["Ankara", "Izmir"]),
        ]),
        [new SortItem(TS, "Tutar", ToplamaTuru.Topla, Azalan: true)],
        1000);

    [Fact]
    public void bayrak_agg_join_filtre_group_sort_limit()
    {
        var result = Uretici.Generate(BayrakModel(), Harita());

        Assert.Equal(D("""
            SELECT TOP (@satir) [m].[Sehir] AS [Sehir], [u].[Kategori] AS [Kategori], SUM([s].[Tutar]) AS [ToplamSatis], COUNT([s].[SiparisNo]) AS [SiparisSayisi]
            FROM [dbo].[Musteriler] AS [m]
            JOIN [dbo].[Siparisler] AS [s] ON [s].[MusteriId] = [m].[MusteriNo]
            JOIN [dbo].[Urunler] AS [u] ON [u].[UrunNo] = [s].[UrunId]
            WHERE ([s].[Tarih] >= @p0 AND [m].[Sehir] IN (@p1, @p2))
            GROUP BY [m].[Sehir], [u].[Kategori]
            ORDER BY [ToplamSatis] DESC
            """), D(result.Sql));

        // Tarih kolonu "date" tipli → string değer DateTime'a çevrilir (tipli parametre).
        Assert.Equal(new DateTime(2026, 1, 1), result.Parametreler["@p0"]);
        Assert.Equal("Ankara", result.Parametreler["@p1"]);
        Assert.Equal("Izmir", result.Parametreler["@p2"]);
        Assert.Equal(1000, result.Parametreler["@satir"]);
        Assert.Equal(4, result.Parametreler.Count);
    }

    [Fact]
    public void uretilen_sql_select_only_bekciden_gecer()
    {
        // Tek kapı garantisi: üretilen SQL, ham SQL ekranıyla aynı bekçiden geçmeli.
        var sql = Uretici.Generate(BayrakModel(), Harita()).Sql;
        var karar = new ScriptDomQueryGuard().Inspect(sql);
        Assert.True(karar.Gecerli, karar.Sebep);
    }

    [Fact]
    public void filtre_operatorleri_ve_deger_parametrelesir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Ad")],
            new FilterGroup(MantikBaglaci.Ve,
            [
                new FilterCondition(TM, "Sehir", KarsilastirmaOp.Arasinda, ["A", "M"]),
                new FilterCondition(TM, "Ad", KarsilastirmaOp.Icerir, ["ali"]),
                new FilterCondition(TM, "MusteriNo", KarsilastirmaOp.Bos, []),
            ]),
            [], null);

        var result = Uretici.Generate(model, Harita());

        /* 'İçerir' harf/aksan duyarsız çalışır (S39): kullanıcı "AUDİ" yazıp veride "Audi"
         * durduğunda varsayılan collation eşleştirmiyor ve sorgu sessizce 0 satır döndürüyordu.
         * Collation Latin_AI'dir, Türkçe DEĞİL: ölçüldü ki Türkçe collation İ/i ile I/ı ayrımını
         * korur ve 'Audi' ↔ 'AUDİ' eşleşmez; Latin AI noktayı aksan sayıp eşleştirir.
         * LIKE zaten indeks aramasını kullanmadığı için ek maliyet yok; eşitlik (=) bilerek
         * dokunulmadan bırakıldı (orada collation seek'i bozardı). */
        Assert.Contains("WHERE ([m].[Sehir] BETWEEN @p0 AND @p1 AND [m].[Ad] COLLATE SQL_Latin1_General_CP1_CI_AI LIKE @p2 AND [m].[MusteriNo] IS NULL)", result.Sql);
        Assert.Equal("A", result.Parametreler["@p0"]);
        Assert.Equal("M", result.Parametreler["@p1"]);
        Assert.Equal("%ali%", result.Parametreler["@p2"]); // Icerir: değer %..% ile sarılır, SQL'e değil
        Assert.Equal(3, result.Parametreler.Count);
    }

    [Fact]
    public void ic_ice_ve_veya_dogru_parantezlenir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Ad")],
            new FilterGroup(MantikBaglaci.Ve,
            [
                new FilterCondition(TM, "Sehir", KarsilastirmaOp.Esit, ["Ankara"]),
                new FilterGroup(MantikBaglaci.Veya,
                [
                    new FilterCondition(TM, "Ad", KarsilastirmaOp.Esit, ["a"]),
                    new FilterCondition(TM, "Ad", KarsilastirmaOp.Esit, ["b"]),
                ]),
            ]),
            [], null);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("WHERE ([m].[Sehir] = @p0 AND ([m].[Ad] = @p1 OR [m].[Ad] = @p2))", result.Sql);
    }

    [Fact]
    public void kimlik_enjeksiyonu_koseli_parantez_kacisiyla_kapanir()
    {
        var kotu = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var kolonAdi = "Ad]; DROP TABLE Musteriler--";
        var dataMap = new DataMapDetail(Guid.NewGuid(), "K", Guid.NewGuid(), DataMode.Canli,
            [new DataMapTableDto(kotu, "dbo", "T", "T", [new DataMapColumnDto(kolonAdi, null, ColumnRole.Boyut, "nvarchar")])],
            []);
        var model = new QueryModel(Guid.NewGuid(), [new SelectedField(kotu, kolonAdi)], null, [], null);

        var sql = Uretici.Generate(model, dataMap).Sql;

        // ] -> ]] kaçışı: kötü ad tek bir parantezli kimlik olarak kalır, ifadeden kaçamaz.
        Assert.Contains("[Ad]]; DROP TABLE Musteriler--]", sql);
        Assert.DoesNotContain("Ad]; DROP", sql); // kaçışsız (tek ]) hâli asla görünmez
    }

    [Fact]
    public void bos_alan_secimi_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(), [], null, [], null);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void haritada_olmayan_kolon_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(), [new SelectedField(TM, "OlmayanKolon")], null, [], null);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void iliskisiz_tablolar_join_yolu_yoksa_reddedilir()
    {
        var a = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var b = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
        var dataMap = new DataMapDetail(Guid.NewGuid(), "Kopuk", Guid.NewGuid(), DataMode.Canli,
            [
                new DataMapTableDto(a, "dbo", "A", "A", [new DataMapColumnDto("x", null, ColumnRole.Boyut, "int")]),
                new DataMapTableDto(b, "dbo", "B", "B", [new DataMapColumnDto("y", null, ColumnRole.Boyut, "int")]),
            ],
            []); // ilişki yok
        var model = new QueryModel(Guid.NewGuid(), [new SelectedField(a, "x"), new SelectedField(b, "y")], null, [], null);

        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, dataMap));
    }

    [Fact]
    public void secili_olmayan_alan_uzerinde_siralama_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")],
            null,
            [new SortItem(TM, "Ad")], // Ad seçili değil
            null);

        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void kotucul_takma_ad_da_koseli_parantezle_kacilir()
    {
        // TakmaAd kullanıcı girdisi; kolon adıyla aynı Q() yolundan geçer, ifadeden kaçamaz.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir", ToplamaTuru.Yok, "x] AS y, (SELECT 1)")],
            null, [], null);

        var sql = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("AS [x]] AS y, (SELECT 1)]", sql);
        Assert.DoesNotContain("AS x] AS y", sql); // kaçışsız breakout asla görünmez
    }

    [Fact]
    public void aralik_disi_toplama_turu_reddedilir()
    {
        // JSON'da sayısal enum token'ı (ör. 99) System.Text.Json'da bağlanır ama üretici reddeder —
        // yoksa GROUP BY dışı ham kolon = sözdizimsel-geçerli ama semantik-geçersiz SQL.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir", (ToplamaTuru)99)],
            null, [], null);

        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void wire_frontend_json_modeli_deserialize_olur_ve_sql_uretir()
    {
        // S8 frontend'in gönderdiği biçim: polimorfik filtre ('tur' ayrımcısı) + string enum'lar.
        var json = ("""
        {
          "haritaId": "00000000-0000-0000-0000-000000000000",
          "alanlar": [
            { "tabloId": "@TM", "kolon": "Sehir", "toplama": "Yok" },
            { "tabloId": "@TS", "kolon": "Tutar", "toplama": "Topla", "takmaAd": "ToplamSatis" }
          ],
          "filtre": { "tur": "grup", "baglac": "Ve", "cocuklar": [
            { "tur": "kosul", "tabloId": "@TM", "kolon": "Sehir", "op": "Esit", "degerler": ["Ankara"] }
          ]},
          "siralama": [ { "tabloId": "@TS", "kolon": "Tutar", "toplama": "Topla", "azalan": true } ],
          "satirLimiti": 100
        }
        """).Replace("@TM", TM.ToString()).Replace("@TS", TS.ToString());

        // ASP.NET Core'un varsayılanını yansıt: camelCase + case-insensitive + string enum.
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        opts.Converters.Add(new JsonStringEnumConverter());
        var model = JsonSerializer.Deserialize<QueryModel>(json, opts)!;

        var grup = Assert.IsType<FilterGroup>(model.Filtre);
        var kosul = Assert.IsType<FilterCondition>(grup.Cocuklar[0]);
        Assert.Equal(KarsilastirmaOp.Esit, kosul.Op);
        Assert.Equal(ToplamaTuru.Topla, model.Alanlar[1].Toplama);

        var result = Uretici.Generate(model, Harita());
        Assert.Contains("SUM([s].[Tutar]) AS [ToplamSatis]", result.Sql);
        Assert.Contains("WHERE ([m].[Sehir] = @p0)", result.Sql);
        Assert.Contains("ORDER BY [ToplamSatis] DESC", result.Sql);
        // JSON'dan gelen JsonElement, üreticide kolon tipine göre CLR'a çevrilir (nvarchar → string).
        Assert.Equal("Ankara", result.Parametreler["@p0"]);
    }

    // GUID kolonlu mini dataMap — tipli parametre dönüşümü senaryoları için.
    private static (DataMapDetail dataMap, Guid tablo) GuidliHarita()
    {
        var t = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
        return (new DataMapDetail(Guid.NewGuid(), "G", Guid.NewGuid(), DataMode.Canli,
            [new DataMapTableDto(t, "dbo", "Kayitlar", "Kayitlar",
            [
                new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "uniqueidentifier"),
                new DataMapColumnDto("Ad", null, ColumnRole.Boyut, "nvarchar"),
                new DataMapColumnDto("Sayi", null, ColumnRole.Olcu, "int"),
            ])], []), t);
    }

    [Fact]
    public void filtre_degeri_kolon_tipine_cevrilir_guid_ve_int()
    {
        var (dataMap, t) = GuidliHarita();
        var guid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(t, "Ad")],
            new FilterGroup(MantikBaglaci.Ve,
            [
                new FilterCondition(t, "Id", KarsilastirmaOp.Esit, [guid]),
                new FilterCondition(t, "Sayi", KarsilastirmaOp.Buyuk, ["5"]),
            ]),
            [], null);

        var result = Uretici.Generate(model, dataMap);

        Assert.Equal(Guid.Parse(guid), result.Parametreler["@p0"]); // string → Guid (tipli)
        Assert.Equal(5L, result.Parametreler["@p1"]);               // string → tam sayı
    }

    [Fact]
    public void guid_kolona_cevrilemeyen_deger_anlasilir_hatayla_reddedilir()
    {
        // Canlı testte yaşanan senaryo: uniqueidentifier kolona "1" → SQL Server'da icra hatası (500)
        // yerine üretim anında anlaşılır ret (400).
        var (dataMap, t) = GuidliHarita();
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(t, "Ad")],
            new FilterGroup(MantikBaglaci.Ve, [new FilterCondition(t, "Id", KarsilastirmaOp.Esit, ["1"])]),
            [], null);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, dataMap));
        Assert.Contains("Id", hata.Message);
        Assert.Contains("uniqueidentifier", hata.Message);
    }

    [Fact]
    public void bozuk_harita_fantom_ara_tabloyu_reddeder()
    {
        var a = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
        var b = Guid.Parse("dddddddd-0000-0000-0000-000000000002"); // haritada TABLO olarak YOK
        var c = Guid.Parse("dddddddd-0000-0000-0000-000000000003");
        var dataMap = new DataMapDetail(Guid.NewGuid(), "Bozuk", Guid.NewGuid(), DataMode.Canli,
            [
                new DataMapTableDto(a, "dbo", "A", "A", [new DataMapColumnDto("x", null, ColumnRole.Boyut, "int")]),
                new DataMapTableDto(c, "dbo", "C", "C", [new DataMapColumnDto("y", null, ColumnRole.Boyut, "int")]),
            ],
            [
                new DataMapRelationshipDto(a, "aid", b, "id", false), // b tabloya çözülemez
                new DataMapRelationshipDto(b, "cid", c, "id", false),
            ]);
        var model = new QueryModel(Guid.NewGuid(), [new SelectedField(a, "x"), new SelectedField(c, "y")], null, [], null);

        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, dataMap));
    }

    // İki tablo arasında İKİ ilişki olan mini dataMap (join seçimi senaryosu).
    private static (DataMapDetail dataMap, Guid a, Guid b) CiftIliskiliHarita()
    {
        var a = Guid.Parse("ffffffff-0000-0000-0000-00000000000a");
        var b = Guid.Parse("ffffffff-0000-0000-0000-00000000000b");
        return (new DataMapDetail(Guid.NewGuid(), "Cift", Guid.NewGuid(), DataMode.Canli,
            [
                new DataMapTableDto(a, "dbo", "A", "A",
                    [new DataMapColumnDto("x", null, ColumnRole.Boyut, "int"),
                     new DataMapColumnDto("b1", null, ColumnRole.Anahtar, "int"),
                     new DataMapColumnDto("b2", null, ColumnRole.Anahtar, "int")]),
                new DataMapTableDto(b, "dbo", "B", "B",
                    [new DataMapColumnDto("y", null, ColumnRole.Boyut, "int"),
                     new DataMapColumnDto("id", null, ColumnRole.Anahtar, "int"),
                     new DataMapColumnDto("id2", null, ColumnRole.Anahtar, "int")]),
            ],
            [
                new DataMapRelationshipDto(a, "b1", b, "id", true),   // 1. ilişki
                new DataMapRelationshipDto(a, "b2", b, "id2", true),  // 2. ilişki (aynı çift)
            ]), a, b);
    }

    [Fact]
    public void join_secimi_yoksa_ilk_iliski_secimle_ikincisi_kullanilir()
    {
        var (dataMap, a, b) = CiftIliskiliHarita();
        var alanlar = new[] { new SelectedField(a, "x"), new SelectedField(b, "y") };

        // Seçim yok → BFS ilk ilişkiyi kullanır (b1 = id).
        var vars = Uretici.Generate(new QueryModel(Guid.NewGuid(), alanlar, null, [], null), dataMap);
        Assert.Contains("ON [b].[id] = [a].[b1]", vars.Sql);
        Assert.DoesNotContain("[id2]", vars.Sql);

        // Seçim var → o çift için 2. ilişki (b2 = id2) kullanılır — kullanıcının düzeltmesi.
        var secimli = Uretici.Generate(
            new QueryModel(Guid.NewGuid(), alanlar, null, [], null,
                Joinler: [new JoinSelection(a, "b2", b, "id2")]), dataMap);
        Assert.Contains("ON [b].[id2] = [a].[b2]", secimli.Sql);
        Assert.DoesNotContain("[b1]", secimli.Sql);
    }

    [Fact]
    public void join_secimi_haritada_olmayan_kolonu_reddeder()
    {
        var (dataMap, a, b) = CiftIliskiliHarita();
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(a, "x"), new SelectedField(b, "y")], null, [], null,
            Joinler: [new JoinSelection(a, "yok", b, "id")]); // 'yok' kolonu A'da yok
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, dataMap));
    }

    [Fact]
    public void join_secimi_iliski_olmayan_kolon_ciftini_de_kullanabilir()
    {
        // Kullanıcının çizgiye tıklayıp join kolonlarını elle değiştirmesi: seçilen çift haritada
        // TANIMLI bir ilişki olmasa da (a.x = b.y) — kolonlar var olduğu sürece — kabul edilir.
        // Join YAPISI yine haritanın ilişkisinden gelir (a-b bağlı), yalnız kolonlar override.
        var (dataMap, a, b) = CiftIliskiliHarita();
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(a, "x"), new SelectedField(b, "y")], null, [], null,
            Joinler: [new JoinSelection(a, "x", b, "y")]);
        var vars = Uretici.Generate(model, dataMap);
        Assert.Contains("ON [b].[y] = [a].[x]", vars.Sql);
        Assert.DoesNotContain("[b1]", vars.Sql);
    }

    [Fact]
    public void hesaplanan_kolon_satir_duzeyinde_group_by_a_girer()
    {
        // Y1: toplamasız formül = satır düzeyi BOYUT → aggregate'li sorguda GROUP BY'a eklenir
        // (bileşik/CASE kolonlarıyla aynı kural). Yer tutucular alias'lı kolona çevrilir.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo", ToplamaTuru.Say, "Adet")], null, [], null,
            HesaplananAlanlar: [new ComputedColumn("{Siparisler.Tutar} * 0.20", "Kdv")]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("([s].[Tutar] * 0.20) AS [Kdv]", s);
        Assert.Contains("GROUP BY ([s].[Tutar] * 0.20)", s);
    }

    [Fact]
    public void hesaplanan_kolon_toplama_iceriyorsa_group_by_a_girmez()
    {
        // Y1: SUM/COUNT içeren formül GRUP düzeyindedir — GROUP BY'a eklenmemeli (yoksa SQL patlar).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            HesaplananAlanlar:
            [new ComputedColumn("SUM({Siparisler.Tutar}) * 100.0 / NULLIF(COUNT({Siparisler.SiparisNo}), 0)", "OrtTutar")]);

        var sonuc = Uretici.Generate(model, Harita());

        Assert.Contains("(SUM([s].[Tutar]) * 100.0 / NULLIF(COUNT([s].[SiparisNo]), 0)) AS [OrtTutar]", sonuc.Sql);
        Assert.Contains("GROUP BY [m].[Sehir]", sonuc.Sql);       // yalnız boyut kolonu
        Assert.DoesNotContain("GROUP BY [m].[Sehir], (SUM", sonuc.Sql);
    }

    [Theory]
    [InlineData("{Siparisler.Tutar}; DROP TABLE x", ";")]
    [InlineData("(SELECT 1)", "SELECT")]
    [InlineData("{Siparisler.Tutar} -- yorum", "--")]
    [InlineData("BILINMEYEN({Siparisler.Tutar})", "BILINMEYEN")]
    [InlineData("({Siparisler.Tutar} * 2", "parantez")]
    public void hesaplanan_kolonda_gecersiz_ifade_reddedilir(string ifade, string mesajParcasi)
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            HesaplananAlanlar: [new ComputedColumn(ifade, "X")]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains(mesajParcasi, hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void hesaplanan_kolonda_metin_sabiti_serbesttir()
    {
        // Tek tırnak içindeki kelimeler VERİdir — beyaz liste denetimine takılmamalı.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            HesaplananAlanlar: [new ComputedColumn("ISNULL({Musteriler.Sehir}, 'bilinmiyor SELECT')", "SehirAd")]);

        Assert.Contains("ISNULL([m].[Sehir], 'bilinmiyor SELECT')", Uretici.Generate(model, Harita()).Sql);
    }

    [Theory]
    [InlineData(TarihKovasi.Yil, "DATEADD(year, DATEDIFF(year, 0, [s].[Tarih]), 0)", "Tarih_Yil")]
    [InlineData(TarihKovasi.Ceyrek, "DATEADD(quarter, DATEDIFF(quarter, 0, [s].[Tarih]), 0)", "Tarih_Ceyrek")]
    [InlineData(TarihKovasi.Ay, "DATEADD(month, DATEDIFF(month, 0, [s].[Tarih]), 0)", "Tarih_Ay")]
    // Hafta: gün-modülo deseni (pazartesi başı) — DATEDIFF(week,…) pazarı sonraki haftaya atıyordu.
    [InlineData(TarihKovasi.Hafta, "DATEADD(day, -(DATEDIFF(day, 0, [s].[Tarih]) % 7), CAST([s].[Tarih] AS date))", "Tarih_Hafta")]
    [InlineData(TarihKovasi.Gun, "CAST([s].[Tarih] AS date)", "Tarih_Gun")]
    [InlineData(TarihKovasi.YilNo, "YEAR([s].[Tarih])", "Tarih_YilNo")]
    [InlineData(TarihKovasi.CeyrekNo, "DATEPART(quarter, [s].[Tarih])", "Tarih_CeyrekNo")]
    [InlineData(TarihKovasi.AyNo, "MONTH([s].[Tarih])", "Tarih_AyNo")]
    [InlineData(TarihKovasi.SaatNo, "DATEPART(hour, [s].[Tarih])", "Tarih_SaatNo")]
    public void tarih_kovasi_hem_selecte_hem_group_by_a_ayni_ifadeyle_girer(TarihKovasi kova, string ifade, string alias)
    {
        // Y4: kırılım ifadesi SELECT ile GROUP BY'da BİREBİR aynı olmalı; farklı olursa SQL Server
        // "GROUP BY listesinde değil" der. Varsayılan çıkış adı kırılımı taşır (Tarih_Ay gibi).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "Tarih", Kova: kova), new SelectedField(TS, "Tutar", ToplamaTuru.Topla)],
            null, [], null);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains(ifade + " AS " + "[" + alias + "]", s);
        Assert.Contains("GROUP BY " + ifade, s);
    }

    [Fact]
    public void tarih_kovasi_elle_gruplamada_da_indirgenmis_ifadeyi_kullanir()
    {
        // Kullanıcı GROUP BY listesini elle düzenlediyse kova bilgisi orada da taşınır.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "Tarih", Kova: TarihKovasi.Ay), new SelectedField(TS, "Tutar", ToplamaTuru.Topla)],
            null, [], null,
            Gruplamalar: [new GroupingItem(TS, "Tarih", TarihKovasi.Ay)]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("GROUP BY DATEADD(month, DATEDIFF(month, 0, [s].[Tarih]), 0)", s);
        Assert.DoesNotContain("GROUP BY [s].[Tarih]", s);
    }

    [Fact]
    public void ayni_kolonun_iki_kovasi_ayri_kolon_olur_ve_ayri_siralanir()
    {
        // Genel ihtiyaç: "yıl + ay no" kırılımı (mevsimsellik). İkisi ayrı çıkış, ayrı sıra.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "Tarih", Kova: TarihKovasi.YilNo),
             new SelectedField(TS, "Tarih", Kova: TarihKovasi.AyNo),
             new SelectedField(TS, "Tutar", ToplamaTuru.Topla)],
            null,
            [new SortItem(TS, "Tarih", Kova: TarihKovasi.YilNo),
             new SortItem(TS, "Tarih", Kova: TarihKovasi.AyNo, Azalan: true)],
            null);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("YEAR([s].[Tarih]) AS [Tarih_YilNo]", s);
        Assert.Contains("MONTH([s].[Tarih]) AS [Tarih_AyNo]", s);
        Assert.Contains("ORDER BY [Tarih_YilNo] ASC, [Tarih_AyNo] DESC", s);
    }

    [Fact]
    public void tarih_kovasi_tarih_olmayan_kolonda_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir", Kova: TarihKovasi.Ay)], null, [], null);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("tarih kolonu değil", hata.Message);
    }

    [Fact]
    public void tarih_kovasi_toplamayla_birlikte_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "Tarih", ToplamaTuru.EnCok, Kova: TarihKovasi.Ay)], null, [], null);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("toplama", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void formul_kosulu_toplamasizsa_where_e_gider()
    {
        // Y2: satır düzeyi formül koşulu ("tutarın %20'si 100'den büyük olan satırlar") gruplamadan
        // ÖNCE elenmeli → WHERE. Kullanıcı WHERE/HAVING ayrımını bilmek zorunda değil.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "SiparisNo")], null, [], null,
            IfadeKosullari: [new ExpressionCondition("{Siparisler.Tutar} * 0.20", KarsilastirmaOp.Buyuk, [100])]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("WHERE ([s].[Tutar] * 0.20) > @p0", s);
        Assert.DoesNotContain("HAVING", s);
    }

    [Fact]
    public void formul_kosulu_toplama_iceriyorsa_having_e_gider()
    {
        // Y2: aynı giriş yerinden yazılan ama TOPLAMA içeren koşul gruplama SONRASI uygulanır.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            IfadeKosullari:
            [new ExpressionCondition("SUM({Siparisler.Tutar}) / NULLIF(COUNT({Siparisler.SiparisNo}), 0)",
                KarsilastirmaOp.BuyukEsit, [500])]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("HAVING (SUM([s].[Tutar]) / NULLIF(COUNT([s].[SiparisNo]), 0)) >= @p0", s);
        Assert.DoesNotContain("WHERE", s);
    }

    [Fact]
    public void formul_kosulu_cikis_kolonuna_atif_yapabilir_ve_toplamayi_miras_alir()
    {
        // Y2: [Ciro] bir ÇIKIŞ kolonudur (SUM içerir) → koşul HAVING'e gitmeli ve alias yerine
        // İFADE yazılmalı (SQL, HAVING'de alias'a atıf kabul etmez).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            IfadeKosullari: [new ExpressionCondition("[Ciro]", KarsilastirmaOp.Buyuk, [1000])]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("HAVING ((SUM([s].[Tutar]))) > @p0", s);
        Assert.DoesNotContain("HAVING [Ciro]", s);
    }

    [Fact]
    public void bilinmeyen_cikis_kolonuna_atif_anlasilir_hata_verir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            IfadeKosullari: [new ExpressionCondition("[Yokolan]", KarsilastirmaOp.Buyuk, [1])]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("'[Yokolan]' diye bir çıkış kolonu yok", hata.Message);
        Assert.Contains("Sehir", hata.Message); // kullanılabilir kolonlar sayılır
    }

    [Fact]
    public void hesaplanan_kolon_baska_cikis_kolonlarina_atif_yapabilir()
    {
        // Y2: formül, SELECT'teki başka kolonlara ([Ciro] / [Adet]) atıf yapabilir; atıf toplama
        // taşıdığı için sorgu gruplanır ve hesap GROUP BY'a GİRMEZ.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"),
             new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro"),
             new SelectedField(TS, "SiparisNo", ToplamaTuru.Say, "Adet")], null, [], null,
            HesaplananAlanlar: [new ComputedColumn("[Ciro] / NULLIF([Adet], 0)", "SepetOrtalamasi")]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("((SUM([s].[Tutar])) / NULLIF((COUNT([s].[SiparisNo])), 0)) AS [SepetOrtalamasi]", s);
        Assert.Contains("GROUP BY [m].[Sehir]", s);
        Assert.DoesNotContain("GROUP BY [m].[Sehir], (", s);
    }

    [Fact]
    public void siralama_cikis_adiyla_hesaplanan_kolonu_da_siralayabilir()
    {
        // Y2: eskiden sıralama yalnız SEÇİLİ ALAN üzerindeydi; artık her çıkış kolonu sıralanabilir.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null,
            [new SortItem(Guid.Empty, "", ToplamaTuru.Yok, Azalan: true, CikisAdi: "Kdv")], null,
            HesaplananAlanlar: [new ComputedColumn("{Siparisler.Tutar} * 0.20", "Kdv")]);

        Assert.Contains("ORDER BY [Kdv] DESC", Uretici.Generate(model, Harita()).Sql);
    }

    [Fact]
    public void cikista_olmayan_ada_gore_siralama_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null,
            [new SortItem(Guid.Empty, "", ToplamaTuru.Yok, false, CikisAdi: "Yokolan")], null);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("'Yokolan' kolonu çıkışta yok", hata.Message);
    }

    [Fact]
    public void formul_kosulunda_gecersiz_ifade_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            IfadeKosullari: [new ExpressionCondition("(SELECT 1)", KarsilastirmaOp.Buyuk, [1])]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("Formül koşulu", hata.Message);
        Assert.Contains("SELECT", hata.Message);
    }

    [Fact]
    public void pencere_kolonu_cikis_kolonlarina_atif_yapip_over_uretir()
    {
        // Y6: "her şehirde ciroya göre sıra". Bölüm/sıra/değer, çıkış kolonlarına ([Ad]) atıf
        // yapar → gruplanmış sorguda SUM(...) ifadesi OVER içine olduğu gibi yerleşir.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            PencereKolonlari:
            [new WindowColumn(PencereIslevi.Sira, Bolum: ["[Sehir]"],
                Sira: [new WindowSort("[Ciro]", Azalan: true)], TakmaAd: "CiroSirasi")]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("RANK() OVER (PARTITION BY ([m].[Sehir]) ORDER BY (SUM([s].[Tutar])) DESC) AS [CiroSirasi]", s);
        Assert.DoesNotContain("GROUP BY [m].[Sehir], RANK", s); // pencere ASLA GROUP BY'a girmez
    }

    [Fact]
    public void onceki_donem_ve_kosan_toplam_dogru_over_bicimi_uretir()
    {
        // Genel ihtiyaç: "geçen aya göre" ve "yılbaşından bugüne kümülatif".
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "Tarih", Kova: TarihKovasi.Ay, TakmaAd: "Ay"),
             new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            PencereKolonlari:
            [new WindowColumn(PencereIslevi.Onceki, Deger: "[Ciro]", Sira: [new WindowSort("[Ay]")], TakmaAd: "GecenAy"),
             new WindowColumn(PencereIslevi.KosanToplam, Deger: "[Ciro]", Sira: [new WindowSort("[Ay]")], TakmaAd: "Kumulatif")]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("LAG((SUM([s].[Tutar])), 1) OVER (ORDER BY (DATEADD(month, DATEDIFF(month, 0, [s].[Tarih]), 0)) ASC) AS [GecenAy]", s);
        Assert.Contains("SUM((SUM([s].[Tutar]))) OVER (ORDER BY (DATEADD(month, DATEDIFF(month, 0, [s].[Tarih]), 0)) ASC ROWS UNBOUNDED PRECEDING) AS [Kumulatif]", s);
    }

    [Fact]
    public void bolum_toplami_siralanmaz_ve_hesaplanan_kolon_ona_atif_yapabilir()
    {
        // Bölüm toplamına SIRA eklenirse SQL Server onu KÜMÜLATİF toplama çevirir ve "paya oran"
        // sessizce yanlış çıkar → sıra bilerek yok sayılır. Hesaplanan kolon (Y1) pencere
        // sonucuna atıf yapabilmeli (pencere, hesaptan ÖNCE üretilir).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            PencereKolonlari:
            [new WindowColumn(PencereIslevi.BolumToplami, Deger: "[Ciro]",
                Sira: [new WindowSort("[Ciro]")], TakmaAd: "GenelToplam")],
            HesaplananAlanlar: [new ComputedColumn("[Ciro] * 100.0 / NULLIF([GenelToplam], 0)", "Pay")]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("SUM((SUM([s].[Tutar]))) OVER () AS [GenelToplam]", s);
        Assert.DoesNotContain("OVER (ORDER BY (SUM([s].[Tutar])) ASC) AS [GenelToplam]", s);
        Assert.Contains("AS [Pay]", s);
        Assert.Contains("(SUM((SUM([s].[Tutar]))) OVER ())", s); // hesap, pencere İFADESİNİ içerir
    }

    [Fact]
    public void hareketli_ortalama_ve_dilim_bicimleri()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "Tarih", TakmaAd: "Gun"), new SelectedField(TS, "Tutar", TakmaAd: "Tutar")], null, [], null,
            PencereKolonlari:
            [new WindowColumn(PencereIslevi.HareketliOrtalama, Deger: "[Tutar]", Sira: [new WindowSort("[Gun]")], Sayi: 3, TakmaAd: "Ort3"),
             new WindowColumn(PencereIslevi.Dilim, Sira: [new WindowSort("[Tutar]", Azalan: true)], Sayi: 4, TakmaAd: "Ceyreklik")]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("AVG(([s].[Tutar])) OVER (ORDER BY ([s].[Tarih]) ASC ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) AS [Ort3]", s);
        Assert.Contains("NTILE(4) OVER (ORDER BY ([s].[Tutar]) DESC) AS [Ceyreklik]", s);
    }

    [Fact]
    public void sirasiz_pencere_islevi_anlasilir_hata_verir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            PencereKolonlari: [new WindowColumn(PencereIslevi.SiraNo, TakmaAd: "No")]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("SIRA seçin", hata.Message);
    }

    [Fact]
    public void degersiz_pencere_islevi_anlasilir_hata_verir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            PencereKolonlari: [new WindowColumn(PencereIslevi.Onceki, Sira: [new WindowSort("[Sehir]")], TakmaAd: "X")]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("DEĞER", hata.Message);
    }

    [Fact]
    public void grup_ici_ilk_n_sorguyu_sarmalar_ve_yardimci_kolonu_gizler()
    {
        // Y7: "her şehirde en çok satan 3 ürün". Pencereye WHERE'de atıf yapılamaz → sarmalama.
        // Yardımcı sıra kolonu DIŞ SELECT'te yer almaz (kullanıcının sonucunu kirletmez).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TU, "Kategori"),
             new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            GrupIciIlkN: new TopNPerGroup([new WindowSort("[Ciro]", Azalan: true)], 3, Bolum: ["[Sehir]"]));

        var s = Uretici.Generate(model, Harita());

        Assert.StartsWith("SELECT [Sehir], [Kategori], [Ciro]\nFROM (\n", D(s.Sql));
        Assert.Contains("ROW_NUMBER() OVER (PARTITION BY ([m].[Sehir]) ORDER BY (SUM([s].[Tutar])) DESC) AS [_ilkNSira]", s.Sql);
        Assert.Contains(") AS [_arakume]\nWHERE [_ilkNSira] <= @ilkN", D(s.Sql));
        Assert.Equal(3, s.Parametreler["@ilkN"]);
        Assert.DoesNotContain("SELECT [Sehir], [Kategori], [Ciro], [_ilkNSira]", s.Sql);
    }

    [Fact]
    public void grup_ici_ilk_n_de_limit_ve_siralama_dis_katmana_tasinir()
    {
        // İç katmanda TOP, bölüm kırpmasından ÖNCE satır keserdi (yanlış sonuç); alt sorguda
        // TOP'suz ORDER BY da geçersizdir → ikisi de dışarı taşınır.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")],
            null, [new SortItem(TM, "Sehir")], 100,
            GrupIciIlkN: new TopNPerGroup([new WindowSort("[Ciro]", Azalan: true)], 2));

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.StartsWith("SELECT TOP (@satir) [Sehir], [Ciro]\nFROM (\n", sql);
        Assert.DoesNotContain("SELECT TOP (@satir) [m].[Sehir]", sql);   // iç katmanda TOP yok
        Assert.EndsWith("ORDER BY [Sehir] ASC", sql);                     // sıralama dışarıda
        Assert.Equal(2, sql.Split("ORDER BY [Sehir] ASC").Length);        // yalnız bir kez (içeride yok)
    }

    [Fact]
    public void grup_ici_ilk_n_esitleri_de_alabilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            GrupIciIlkN: new TopNPerGroup([new WindowSort("[Ciro]", Azalan: true)], 3, EsitleriDeAl: true));

        Assert.Contains("RANK() OVER (ORDER BY (SUM([s].[Tutar])) DESC) AS [_ilkNSira]",
            Uretici.Generate(model, Harita()).Sql);
    }

    [Theory]
    [InlineData(0, "0'dan büyük")]
    [InlineData(3, "SIRA seçin")]
    public void grup_ici_ilk_n_eksik_bilgide_anlasilir_hata_verir(int adet, string mesaj)
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            GrupIciIlkN: new TopNPerGroup(adet == 0 ? [new WindowSort("[Sehir]")] : [], adet));

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains(mesaj, hata.Message);
    }

    [Fact]
    public void pencere_kolonuna_gore_kosul_ara_kumeye_tasinir()
    {
        // Y8: "ortalamanın üstünde satan şehirler". Pencere işlevi SQL'de ne WHERE'de ne HAVING'de
        // kullanılabilir → sorgu ara kümeye sarılır ve koşul DIŞ katmanda ADLARLA uygulanır.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            PencereKolonlari: [new WindowColumn(PencereIslevi.BolumToplami, Deger: "[Ciro]", TakmaAd: "GenelToplam")],
            HesaplananAlanlar: [new ComputedColumn("[GenelToplam] / 81.0", "OrtalamaCiro")],
            IfadeKosullari: [new ExpressionCondition("[Ciro] - [OrtalamaCiro]", KarsilastirmaOp.Buyuk, [0])]);

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.Contains("FROM (\n", sql);
        Assert.Contains(") AS [_arakume]\nWHERE (([Ciro] - [OrtalamaCiro]) > @p0)", sql);
        Assert.StartsWith("SELECT [Sehir], [Ciro], [GenelToplam], [OrtalamaCiro]\n", sql);
        Assert.DoesNotContain("HAVING", sql); // pencereli koşul HAVING'e SIZMAMALI
    }

    [Fact]
    public void ara_kume_ozeti_ozetin_ozetini_uretir()
    {
        // Y8: "her müşterinin toplamını bul, sonra bunların ORTALAMASINI al" — tek katta
        // yazılamaz (SQL'de toplama içinde toplama yasak). İç kat müşteri kırılımı, dış kat ortalama.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TM, "Ad", TakmaAd: "Musteri"),
             new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "MusteriToplami")],
            null, [new SortItem(Guid.Empty, "", CikisAdi: "Sehir")], null,
            AraKumeGruplari: ["Sehir"],
            AraKumeOzeti: [new OuterAggregate(ToplamaTuru.Ortalama, "MusteriToplami", "MusteriBasiOrtalama"),
                           new OuterAggregate(ToplamaTuru.Say, "Musteri", "MusteriSayisi")]);

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.StartsWith("SELECT [Sehir], AVG([MusteriToplami]) AS [MusteriBasiOrtalama], COUNT([Musteri]) AS [MusteriSayisi]\nFROM (\n", sql);
        Assert.Contains("GROUP BY [m].[Sehir], [m].[Ad]", sql);      // iç kat: müşteri kırılımı
        Assert.EndsWith(") AS [_arakume]\nGROUP BY [Sehir]\nORDER BY [Sehir] ASC", sql); // dış kat
    }

    [Fact]
    public void ara_kume_disinda_kalan_kolonla_siralama_anlasilir_hata_verir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TM, "Ad", TakmaAd: "Musteri"),
             new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "MusteriToplami")],
            null, [new SortItem(Guid.Empty, "", CikisAdi: "Musteri")], null,
            AraKumeGruplari: ["Sehir"],
            AraKumeOzeti: [new OuterAggregate(ToplamaTuru.Ortalama, "MusteriToplami")]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("ara kümenin dışında kaldığı için sıralamada kullanılamaz", hata.Message);
    }

    [Fact]
    public void ara_kume_kosulunda_tablo_kolonuna_atif_reddedilir()
    {
        // Dış katmanda iç tabloların kolonları görünmez — sessiz "invalid column" yerine yol gösterir.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            PencereKolonlari: [new WindowColumn(PencereIslevi.BolumToplami, Deger: "[Ciro]", TakmaAd: "Genel")],
            IfadeKosullari: [new ExpressionCondition("[Genel] - {Siparisler.Tutar}", KarsilastirmaOp.Buyuk, [0])]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("ARA KÜME üzerinde", hata.Message);
    }

    [Fact]
    public void capraz_tablo_pivot_uretir_ve_satir_ekseni_kalan_kolonlardir()
    {
        // Y9: "şehir satır, kategori sütun, ciro hücre". Çapraza girmeyen kolonlar satır eksenidir.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TU, "Kategori"),
             new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")],
            null, [new SortItem(Guid.Empty, "", CikisAdi: "Sehir")], null,
            Capraz: new PivotSpec("Kategori", "Ciro", ["Beyaz Esya", "Mobilya"]));

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.StartsWith("SELECT [Sehir], [Beyaz Esya], [Mobilya]\nFROM (\n", sql);
        Assert.Contains(") AS [_kaynak]\nPIVOT (SUM([Ciro]) FOR [Kategori] IN ([Beyaz Esya], [Mobilya])) AS [_capraz]", sql);
        Assert.EndsWith("ORDER BY [Sehir] ASC", sql);
    }

    [Fact]
    public void capraz_tabloda_eksik_bilgi_ve_uyumsuz_birlesim_reddedilir()
    {
        var haritaM = Harita();
        var temel = new[] { new SelectedField(TM, "Sehir"), new SelectedField(TU, "Kategori"),
                            new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro") };

        var basliksiz = new QueryModel(Guid.NewGuid(), temel, null, [], null,
            Capraz: new PivotSpec("Kategori", "Ciro", []));
        Assert.Contains("sütuna dönecek DEĞERLERİ girin",
            Assert.Throws<QueryModelException>(() => Uretici.Generate(basliksiz, haritaM)).Message);

        var ilkNIle = new QueryModel(Guid.NewGuid(), temel, null, [], null,
            Capraz: new PivotSpec("Kategori", "Ciro", ["A"]),
            GrupIciIlkN: new TopNPerGroup([new WindowSort("[Ciro]")], 2));
        Assert.Contains("birlikte kullanılamaz",
            Assert.Throws<QueryModelException>(() => Uretici.Generate(ilkNIle, haritaM)).Message);
    }

    [Fact]
    public void ara_toplam_genel_toplam_satiri_ekler_ve_etiketler()
    {
        // Y10: "(Tümü)" etiketi GERÇEK boş değerle karışmasın diye GROUPING() bayrağıyla yazılır.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            AraToplam: new SubtotalSpec());

        var s = Uretici.Generate(model, Harita());
        var sql = D(s.Sql);

        Assert.Contains("GROUPING([m].[Sehir]) AS [_araToplam1]", sql);
        Assert.Contains("GROUP BY GROUPING SETS (([m].[Sehir]), ())", sql);
        Assert.Contains("CASE WHEN [_araToplam1] = 1 THEN @p0 ELSE CAST([Sehir] AS nvarchar(4000)) END AS [Sehir]", sql);
        Assert.Equal("(Tümü)", s.Parametreler["@p0"]);
        Assert.Contains("[Ciro]", sql);
    }

    [Fact]
    public void kademeli_ara_toplam_rollup_uretir_ve_etiket_kapatilabilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TU, "Kategori"),
             new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            AraToplam: new SubtotalSpec(AraToplamTuru.Kademeli, Etiketle: false));

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.Contains("GROUP BY ROLLUP([m].[Sehir], [u].[Kategori])", sql);
        Assert.DoesNotContain("GROUPING(", sql);   // etiket kapalı → bayrak da yok
        Assert.DoesNotContain("_arakume", sql);     // sarmalamaya gerek kalmaz
    }

    [Fact]
    public void kirilimsiz_ara_toplam_anlasilir_hata_verir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")], null, [], null,
            AraToplam: new SubtotalSpec());

        Assert.Contains("en az bir kırılım gerekir",
            Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita())).Message);
    }

    [Fact]
    public void varlik_kosulu_exists_uretir_ve_satir_cogaltmaz()
    {
        // Y11: "siparişi olan müşteriler". Join ile yapılsaydı çoklu sipariş müşteri satırını
        // ÇOĞALTIRDI; EXISTS ne satır çoğaltır ne kolon ekler. Bağ kolonları haritadan türetilir.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Ad")], null, [], null,
            VarlikKosullari: [new ExistenceCondition(TS)]);

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.Contains("WHERE EXISTS (SELECT 1 FROM [dbo].[Siparisler] AS [_v1] WHERE [_v1].[MusteriId] = [m].[MusteriNo])", sql);
        Assert.DoesNotContain("JOIN [dbo].[Siparisler]", sql); // ana sorguya join'lenmez
    }

    [Fact]
    public void yok_varlik_kosulu_not_exists_ve_ic_filtre_uretir()
    {
        // "Hiç iptal edilmemiş siparişi OLMAYAN müşteriler" — join'le sorulamayan soru.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Ad")], null, [], null,
            VarlikKosullari:
            [new ExistenceCondition(TS, Yok: true,
                Filtre: new FilterCondition(TS, "Tutar", KarsilastirmaOp.Buyuk, ["100"]))]);

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.Contains("NOT EXISTS (SELECT 1 FROM [dbo].[Siparisler] AS [_v1] WHERE [_v1].[MusteriId] = [m].[MusteriNo] AND ([_v1].[Tutar] > @p0))", sql);
    }

    [Fact]
    public void varlik_kosulu_sorguda_olmayan_tabloya_baglanamaz()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Ad")], null, [], null,
            VarlikKosullari: [new ExistenceCondition(TS, Baglar: [new ExistenceLink("UrunId", TU, "UrunNo")])]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("o tablo sorguda yok", hata.Message);
    }

    [Fact]
    public void tekille_distinct_uretir()
    {
        // Y12: join yüzünden çoğalmış satır listesinde tekilleştirme.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null, Tekille: true);

        Assert.StartsWith("SELECT DISTINCT [m].[Sehir] AS [Sehir]", D(Uretici.Generate(model, Harita()).Sql));
    }

    [Fact]
    public void sayfalama_offset_fetch_uretir_ve_top_ile_karismaz()
    {
        // Y16: OFFSET/FETCH ile TOP aynı sorguda kullanılamaz → limit SAYFA BOYU olur.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [new SortItem(TM, "Sehir")], 50, Atla: 100);

        var s = Uretici.Generate(model, Harita());

        Assert.DoesNotContain("TOP (", s.Sql);
        Assert.EndsWith("ORDER BY [Sehir] ASC\nOFFSET (@atla) ROWS FETCH NEXT (@satir) ROWS ONLY", D(s.Sql));
        Assert.Equal(100, s.Parametreler["@atla"]);
        Assert.Equal(50, s.Parametreler["@satir"]);
    }

    [Fact]
    public void sirasiz_sayfalama_anlasilir_hata_verir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], 50, Atla: 100);

        Assert.Contains("Sayfalama için bir SIRALAMA gerekir",
            Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita())).Message);
    }

    [Fact]
    public void join_on_una_sabit_kosul_eklenebilir_ve_where_e_kaymaz()
    {
        // Y13: LEFT join'de ON'daki filtre EŞLEŞMEYİ daraltır (ana satır yine gelir);
        // WHERE'e yazılsaydı ana satır ELENİRDİ — ikisi farklı sorudur.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Ciro")],
            null, [], null,
            JoinAyarlari: [new JoinSetting(TS, TM, JoinType.Left, null,
                EkKosullar: [new FilterCondition(TS, "Tutar", KarsilastirmaOp.Buyuk, ["100"])])]);

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.Contains("LEFT JOIN [dbo].[Siparisler] AS [s] ON [s].[MusteriId] = [m].[MusteriNo] AND [s].[Tutar] > @p0", sql);
        Assert.DoesNotContain("WHERE", sql);
    }

    [Fact]
    public void ayni_tablo_ikinci_kez_ornek_olarak_kullanilabilir()
    {
        // Y14: "getiren" ve "danışman" aynı tablodan gelir ama AYRI satırlardır. Örnek, haritaya
        // sanal olarak eklenir; kaynak tablonun ilişkileri kopyalandığı için join kendiliğinden kurulur.
        var ikinciMusteri = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Ad", TakmaAd: "Musteri"),
             new SelectedField(ikinciMusteri, "Ad", TakmaAd: "IkinciMusteri"),
             new SelectedField(TS, "SiparisNo")],
            null, [], null,
            KokTabloId: TS,
            TabloOrnekleri: [new TableInstance(ikinciMusteri, TM, "Ikinci Musteri")]);

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        // Aynı tablo İKİ FARKLI takma adla FROM/JOIN'e girer.
        Assert.Contains("JOIN [dbo].[Musteriler] AS [m] ", sql);
        Assert.Contains("JOIN [dbo].[Musteriler] AS [m1] ", sql);
        Assert.Contains("[m].[Ad] AS [Musteri]", sql);
        Assert.Contains("[m1].[Ad] AS [IkinciMusteri]", sql);
    }

    [Fact]
    public void birlesim_union_all_uretir_ve_siralama_butune_uygulanir()
    {
        // Y15: iki sorgunun sonucu alt alta. Sıralama/limit SQL gereği yalnız BÜTÜNE uygulanabilir.
        var h = Guid.NewGuid();
        var ikinci = new QueryModel(h,
            [new SelectedField(TU, "Kategori", TakmaAd: "Ad")], null, [], null);
        var model = new QueryModel(h,
            [new SelectedField(TM, "Sehir", TakmaAd: "Ad")],
            null, [new SortItem(Guid.Empty, "", CikisAdi: "Ad")], 10,
            Birlesim: [ikinci]);

        var s = Uretici.Generate(model, Harita());
        var sql = D(s.Sql);

        Assert.StartsWith("SELECT TOP (@satir) [Ad]\nFROM (\n", sql);
        Assert.Contains("\nUNION ALL\n", sql);
        Assert.Contains("[m].[Sehir] AS [Ad]", sql);
        Assert.Contains("[u].[Kategori] AS [Ad]", sql);
        Assert.EndsWith(") AS [_birlesim]\nORDER BY [Ad] ASC", sql);
    }

    [Fact]
    public void birlesimde_kolon_sayisi_uyusmazsa_anlasilir_hata_verir()
    {
        var h = Guid.NewGuid();
        var ikinci = new QueryModel(h,
            [new SelectedField(TU, "Kategori"), new SelectedField(TU, "UrunNo")], null, [], null);
        var model = new QueryModel(h, [new SelectedField(TM, "Sehir")], null, [], null, Birlesim: [ikinci]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("kolon SAYISI aynı olmalı", hata.Message);
    }

    [Fact]
    public void birlesimde_tekillestirme_union_yapar()
    {
        var h = Guid.NewGuid();
        var ikinci = new QueryModel(h, [new SelectedField(TU, "Kategori", TakmaAd: "Ad")], null, [], null);
        var model = new QueryModel(h, [new SelectedField(TM, "Sehir", TakmaAd: "Ad")], null, [], null,
            Birlesim: [ikinci], BirlesimTekille: true);

        var sql = D(Uretici.Generate(model, Harita()).Sql);

        Assert.Contains("\nUNION\n", sql);
        Assert.DoesNotContain("UNION ALL", sql);
    }

    // ---- RLS (satır-düzeyi güvenlik): kural KULLANICIDAN gelir, modelden DEĞİL ----

    [Fact]
    public void rls_kurali_where_e_ve_ile_eklenir()
    {
        var model = new QueryModel(Guid.NewGuid(), [new SelectedField(TM, "Ad")], null, [], null);

        var s = Uretici.Generate(model, Harita(), null,
            [new RowFilter(TM, "Sehir", KarsilastirmaOp.Esit, ["Ankara"], "Bayi kapsamı")]);

        Assert.Contains("WHERE [m].[Sehir] = @p0", D(s.Sql));
        Assert.Equal("Ankara", s.Parametreler["@p0"]);
    }

    [Fact]
    public void rls_kurali_kullanicinin_veyali_filtresiyle_etkisiz_birakilamaz()
    {
        // KAÇIŞ DENEMESİ: kullanıcı "A VEYA B" kurarsa ve kural düz AND ile eklenseydi,
        // "A OR B AND kural" olur; VEYA'nın sol dalı kuralı ATLARDI. Kullanıcı filtresinin
        // TAMAMI paranteze alınmalı.
        var kullaniciFiltresi = new FilterGroup(MantikBaglaci.Veya, [
            new FilterCondition(TM, "Sehir", KarsilastirmaOp.Esit, ["Ankara"]),
            new FilterCondition(TM, "Sehir", KarsilastirmaOp.EsitDegil, ["Ankara"]), // her satırı kapsar
        ]);
        var model = new QueryModel(Guid.NewGuid(), [new SelectedField(TM, "Ad")], kullaniciFiltresi, [], null);

        var sql = D(Uretici.Generate(model, Harita(), null,
            [new RowFilter(TM, "MusteriNo", KarsilastirmaOp.Esit, ["7"])]).Sql).Replace("\n", " ");

        Assert.Matches(@"WHERE \(.*OR.*\) AND \[m\]\.\[MusteriNo\] = @p\d", sql);
    }

    [Fact]
    public void rls_kurali_birlesim_dallarina_da_uygulanir()
    {
        // KAÇIŞ DENEMESİ: UNION'la ikinci bir sorgu ekleyip kuralsız veri çekmek.
        var h = Guid.NewGuid();
        var ikinci = new QueryModel(h, [new SelectedField(TM, "Ad", TakmaAd: "Ad")], null, [], null);
        var model = new QueryModel(h, [new SelectedField(TM, "Ad", TakmaAd: "Ad")], null, [], null,
            Birlesim: [ikinci]);

        var sql = D(Uretici.Generate(model, Harita(), null,
            [new RowFilter(TM, "Sehir", KarsilastirmaOp.Esit, ["Ankara"])]).Sql);

        // Her iki dal da kuralı almalı (parametre adları dal önekiyle ayrılır: @pb10 / @pb20).
        Assert.Contains("WHERE [m].[Sehir] = @pb10", sql);
        Assert.Contains("WHERE [m].[Sehir] = @pb20", sql);
    }

    [Fact]
    public void rls_kurali_sorguda_olmayan_tablo_icin_atlanir()
    {
        // Kuralın tablosu sorguda yoksa o tablodan zaten satır dönmez — sorguyu kırma, atla.
        var model = new QueryModel(Guid.NewGuid(), [new SelectedField(TM, "Ad")], null, [], null);

        var sql = D(Uretici.Generate(model, Harita(), null,
            [new RowFilter(TU, "Kategori", KarsilastirmaOp.Esit, ["Beyaz"])]).Sql);

        Assert.DoesNotContain("WHERE", sql);
        Assert.DoesNotContain("Urunler", sql);
    }

    [Fact]
    public void rls_kurali_haritada_olmayan_kolonu_kabul_etmez()
    {
        // Yanlış yapılandırılmış kural sessizce yok sayılmaz — açıkça patlar (fail-closed).
        var model = new QueryModel(Guid.NewGuid(), [new SelectedField(TM, "Ad")], null, [], null);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita(), null,
            [new RowFilter(TM, "OlmayanKolon", KarsilastirmaOp.Esit, ["x"])]));
        Assert.Contains("Kolon haritada yok", hata.Message);
    }

    [Fact]
    public void ozet_kosulu_having_uretir_ve_filtreden_ayri_kalir()
    {
        // Y3: "3'ten çok siparişi olan şehirler" — sayım GRUP üstünde (HAVING), tutar filtresi
        // satır bazında (WHERE). İkisi aynı sorguda ve BİRBİRİNE KARIŞMAMALI.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "SiparisNo", ToplamaTuru.Say, "Adet")],
            new FilterCondition(TS, "Tutar", KarsilastirmaOp.Buyuk, [100L]),
            [], null,
            OzetKosullar: [new SummaryCondition(ToplamaTuru.Say, TS, "SiparisNo", KarsilastirmaOp.BuyukEsit, [3L])]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("WHERE [s].[Tutar] > @p0", s);
        Assert.Contains("GROUP BY [m].[Sehir]", s);
        Assert.Contains("HAVING COUNT([s].[SiparisNo]) >= @p1", s);
        // HAVING, GROUP BY'dan SONRA gelmeli (SQL sırası).
        Assert.True(s.IndexOf("HAVING", StringComparison.Ordinal) > s.IndexOf("GROUP BY", StringComparison.Ordinal));
    }

    [Fact]
    public void ozet_kosulu_benzersiz_sayimda_degeri_sayiya_cevirir_ve_baglaclanir()
    {
        // COUNT(DISTINCT metin kolonu) >= 3: karşılaştırma değeri kolonun tipine (nvarchar) DEĞİL
        // sayıya çevrilmeli. İkinci koşul VEYA ile bağlanır (soldan sağa).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            Gruplamalar: [new GroupingItem(TM, "Sehir")],
            OzetKosullar:
            [
                new SummaryCondition(ToplamaTuru.BenzersizSay, TS, "SiparisNo", KarsilastirmaOp.BuyukEsit, [3L]),
                new SummaryCondition(ToplamaTuru.Topla, TS, "Tutar", KarsilastirmaOp.Buyuk, [1000L], MantikBaglaci.Veya),
            ]);

        var sonuc = Uretici.Generate(model, Harita());

        Assert.Contains("HAVING (COUNT(DISTINCT [s].[SiparisNo]) >= @p0) OR SUM([s].[Tutar]) > @p1", sonuc.Sql);
        Assert.Equal(3L, sonuc.Parametreler["@p0"]);      // sayıya çevrildi (nvarchar'a değil)
        Assert.Equal(1000m, sonuc.Parametreler["@p1"]);   // money kolonu → decimal
    }

    [Fact]
    public void ozet_kosulu_toplamasiz_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            OzetKosullar: [new SummaryCondition(ToplamaTuru.Yok, TS, "Tutar", KarsilastirmaOp.Buyuk, [1L])]);

        var hata = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("filtreye", hata.Message); // "satır bazlı koşullar filtreye yazılır"
    }

    [Fact]
    public void ozet_alan_turetilmis_tablo_left_join_uretir()
    {
        // Müşteri şehri + (müşteri başına, tutarı 100'ü aşan sipariş sayısı). Özet tablosu ana
        // sorguya JOIN'lenmez (fan-trap yok); türetilmiş tabloda önce gruplanır sonra bağlanır.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            OzetAlanlar: [new SummaryField(TS, "SiparisNo", ToplamaTuru.Say, TM, "SiparisSayisi",
                new FilterCondition(TS, "Tutar", KarsilastirmaOp.Buyuk, [100L]))]); // JSON sayıları long gelir

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("ISNULL([oz1].[deger], 0) AS [SiparisSayisi]", result.Sql);
        Assert.Contains(
            "LEFT JOIN (SELECT [MusteriId] AS [bag], COUNT([SiparisNo]) AS [deger] FROM [dbo].[Siparisler]" +
            " WHERE [Tutar] > @p0 GROUP BY [MusteriId]) AS [oz1] ON [oz1].[bag] = [m].[MusteriNo]", result.Sql);
        Assert.Equal(100m, result.Parametreler["@p0"]); // money → DECIMAL çevrimi (Y2: para double bağlanmaz)
        Assert.DoesNotContain("JOIN [dbo].[Siparisler] AS", result.Sql); // ana sorguya joinlenmedi
        Assert.DoesNotContain("GROUP BY [m]", result.Sql);               // ana sorguda aggregate yok
    }

    [Fact]
    public void ayni_tabloya_dusen_ozetler_tek_turetilmis_tabloda_birlesir()
    {
        // Kullanıcı isteği (15 Ağu 2026): "SUM + COUNT" gibi iki ölçü kaynağı İKİ KEZ taratmasın;
        // elle yazılan SQL ile aynı şekle gelsin:
        //   LEFT JOIN (SELECT MusteriId, SUM(Tutar), COUNT_BIG(*) ... GROUP BY MusteriId) o ON ...
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            OzetAlanlar: [
                new SummaryField(TS, "Tutar", ToplamaTuru.Topla, TM, "OdenenToplam"),
                new SummaryField(TS, "SiparisNo", ToplamaTuru.Say, TM, "SiparisSayisi"),
            ]);

        var result = Uretici.Generate(model, Harita());

        // TEK türetilmiş tablo, İKİ ölçü kolonu.
        Assert.Contains(
            "LEFT JOIN (SELECT [MusteriId] AS [bag], SUM([Tutar]) AS [deger1], COUNT([SiparisNo]) AS [deger2]" +
            " FROM [dbo].[Siparisler] GROUP BY [MusteriId]) AS [oz1] ON [oz1].[bag] = [m].[MusteriNo]", result.Sql);
        Assert.Contains("ISNULL([oz1].[deger1], 0) AS [OdenenToplam]", result.Sql);
        Assert.Contains("ISNULL([oz1].[deger2], 0) AS [SiparisSayisi]", result.Sql);
        Assert.DoesNotContain("[oz2]", result.Sql); // ikinci tarama YOK
    }

    [Fact]
    public void farkli_filtreli_ozetler_ayri_turetilmis_tabloda_kalir()
    {
        // Filtreler farklıysa birleştirme YANLIŞ olurdu (biri diğerinin filtresini alırdı).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            OzetAlanlar: [
                new SummaryField(TS, "SiparisNo", ToplamaTuru.Say, TM, "BuyukSiparis",
                    new FilterCondition(TS, "Tutar", KarsilastirmaOp.Buyuk, [100L])),
                new SummaryField(TS, "SiparisNo", ToplamaTuru.Say, TM, "TumSiparis"),
            ]);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("[oz1]", result.Sql);
        Assert.Contains("[oz2]", result.Sql);
        Assert.Contains("ISNULL([oz1].[deger], 0) AS [BuyukSiparis]", result.Sql);
        Assert.Contains("ISNULL([oz2].[deger], 0) AS [TumSiparis]", result.Sql);
    }

    [Fact]
    public void ozet_alan_aggregateli_sorguda_group_by_a_girer()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Toplam")],
            null, [], null,
            OzetAlanlar: [new SummaryField(TS, "SiparisNo", ToplamaTuru.Say, TM, "SiparisSayisi")]);

        var result = Uretici.Generate(model, Harita());

        // Türetilmiş tablo bağ-kolon başına tek satır döndürür → GROUP BY'a girmesi sonucu değiştirmez.
        Assert.Contains("GROUP BY [m].[Sehir], ISNULL([oz1].[deger], 0)", result.Sql);
    }

    [Fact]
    public void ozet_alan_iliskisiz_bag_tablosu_reddedilir()
    {
        // Urunler ile Musteriler arasında haritada doğrudan ilişki yok.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            OzetAlanlar: [new SummaryField(TU, "UrunNo", ToplamaTuru.Say, TM)]);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void ozet_filtresi_yabanci_tablo_kolonunu_reddeder()
    {
        // Özet filtresi yalnız özet tablosunun kolonlarını kullanabilir (kapsam sızıntısı yok).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            OzetAlanlar: [new SummaryField(TS, "SiparisNo", ToplamaTuru.Say, TM, null,
                new FilterCondition(TM, "Sehir", KarsilastirmaOp.Esit, ["Ankara"]))]);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void ozet_alan_toplamasiz_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            OzetAlanlar: [new SummaryField(TS, "SiparisNo", ToplamaTuru.Yok, TM)]);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void bilesik_kolon_concat_uretir_sabitler_parametre_olur()
    {
        // Ad + ' ' + Soyad benzeri: kolonlar CONCAT'te, ayraç metni PARAMETRE (SQL'e gömülmez).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "MusteriNo")], null, [], null,
            BilesikAlanlar: [new CompositeField(
                [new CompositePart(TM, "Ad"), new CompositePart(Sabit: " "), new CompositePart(TM, "Sehir")],
                "AdSehir")]);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("CONCAT([m].[Ad], @p0, [m].[Sehir]) AS [AdSehir]", result.Sql);
        Assert.Equal(" ", result.Parametreler["@p0"]);
        Assert.DoesNotContain("GROUP BY", result.Sql); // aggregate yok
    }

    [Fact]
    public void bilesik_kolon_aggregateli_sorguda_group_by_a_girer()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "Toplam")], null, [], null,
            BilesikAlanlar: [new CompositeField(
                [new CompositePart(TM, "Ad"), new CompositePart(Sabit: " - "), new CompositePart(TM, "Sehir")],
                "Kisi")]);

        var result = Uretici.Generate(model, Harita());

        // Birleşik alanın tablosu (Musteriler) otomatik join'lendi + ifade GROUP BY'da.
        Assert.Contains("JOIN [dbo].[Musteriler]", result.Sql);
        Assert.Contains("GROUP BY CONCAT([m].[Ad], @p0, [m].[Sehir])", result.Sql);
    }

    [Fact]
    public void bilesik_kolon_kolonsuz_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            BilesikAlanlar: [new CompositeField([new CompositePart(Sabit: "x")], "S")]);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void elle_kosul_yer_tutucu_cozulur_ve_filtrelerle_and_lenir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")],
            new FilterCondition(TM, "Sehir", KarsilastirmaOp.EsitDegil, ["X"]),
            [], null,
            ElleKosul: "{Musteriler.MusteriNo} IN (SELECT MusteriId FROM dbo.Siparisler WHERE Tutar > 500)");

        var result = Uretici.Generate(model, Harita());

        Assert.Contains(
            "WHERE [m].[Sehir] <> @p0 AND ([m].[MusteriNo] IN (SELECT MusteriId FROM dbo.Siparisler WHERE Tutar > 500))",
            result.Sql);
    }

    [Fact]
    public void elle_kosul_veya_baglaciyla_birlesir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")],
            new FilterCondition(TM, "Sehir", KarsilastirmaOp.Esit, ["Ankara"]),
            [], null,
            ElleKosul: "{Musteriler.Ad} LIKE 'A%'",
            ElleKosulBaglaci: MantikBaglaci.Veya);

        var result = Uretici.Generate(model, Harita());

        // VEYA'da öncelik için iki taraf da parantezlenir.
        Assert.Contains("WHERE ([m].[Sehir] = @p0) OR ([m].[Ad] LIKE 'A%')", result.Sql);
    }

    [Fact]
    public void elle_kosul_dostca_adla_da_cozulur_ve_tabloyu_joinler()
    {
        // Yer tutucuda dostça ad da geçer; tablo sorguda yoksa otomatik join'lenir.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            ElleKosul: "{Siparisler.Tutar} IS NOT NULL");

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("JOIN [dbo].[Siparisler] AS [s]", result.Sql);
        Assert.Contains("WHERE ([s].[Tutar] IS NOT NULL)", result.Sql);
    }

    [Fact]
    public void elle_kosul_literal_icindeki_yer_tutucu_veri_olarak_korunur()
    {
        // Y3: tek-tırnaklı string literal İÇİNDEKİ {X.Y} metni değiştirilmez ('' kaçışı dahil).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            ElleKosul: "{Musteriler.Ad} <> '{Musteriler.Ad} ''ornek''' ");

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("WHERE ([m].[Ad] <> '{Musteriler.Ad} ''ornek''')", result.Sql);
    }

    [Fact]
    public void elle_kosul_ayni_dostca_adli_iki_tablo_reddedilir()
    {
        // Y3: ad birden çok tabloyla eşleşiyorsa sessiz ilk-seçim yerine açık hata.
        var a = Guid.Parse("dddddddd-0000-0000-0000-00000000000a");
        var b = Guid.Parse("dddddddd-0000-0000-0000-00000000000b");
        var dataMap = new DataMapDetail(Guid.NewGuid(), "H", Guid.NewGuid(), DataMode.Canli,
            [
                new DataMapTableDto(a, "dbo", "TabloA", "Ayni", [new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int")]),
                new DataMapTableDto(b, "dbo", "TabloB", "Ayni", [new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int")]),
            ],
            [new DataMapRelationshipDto(a, "Id", b, "Id", false)]);
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(a, "Id")], null, [], null, ElleKosul: "{Ayni.Id} = 1");
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, dataMap));
    }

    [Fact]
    public void elle_kosul_bilinmeyen_tablo_veya_kolon_reddedilir()
    {
        var h = Harita();
        Assert.Throws<QueryModelException>(() => Uretici.Generate(new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null, ElleKosul: "{Yok.Kolon} = 1"), h));
        Assert.Throws<QueryModelException>(() => Uretici.Generate(new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null, ElleKosul: "{Musteriler.Yok} = 1"), h));
        Assert.Throws<QueryModelException>(() => Uretici.Generate(new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null, ElleKosul: "{Gecersiz} = 1"), h));
    }

    [Fact]
    public void kosullu_olcu_sum_case_uretir_ve_group_by_tetikler()
    {
        // Şehir başına: tutarı 500'ü aşan siparişlerin TOPLAM tutarı (kullanıcının CASE WHEN örneği).
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            KosulluOlculer: [new ConditionalMeasure(ToplamaTuru.Topla,
                new FilterCondition(TS, "Tutar", KarsilastirmaOp.Buyuk, [500L]),
                TS, "Tutar", "BuyukSatisToplami")]);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("SUM(CASE WHEN [s].[Tutar] > @p0 THEN [s].[Tutar] ELSE 0 END) AS [BuyukSatisToplami]", result.Sql);
        Assert.Contains("JOIN [dbo].[Siparisler]", result.Sql);       // koşulun tablosu otomatik joinlendi
        Assert.Contains("GROUP BY [m].[Sehir]", result.Sql);          // koşullu ölçü aggregate sayılır
    }

    [Fact]
    public void kosullu_olcu_say_kolonsuz_else_suz_uretir()
    {
        // Kolon verilmezse değer sabit 1; Say'da ELSE yok — eşleşmeyen NULL kalır, COUNT saymaz.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            KosulluOlculer: [new ConditionalMeasure(ToplamaTuru.Say,
                new FilterCondition(TS, "Tutar", KarsilastirmaOp.Buyuk, [500L]), TakmaAd: "BuyukSatisAdedi")]);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("COUNT(CASE WHEN [s].[Tutar] > @p0 THEN 1 END) AS [BuyukSatisAdedi]", result.Sql);
    }

    [Fact]
    public void kosullu_olcu_benzersiz_say_kolonsuz_reddedilir()
    {
        // O1: COUNT(DISTINCT 1) her zaman <=1 - anlamsiz; deger kolonu zorunlu.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            KosulluOlculer: [new ConditionalMeasure(ToplamaTuru.BenzersizSay,
                new FilterCondition(TS, "Tutar", KarsilastirmaOp.Buyuk, [500L]))]);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void kosullu_olcu_toplamasiz_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir")], null, [], null,
            KosulluOlculer: [new ConditionalMeasure(ToplamaTuru.Yok,
                new FilterCondition(TM, "Sehir", KarsilastirmaOp.Esit, ["A"]))]);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void kok_tablo_secimi_from_u_degistirir()
    {
        // Varsayılan kök "ilk alanın tablosu"ydu (Musteriler); kullanıcı Siparisler'i kök seçer.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar")], null, [], null,
            KokTabloId: TS);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("FROM [dbo].[Siparisler] AS [s]", result.Sql);
        Assert.Contains("JOIN [dbo].[Musteriler] AS [m] ON [m].[MusteriNo] = [s].[MusteriId]", result.Sql);
    }

    [Fact]
    public void join_ayari_left_turu_ve_bilesik_kosul_uretir()
    {
        // Siparisler, Musteriler'e LEFT + iki koşulla (bileşik join) bağlanır.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar")], null, [], null,
            JoinAyarlari: [new JoinSetting(TS, TM, JoinType.Left,
                [new JoinCondition("MusteriId", "MusteriNo"), new JoinCondition("UrunId", "MusteriNo")])]);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains(
            "LEFT JOIN [dbo].[Siparisler] AS [s] ON [s].[MusteriId] = [m].[MusteriNo] AND [s].[UrunId] = [m].[MusteriNo]",
            result.Sql);
    }

    [Fact]
    public void join_ayari_ebeveyn_zinciri_sirayla_baglanir()
    {
        // Urunler → Siparisler → Musteriler(kök) zinciri; koşullar haritadan otomatik.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TU, "Kategori")], null, [], null,
            JoinAyarlari: [
                new JoinSetting(TU, TS),  // Urunler'in ebeveyni Siparisler
                new JoinSetting(TS, TM),  // Siparisler'in ebeveyni Musteriler
            ]);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("FROM [dbo].[Musteriler] AS [m]", result.Sql);
        Assert.Contains("JOIN [dbo].[Siparisler] AS [s] ON [s].[MusteriId] = [m].[MusteriNo]", result.Sql);
        Assert.Contains("JOIN [dbo].[Urunler] AS [u] ON [u].[UrunNo] = [s].[UrunId]", result.Sql);
        // Sıra: ebeveyn önce (Siparisler, sonra Urunler).
        Assert.True(result.Sql.IndexOf("[dbo].[Siparisler]", StringComparison.Ordinal)
                  < result.Sql.IndexOf("[dbo].[Urunler]", StringComparison.Ordinal));
    }

    [Fact]
    public void esit_derinlikte_join_sirasi_deterministiktir()
    {
        // Test borcu: aynı derinlikteki tablolar Guid sırasıyla dizilir — SQL üretimi kararlı.
        var kok = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var x = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000a");
        var y = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");
        var dataMap = new DataMapDetail(Guid.NewGuid(), "H", Guid.NewGuid(), DataMode.Canli,
            [
                new DataMapTableDto(kok, "dbo", "Kok", "Kok", [new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int")]),
                new DataMapTableDto(x, "dbo", "XT", "XT", [new DataMapColumnDto("KokId", null, ColumnRole.Anahtar, "int"), new DataMapColumnDto("A", null, ColumnRole.Boyut, "int")]),
                new DataMapTableDto(y, "dbo", "YT", "YT", [new DataMapColumnDto("KokId", null, ColumnRole.Anahtar, "int"), new DataMapColumnDto("B", null, ColumnRole.Boyut, "int")]),
            ],
            [
                // İlişki listesinde Y ÖNCE — yine de çıktı Guid sırasına (X < Y) göre olmalı.
                new DataMapRelationshipDto(y, "KokId", kok, "Id", true),
                new DataMapRelationshipDto(x, "KokId", kok, "Id", true),
            ]);
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(kok, "Id"), new SelectedField(x, "A"), new SelectedField(y, "B")], null, [], null);

        var result = Uretici.Generate(model, dataMap);

        Assert.True(result.Sql.IndexOf("[dbo].[XT]", StringComparison.Ordinal)
                  < result.Sql.IndexOf("[dbo].[YT]", StringComparison.Ordinal));
    }

    [Fact]
    public void join_ayari_donguluyse_reddedilir()
    {
        // TS↔TU birbirine ebeveyn (döngü) — kök TM'ye ulaşamaz.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar")], null, [], null,
            JoinAyarlari: [new JoinSetting(TS, TU), new JoinSetting(TU, TS)]);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void kok_tablo_icin_join_ayari_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar")], null, [], null,
            KokTabloId: TM,
            JoinAyarlari: [new JoinSetting(TM, TS)]);
        Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
    }

    [Fact]
    public void elle_gruplama_secili_olmayan_kolona_gore_gruplar_ve_tabloyu_joinler()
    {
        // Otomatik GROUP BY yalnız seçili kırılımdan olur; elle liste ile SEÇİLMEYEN bir boyuta
        // (Urunler.Kategori) göre de gruplanabilir — Urunler otomatik join'lenir.
        var model = new QueryModel(Guid.NewGuid(),
            [
                new SelectedField(TM, "Sehir"),
                new SelectedField(TS, "Tutar", ToplamaTuru.Topla, "ToplamSatis"),
            ],
            null, [], null,
            Gruplamalar: [new GroupingItem(TM, "Sehir"), new GroupingItem(TU, "Kategori")]);

        var result = Uretici.Generate(model, Harita());

        Assert.Contains("JOIN [dbo].[Urunler] AS [u]", result.Sql);           // gruplama tablosu join'lendi
        Assert.Contains("GROUP BY [m].[Sehir], [u].[Kategori]", result.Sql);  // tam elle liste
    }

    [Fact]
    public void alansiz_yalniz_formullu_model_calisir_kok_ifadeden_secilir()
    {
        // S22: katalog metrigi tek basina gecerli sorgudur ("yalniz Toplam Ciro" = tek KPI degeri;
        // Metrikler ekranindaki Olc de bu yolu kullanir). Onceden "En az bir alan secilmeli" ile
        // reddediliyordu; FROM koku, formulun ILK {Tablo.Kolon} atfinin tablosundan secilir.
        var model = new QueryModel(Guid.NewGuid(), Alanlar: [], Filtre: null, Siralama: [], SatirLimiti: null,
            HesaplananAlanlar: [new ComputedColumn("SUM({Siparisler.Tutar})", "Toplam Ciro")]);

        var s = Uretici.Generate(model, Harita()).Sql;

        Assert.Contains("(SUM([s].[Tutar])) AS [Toplam Ciro]", s);   // Y1 hesaplananlar hep parantezlenir
        Assert.Contains("FROM [dbo].[Siparisler] AS [s]", s);
        Assert.DoesNotContain("GROUP BY", s);   // tek deger: kirilimsiz toplam
    }

    [Fact]
    public void alansiz_ve_formulsuz_model_reddedilir()
    {
        var model = new QueryModel(Guid.NewGuid(), Alanlar: [], Filtre: null, Siralama: [], SatirLimiti: null);
        var h = Assert.Throws<QueryModelException>(() => Uretici.Generate(model, Harita()));
        Assert.Contains("En az bir alan", h.Message);
    }

    [Fact]
    public void ara_tablolar_raporlanir_kullanici_secmedigi_halde_yola_girenler()
    {
        /* S40 — SESSİZ YANLIŞ: üretici tabloları haritadaki ilişkilerden kendisi birleştirir ve
         * bir tabloya birden çok eşit uzunlukta yol varsa BİRİNİ seçer. Ölçülen vaka: "AUDİ marka
         * araçların tutarı" sorusunda Markalar'a Araclar.MarkaId yerine Servisler.AnaBayiMarkaId
         * üzerinden bağlanıldı — aracın markası değil servisin bayi markası süzüldü, hata da
         * çıkmadı. Yol DEĞİŞTİRİLMEZ (doğrusu sorunun anlamına bağlı) ama GÖRÜNÜR kılınır. */
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TU, "Kategori")],
            Filtre: null, Siralama: [], SatirLimiti: null);

        var r = Uretici.Generate(model, Harita());

        // Musteriler ↔ Urunler doğrudan bağlı değil; Siparisler yola KATILDI ama kullanıcı istemedi.
        Assert.Contains("Siparisler", r.AraTablolar!);
        Assert.Contains("[dbo].[Siparisler]", r.Sql);
    }

    [Fact]
    public void ara_tablo_yoksa_liste_BOS_kalir()
    {
        // Kullanıcının seçtiği tablolar doğrudan bağlıysa söylenecek bir şey yok — aksi hâlde
        // uyarı gürültüye döner ve gerçek belirsizlik gözden kaçar.
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TM, "Sehir"), new SelectedField(TS, "Tutar", ToplamaTuru.Topla)],
            Filtre: null, Siralama: [], SatirLimiti: null);

        var r = Uretici.Generate(model, Harita());

        Assert.Empty(r.AraTablolar!);
    }
}
