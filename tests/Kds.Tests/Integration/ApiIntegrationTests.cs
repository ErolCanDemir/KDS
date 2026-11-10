using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kds.Tests.Integration;

/// <summary>HTTP-seviyesi entegrasyon: gerçek controller → handler → EF (SQLite) boru hattı, sahte
/// "Test" kimliğiyle. Her test kendi tenant/sub GUID'ini kullanır → izolasyon (paylaşımlı SQLite'ta
/// bile testler birbirine karışmaz). Hedef veri kaynağına gidilmez; sorgu SQL'i ÜRETİMİ test edilir
/// (gerçek çalıştırma-sonucu kıyası E2E katmanındadır).</summary>
public sealed class ApiIntegrationTests(KdsWebFactory fabrika) : IClassFixture<KdsWebFactory>
{
    private HttpClient Istemci(string tenant, string roller = "admin")
    {
        var c = fabrika.CreateClient();
        c.DefaultRequestHeaders.Add("X-Test-Auth", "1");
        c.DefaultRequestHeaders.Add("X-Test-Tenant", tenant);
        c.DefaultRequestHeaders.Add("X-Test-Sub", Guid.NewGuid().ToString());
        c.DefaultRequestHeaders.Add("X-Test-Roles", roller);
        return c;
    }

    // Her test kendi tenant'ını (satırıyla) tohumlar → izolasyon + User→Tenant FK karşılanır.
    private async Task<HttpClient> YeniIstemci(string roller = "admin")
        => Istemci(await fabrika.TenantTohumlaAsync(), roller);

    private static async Task<Guid> Id(HttpResponseMessage yanit)
    {
        var govde = await yanit.Content.ReadAsStringAsync();
        Assert.True(yanit.IsSuccessStatusCode, $"{(int)yanit.StatusCode}: {govde}");
        return JsonDocument.Parse(govde).RootElement.GetProperty("id").GetGuid();
    }

    // --- Kimlik / yetki ---

    [Fact]
    public async Task anonim_istek_401_doner()
    {
        var c = fabrika.CreateClient();   // X-Test-Auth yok → kimlik verilmez
        var yanit = await c.GetAsync("/api/baglantilar");
        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
    }

    [Fact]
    public async Task kimlikli_bos_liste_200_doner()
    {
        var yanit = await (await YeniIstemci()).GetAsync("/api/baglantilar");
        yanit.EnsureSuccessStatusCode();
        var dizi = await yanit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, dizi.ValueKind);
        Assert.Equal(0, dizi.GetArrayLength());
    }

    // --- Bağlantılar (S4) ---

    [Fact]
    public async Task baglanti_eklenir_listede_gorunur_parola_sizmaz()
    {
        var c = await YeniIstemci();
        var ekle = await c.PostAsJsonAsync("/api/baglantilar", new
        {
            ad = "Üretim", sunucu = "sql-1", veritabani = "SatisDB",
            kullaniciAdi = "reader", sifre = "gizli-parola-123", mod = "Canli",
        });
        var govde = await ekle.Content.ReadAsStringAsync();
        Assert.True(ekle.IsSuccessStatusCode, govde);
        Assert.DoesNotContain("gizli-parola-123", govde);   // düz parola yanıtta sızmaz

        var liste = await c.GetFromJsonAsync<JsonElement>("/api/baglantilar");
        Assert.Equal(1, liste.GetArrayLength());
        Assert.Equal("Üretim", liste[0].GetProperty("ad").GetString());
    }

    [Fact]
    public async Task baglanti_tenant_izolasyonu()
    {
        var t1 = await fabrika.TenantTohumlaAsync();
        await Istemci(t1).PostAsJsonAsync("/api/baglantilar", new
        {
            ad = "A", sunucu = "s", veritabani = "d",
            kullaniciAdi = (string?)null, sifre = (string?)null, mod = "Canli",
        });

        var liste2 = await (await YeniIstemci()).GetFromJsonAsync<JsonElement>("/api/baglantilar");
        Assert.Equal(0, liste2.GetArrayLength());   // başka tenant görmemeli
    }

    [Fact]
    public async Task gecersiz_baglanti_400_problemdetails()
    {
        var c = await YeniIstemci();
        var yanit = await c.PostAsJsonAsync("/api/baglantilar", new
        {
            ad = "", sunucu = "", veritabani = "",
            kullaniciAdi = (string?)null, sifre = (string?)null, mod = "Canli",
        });
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
    }

    // --- Panolar (S11) ---

    [Fact]
    public async Task pano_eklenir_liste_ve_detay_doner()
    {
        var c = await YeniIstemci();
        var id = await Id(await c.PostAsJsonAsync("/api/panolar", new
        {
            id = (Guid?)null, ad = "Satış Panosu", paylasim = "Ozel", raporlar = Array.Empty<Guid>(),
        }));

        var liste = await c.GetFromJsonAsync<JsonElement>("/api/panolar");
        Assert.Equal(1, liste.GetArrayLength());

        var detay = await c.GetFromJsonAsync<JsonElement>($"/api/panolar/{id}");
        Assert.Equal("Satış Panosu", detay.GetProperty("ad").GetString());
    }

    // --- Haritalar (S6) + Raporlar (S9/S10) + Sorgu üretimi (S7) ---

    /// <summary>Bir bağlantı + tek tablolu (Id/Tutar/Şehir) minimal harita oluşturur; sorgu/rapor
    /// testlerinin ortak tohumu. Tablo Id'si client üretimlidir → modelde doğrudan referanslanır.</summary>
    private async Task<(Guid connId, Guid mapId, Guid tableId)> HaritaTohumla(HttpClient c)
    {
        var connId = await Id(await c.PostAsJsonAsync("/api/baglantilar", new
        {
            ad = "Demo", sunucu = "s", veritabani = "d",
            kullaniciAdi = (string?)null, sifre = (string?)null, mod = "Canli",
        }));

        var tableId = Guid.NewGuid();
        var mapId = await Id(await c.PostAsJsonAsync("/api/haritalar", new
        {
            id = (Guid?)null, ad = "Satış Haritası", baglantiId = connId, mod = "Canli",
            tablolar = new object[]
            {
                new
                {
                    id = tableId, sema = "dbo", hamAd = "Satislar", dostcaAd = "Satışlar",
                    kolonlar = new object[]
                    {
                        new { hamAd = "Id", dostcaAd = (string?)null, rol = "Anahtar", veriTipi = "int" },
                        new { hamAd = "Tutar", dostcaAd = "Tutar", rol = "Olcu", veriTipi = "decimal" },
                        new { hamAd = "Sehir", dostcaAd = "Şehir", rol = "Boyut", veriTipi = "nvarchar" },
                    },
                },
            },
            iliskiler = Array.Empty<object>(),
        }));
        return (connId, mapId, tableId);
    }

    [Fact]
    public async Task harita_kaydedilir_ve_detay_geri_gelir()
    {
        var c = await YeniIstemci();
        var (_, mapId, _) = await HaritaTohumla(c);

        var detay = await c.GetFromJsonAsync<JsonElement>($"/api/haritalar/{mapId}");
        Assert.Equal("Satış Haritası", detay.GetProperty("ad").GetString());
        Assert.Equal(1, detay.GetProperty("tablolar").GetArrayLength());
        Assert.Equal(3, detay.GetProperty("tablolar")[0].GetProperty("kolonlar").GetArrayLength());
    }

    [Fact]
    public async Task rapor_ham_sql_editor_yolu_kaydedilir()
    {
        var c = await YeniIstemci();
        var (connId, _, _) = await HaritaTohumla(c);

        var yanit = await c.PostAsJsonAsync("/api/raporlar", new
        {
            id = (Guid?)null, ad = "Ham Rapor", klasorId = (Guid?)null, model = (object?)null,
            gorsellestirme = "Tablo", paylasim = "Ozel", hamSql = "SELECT 1 AS Deger", baglantiId = connId,
        });
        Assert.True(yanit.IsSuccessStatusCode, await yanit.Content.ReadAsStringAsync());

        var liste = await c.GetFromJsonAsync<JsonElement>("/api/raporlar");
        Assert.Contains(liste.EnumerateArray(), r => r.GetProperty("ad").GetString() == "Ham Rapor");
    }

    [Fact]
    public async Task sorgu_modeli_surukle_birak_yolu_gecerli_sql_uretir()
    {
        var c = await YeniIstemci();
        var (_, mapId, tableId) = await HaritaTohumla(c);

        // Sürükle-bırak modeli: Şehir'e göre Tutar toplamı.
        var model = new
        {
            haritaId = mapId,
            alanlar = new object[]
            {
                new { tabloId = tableId, kolon = "Sehir", toplama = "Yok", takmaAd = (string?)null },
                new { tabloId = tableId, kolon = "Tutar", toplama = "Topla", takmaAd = "ToplamTutar" },
            },
            filtre = (object?)null,
            siralama = Array.Empty<object>(),
            satirLimiti = (int?)100,
        };

        // Uç artık request-record bekler: { model, parametreDegerleri? } (parametreli rapor).
        var yanit = await c.PostAsJsonAsync("/api/sorgu/sql", new { model, parametreDegerleri = (object?)null });
        var govde = await yanit.Content.ReadAsStringAsync();
        Assert.True(yanit.IsSuccessStatusCode, govde);

        var uretilen = JsonDocument.Parse(govde).RootElement;
        var sql = uretilen.GetProperty("sql").GetString()!;
        Assert.Contains("Satislar", sql);                                     // tablo ham adı
        Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);      // Topla → SUM
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase); // boyut+ölçü → grupla
    }

    [Fact]
    public async Task sorgu_ureticisi_musteri_rolune_kapali_403()
    {
        // QueryController yalnız admin+gelistirici; musteri erişemez.
        var c = await YeniIstemci("musteri");
        var yanit = await c.PostAsJsonAsync("/api/sorgu/sql", new
        {
            haritaId = Guid.NewGuid(), alanlar = Array.Empty<object>(),
            filtre = (object?)null, siralama = Array.Empty<object>(), satirLimiti = (int?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
    }

    [Fact]
    public async Task whoami_roller_alani_turkce_wire_ile_doner()
    {
        // Regresyon guard: /api/durum/kimlik alan adı 'roller' (Türkçe wire) olmalı. 'Roles' (İngilizce)
        // olursa frontend `y.roller` boş kalır → rol-korumalı menü/butonlar tümüyle gizlenir.
        var c = await YeniIstemci("admin,gelistirici");
        var kimlik = await c.GetFromJsonAsync<JsonElement>("/api/durum/kimlik");
        Assert.True(kimlik.TryGetProperty("roller", out var roller), "yanıtta 'roller' alanı yok");
        var roller_ = roller.EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("admin", roller_);
        Assert.Contains("gelistirici", roller_);
    }
}
