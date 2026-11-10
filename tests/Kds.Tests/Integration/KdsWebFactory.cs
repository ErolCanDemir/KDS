using Kds.Data;
using Kds.Execution;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kds.Tests.Integration;

/// <summary>API'yi gerçek boru hattıyla (controller → handler → EF) bellekte ayağa kaldırır; iki şey
/// değişir: (1) metadata deposu SQLite (paylaşımlı in-memory bağlantı), (2) kimlik "Test" şeması
/// (Keycloak gerekmez). Hedef veri kaynağına (KdsDemo) gidilmez — bağlantı denetçisi sahtelenir.</summary>
public sealed class KdsWebFactory : WebApplicationFactory<Program>
{
    // Paylaşımlı-önbellek in-memory DB (fabrika örneğine özel ad → paralel test sınıfları çakışmaz).
    // Tek bağlantılı :memory: yerine bunu kullanırız: EF her DbContext için KENDİ bağlantısını açar
    // → eşzamanlı istekler (yük testi) serileşmeden çalışır. DB yalnız en az bir bağlantı açıkken
    // yaşadığından bir "canlı tutucu" bağlantı açık tutulur.
    private readonly string _dbAdi = "kdsmeta-" + Guid.NewGuid().ToString("N");
    private SqliteConnection? _canliTutucu;

    private string BaglantiDizesi => $"Data Source={_dbAdi};Mode=Memory;Cache=Shared";

    protected override void ConfigureWebHost(IWebHostBuilder olusturucu)
    {
        _canliTutucu = new SqliteConnection(BaglantiDizesi);
        _canliTutucu.Open();

        // Program açılışta (build anı) bu değerleri Configuration'dan okur, yoksa AÇIKÇA patlar.
        // "Testing" ortamında appsettings.Development.json yüklenmez; değerleri ortam değişkeniyle
        // enjekte ederiz — WebApplicationFactory + üst-seviye Program'da en güvenilir yol
        // (ConfigureAppConfiguration zamanlaması build-anı okumasına yetişmez). Çift-alt-çizgi = ':'.
        Environment.SetEnvironmentVariable("ConnectionStrings__KdsMetadata", "Server=kullanilmaz;Database=kullanilmaz;");
        Environment.SetEnvironmentVariable("Kimlik__Yetkili", "http://localhost:8080/realms/kds");
        Environment.SetEnvironmentVariable("Kimlik__Istemci", "kds-api");
        Environment.SetEnvironmentVariable("Kimlik__HttpsZorunlu", "false");
        Environment.SetEnvironmentVariable("Onyuz__Adres", "http://localhost:5173");

        // "Development" olsaydı Program açılışta Database.Migrate() (SQL Server) çağırırdı; "Testing" atlatır.
        olusturucu.UseEnvironment("Testing");

        // İçerik kökünü var olan bir dizine sabitle: WebApplicationFactory, Mvc.Testing attribute'u
        // olmayan bir giriş derlemesinden (ör. yük testi projesi) çağrılınca kökü yanlış tahmin edip
        // "DirectoryNotFound" verir. Config env ile enjekte edildiği ve statik dosya sunulmadığı için
        // herhangi bir gerçek dizin yeterlidir.
        olusturucu.UseContentRoot(AppContext.BaseDirectory);

        // Gürültüyü kes: EF her SQL'i Information'da loglar → test çıktısını doldurur ve yük
        // ölçümünü şişirir. Üretim de tipik olarak Warning'de koşar → daha temiz + gerçekçi.
        olusturucu.ConfigureLogging(g => g.ClearProviders().SetMinimumLevel(LogLevel.Warning));

        olusturucu.ConfigureTestServices(servisler =>
        {
            // KdsDbContext kaydını (SQL Server) kaldır, SQLite ile yeniden ekle. EF Core 10'da
            // AddDbContext, sağlayıcıyı ayrıca IDbContextOptionsConfiguration<KdsDbContext> olarak
            // kaydeder; onu da silmezsek hem SqlServer hem Sqlite kayıtlı kalır ("tek sağlayıcı" hatası).
            var kaldirilacak = servisler
                .Where(d => d.ServiceType == typeof(DbContextOptions<KdsDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || d.ServiceType == typeof(KdsDbContext)
                         || (d.ServiceType.IsGenericType
                             && d.ServiceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1"
                             && d.ServiceType.GenericTypeArguments is [var arg] && arg == typeof(KdsDbContext)))
                .ToList();
            foreach (var d in kaldirilacak) servisler.Remove(d);
            servisler.AddDbContext<KdsDbContext>(s => s.UseSqlite(BaglantiDizesi));

            // Gerçek SQL Server'a giden bağlantı denetçisini sahtele (test uçları hedef DB'ye gitmez).
            servisler.RemoveAll<IConnectionProber>();
            servisler.AddSingleton<IConnectionProber, SahteDenetci>();

            // Kimliği "Test" şemasına al: varsayılan şema Test → FallbackPolicy bununla doğrular.
            servisler.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Tanınmayan (500) istisnaları test tarafından okunabilsin diye yakala (üretim davranışını
            // değiştirmez: false döner, gerçek 500 yine üretilir).
            servisler.AddSingleton<SonHata>();
            servisler.AddExceptionHandler<YakalayanIstisnaIsleyici>();
        });
    }

    /// <summary>Yeni bir Tenant satırı ekler ve id'sini döndürür. Testler bunu çağırıp dönen tenant'ı
    /// kullanır: her test kendi tenant'ında izole çalışır (paylaşımlı SQLite'ta bile) VE User→Tenant
    /// FK'si karşılanır (gerçek uygulamada tenant seed'lidir; test DB'si boş başlar).</summary>
    public async Task<string> TenantTohumlaAsync()
    {
        var id = Guid.NewGuid();
        using var kapsam = Services.CreateScope();
        var ctx = kapsam.ServiceProvider.GetRequiredService<KdsDbContext>();
        ctx.Tenantlar.Add(new Kds.Data.Entities.Tenant { Id = id, Ad = "Test-" + id.ToString()[..8] });
        await ctx.SaveChangesAsync();
        return id.ToString();
    }

    // Host kurulduktan sonra SQLite şemasını bir kez oluştur (Testing ortamında Migrate atlanır).
    protected override IHost CreateHost(IHostBuilder olusturucu)
    {
        var host = base.CreateHost(olusturucu);
        using var kapsam = host.Services.CreateScope();
        kapsam.ServiceProvider.GetRequiredService<KdsDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _canliTutucu?.Dispose();
    }

    private sealed class SahteDenetci : IConnectionProber
    {
        public Task<ProbeResult> ProbeAsync(string s, string v, string? k, string? sf, CancellationToken i)
            => Task.FromResult(new ProbeResult(true, 3, null));
    }

    /// <summary>Son tanınmayan istisnayı tutar (test tanılaması).</summary>
    public sealed class SonHata { public Exception? Deger; }

    private sealed class YakalayanIstisnaIsleyici(SonHata kutu)
        : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception hata, CancellationToken t)
        {
            kutu.Deger = hata;
            return ValueTask.FromResult(false);   // bize ait değil → framework 500 üretsin
        }
    }
}
