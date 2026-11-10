using Kds.Data;
using Kds.Data.Entities;
using Kds.Execution;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Kds.Api.Kurulum;

/// <summary>S16/A2 — kurulum işlerinin gövdesi: bağlantı testi, kimlik testi ve kurulumun
/// tamamlanması (veritabanını gerekirse yarat → şemayı kur → ilk kiracı → yapılandırmayı yaz).
/// DI'daki <see cref="KdsDbContext"/> KULLANILMAZ: kurulum sırasında geçerli bağlantı henüz
/// uygulamanın durumunda değildir; şema, aday bağlantıyla kurulan ayrı bir context'le atılır ve
/// yapılandırma ancak BAŞARIDAN SONRA yazılır (yarım kurulum bırakmaz).
/// try/catch meşru — IO adaptörü sınırı (AGENTS §4.6).</summary>
public sealed class KurulumHizmeti(KurulumDurumu durum, IHttpClientFactory istemciFabrikasi)
{
    /// <summary>Aday veritabanını dener: bağlanır mı, veritabanı var mı, yoksa yaratılabilir mi,
    /// şema kurmak için DDL yetkisi var mı.</summary>
    public async Task<VeritabaniTestYanit> VeritabaniTestAsync(VeritabaniIstek istek, CancellationToken ct)
    {
        try
        {
            var dize = Dize(istek);
            await using var baglanti = new SqlConnection(dize);
            await baglanti.OpenAsync(ct);
            var surum = await SkalerAsync(baglanti, "SELECT LEFT(@@VERSION, 120)", ct) as string;
            // Şema kurabilmek için CREATE TABLE yetkisi şart (migration DDL atar).
            var ddl = Convert.ToInt32(await SkalerAsync(baglanti,
                "SELECT ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE'), 0)", ct)) == 1;
            return new VeritabaniTestYanit(true,
                ddl ? "Bağlantı başarılı; şema kurulabilir." : "Bağlanıldı ama bu kullanıcının tablo oluşturma (DDL) yetkisi yok.",
                surum, VeritabaniVar: true, OlusturabilirMi: false, DdlYetkisi: ddl);
        }
        catch (SqlException hata) when (hata.Number is 4060 or 911)
        {
            // Veritabanı yok: sunucuya (master) bağlanıp yaratma yetkisini kontrol et — sihirbaz
            // "yaratayım mı?" diye sorabilsin (ürün kurulumunda en sık senaryo).
            return await VeritabaniYokAsync(istek, ct);
        }
        catch (Exception hata)
        {
            return new VeritabaniTestYanit(false, $"Bağlanılamadı: {hata.Message}");
        }
    }

    private async Task<VeritabaniTestYanit> VeritabaniYokAsync(VeritabaniIstek istek, CancellationToken ct)
    {
        try
        {
            await using var baglanti = new SqlConnection(Dize(istek with { Veritabani = "master" }));
            await baglanti.OpenAsync(ct);
            var surum = await SkalerAsync(baglanti, "SELECT LEFT(@@VERSION, 120)", ct) as string;
            var yaratabilir = Convert.ToInt32(await SkalerAsync(baglanti,
                "SELECT ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE'), 0)", ct)) == 1;
            return new VeritabaniTestYanit(yaratabilir,
                yaratabilir
                    ? $"'{istek.Veritabani}' veritabanı yok — kurulumda oluşturulacak."
                    : $"'{istek.Veritabani}' veritabanı yok ve bu kullanıcı veritabanı oluşturamıyor. " +
                      "Veritabanını DBA'ya oluşturtun ya da yetkili bir kullanıcı girin.",
                surum, VeritabaniVar: false, OlusturabilirMi: yaratabilir, DdlYetkisi: yaratabilir);
        }
        catch (Exception hata)
        {
            return new VeritabaniTestYanit(false, $"Sunucuya bağlanılamadı: {hata.Message}");
        }
    }

    /// <summary>Kimlik sağlayıcının OpenID keşif belgesine erişilebiliyor mu.</summary>
    public async Task<KimlikTestYanit> KimlikTestAsync(KimlikIstek istek, CancellationToken ct)
    {
        var adres = !string.IsNullOrWhiteSpace(istek.MetadataAdresi)
            ? istek.MetadataAdresi!
            : istek.Yetkili.TrimEnd('/') + "/.well-known/openid-configuration";
        try
        {
            using var istemci = istemciFabrikasi.CreateClient();
            istemci.Timeout = TimeSpan.FromSeconds(10);
            using var yanit = await istemci.GetAsync(adres, ct);
            if (!yanit.IsSuccessStatusCode)
                return new KimlikTestYanit(false, $"Kimlik sağlayıcı {(int)yanit.StatusCode} döndü: {adres}");

            using var belge = System.Text.Json.JsonDocument.Parse(await yanit.Content.ReadAsStringAsync(ct));
            var yayinci = belge.RootElement.TryGetProperty("issuer", out var i) ? i.GetString() : null;
            return new KimlikTestYanit(true, "Kimlik sağlayıcıya ulaşıldı.", yayinci);
        }
        catch (Exception hata)
        {
            return new KimlikTestYanit(false, $"Kimlik sağlayıcıya ulaşılamadı ({adres}): {hata.Message}");
        }
    }

    /// <summary>Kurulumu tamamlar. Sıra önemlidir: önce şema+kiracı (aday bağlantıyla), sonra
    /// yapılandırma yazımı — böylece hata hâlinde uygulama kurulum modunda kalır.</summary>
    public async Task<TamamlaYanit> TamamlaAsync(TamamlaIstek istek, CancellationToken ct)
    {
        var test = await VeritabaniTestAsync(istek.Veritabani, ct);
        if (!test.Basarili)
            return new TamamlaYanit(false, test.Mesaj, Guid.Empty);

        if (!test.VeritabaniVar)
            await VeritabaniYaratAsync(istek.Veritabani, ct);

        var dize = Dize(istek.Veritabani);
        var kiraciId = istek.KiraciId ?? Guid.NewGuid();

        // Şema + ilk kiracı: DI'dan bağımsız context (interceptor/tenant filtresi gerekmez).
        var secenekler = new DbContextOptionsBuilder<KdsDbContext>().UseSqlServer(dize).Options;
        await using (var ctx = new KdsDbContext(secenekler, new FixedTenantContext(null)))
        {
            await ctx.Database.MigrateAsync(ct);
            if (!await ctx.Tenantlar.AnyAsync(t => t.Id == kiraciId, ct))
            {
                ctx.Tenantlar.Add(new Tenant { Id = kiraciId, Ad = istek.KiraciAdi.Trim() });
                await ctx.SaveChangesAsync(ct);
            }
        }

        durum.Guncelle(new KurulumAyarlari
        {
            MetadataBaglantisi = dize,
            DepoBaglantisi = istek.DepoSunucusu?.Trim() ?? string.Empty,
            LogBaglantisi = istek.LogBaglantisi?.Trim() ?? string.Empty,
            KimlikYetkili = istek.Kimlik?.Yetkili.Trim() ?? string.Empty,
            KimlikIstemci = istek.Kimlik?.Istemci.Trim() ?? string.Empty,
            KimlikMetadataAdresi = istek.Kimlik?.MetadataAdresi,
            KimlikHttpsZorunlu = istek.Kimlik?.HttpsZorunlu ?? true,
            OnyuzAdres = istek.OnyuzAdres?.Trim() ?? string.Empty,
        });
        KurulumAnahtari.Temizle();

        return new TamamlaYanit(true,
            "Kurulum tamamlandı. Kimlik ayarlarının etkin olması için uygulamayı yeniden başlatın.", kiraciId);
    }

    private static async Task VeritabaniYaratAsync(VeritabaniIstek istek, CancellationToken ct)
    {
        await using var baglanti = new SqlConnection(Dize(istek with { Veritabani = "master" }));
        await baglanti.OpenAsync(ct);
        await using var komut = baglanti.CreateCommand();
        // Ad kullanıcıdan gelir → köşeli parantez kaçışı + tırnak kaçışı (enjeksiyon kapalı).
        var ad = istek.Veritabani.Replace("]", "]]");
        komut.CommandText = $"IF DB_ID(N'{istek.Veritabani.Replace("'", "''")}') IS NULL EXEC('CREATE DATABASE [{ad}]')";
        await komut.ExecuteNonQueryAsync(ct);
    }

    private static string Dize(VeritabaniIstek i) =>
        SqlConnectionString.Kur(i.Sunucu.Trim(), i.Veritabani.Trim(), i.KullaniciAdi, i.Sifre);

    private static async Task<object?> SkalerAsync(SqlConnection baglanti, string sql, CancellationToken ct)
    {
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sql;
        return await komut.ExecuteScalarAsync(ct);
    }
}
