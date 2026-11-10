using Kds.Api.Identity;
using Kds.Business;
using Kds.Data;
using Kds.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

const string OnyuzCors = "onyuz";
const string GomuluCors = "GomuluCors";

var olusturucu = WebApplication.CreateBuilder(args);

olusturucu.Services.AddControllers()
    .AddJsonOptions(secenekler =>
        secenekler.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
olusturucu.Services.AddOpenApi();
// Alan istisnalarını temiz ProblemDetails'e çevir (QueryModelException→400, KeyNotFound→404).
olusturucu.Services.AddExceptionHandler<Kds.Api.ExceptionHandler>();
olusturucu.Services.AddProblemDetails();
olusturucu.Services.AddKdsBusiness();

// S16/A1 — KURULUM: ortama bağlı hiçbir değer artık koda gömülü değil ve eksikliği uygulamayı
// ÇÖKERTMEZ; yapılandırılmamış uygulama KURULUM MODUNA girer (yalnız /api/kurulum açık; A2).
// Kaynak sırası: ortam değişkenleri → şifreli kurulum dosyası → appsettings (geçiş) → kurulum modu.
var kurulumDeposu = new Kds.Api.Kurulum.KurulumDeposu();
var (kurulumAyarlari, kurulumKaynagi) = kurulumDeposu.Oku(olusturucu.Configuration);
var kurulumDurumu = new Kds.Api.Kurulum.KurulumDurumu(kurulumDeposu, kurulumAyarlari, kurulumKaynagi);
olusturucu.Services.AddSingleton(kurulumDeposu);
olusturucu.Services.AddSingleton(kurulumDurumu);
// A2: kurulum modunun güvenlik kapısı (tek kullanımlık anahtar) + sihirbazın iş gövdesi.
olusturucu.Services.AddSingleton<Kds.Api.Kurulum.KurulumAnahtari>();
olusturucu.Services.AddScoped<Kds.Api.Kurulum.KurulumHizmeti>();
olusturucu.Services.AddHttpClient();
// DbContext bağlantıyı ÇALIŞMA ZAMANINDA buradan alır (kurulum sonrası yeniden başlatma yok).
olusturucu.Services.AddSingleton<Kds.Data.IMetadataBaglantiSaglayici>(kurulumDurumu);
olusturucu.Services.AddKdsData();

// Olay günlüğü (KdsLog Mongo, QMS'ten AYRI DB) — Action/Exception/DataChange olayları. Bağlantı
// yoksa no-op (dev'de Mongo olmadan çalışır). Yazım bloklamaz; Mongo düşse istek etkilenmez.
olusturucu.Services.AddKdsLogging(kurulumAyarlari.LogBaglantisi);

// Kimlik/önyüz adresi kurulumdan gelir. Kurulum modunda (henüz yapılandırılmamış) bunlar boş
// olabilir: uygulama yine AYAĞA KALKAR — kimlik doğrulama gereken uçlar 401/503 döner, kurulum
// uçları anonimdir. Kurulu sistemde eksiklerse açıkça patlar (sessiz varsayılan yok).
var yetkili = kurulumAyarlari.KimlikYetkili;
var apiKitlesi = kurulumAyarlari.KimlikIstemci;
var onyuzAdres = kurulumAyarlari.OnyuzAdres;
if (kurulumDurumu.Yapilandirildi && (string.IsNullOrWhiteSpace(yetkili) || string.IsNullOrWhiteSpace(apiKitlesi)))
    throw new InvalidOperationException(
        $"Kimlik yapılandırması eksik (kaynak: {kurulumKaynagi}) — Yetkili/İstemci zorunlu.");

// Container/ters-vekil (S13 yayın): token'ın issuer'ı PUBLIC URL (tarayıcının gördüğü, = Yetkili)
// iken Keycloak metadata'sı İÇ ağ adresinden çekilebilir. Kimlik:MetadataAdresi ayarlıysa metadata
// oradan gelir, issuer yine Yetkili olarak doğrulanır. Boşsa (dev) Authority ikisini de türetir.
var metadataAdresi = kurulumAyarlari.KimlikMetadataAdresi;
olusturucu.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(ayar =>
    {
        // Kurulum modunda kimlik sağlayıcı henüz bilinmiyor: Authority boş bırakılır — token
        // doğrulanamaz (kimlik isteyen uçlar 401), kurulum uçları anonim çalışır.
        if (!string.IsNullOrWhiteSpace(yetkili)) ayar.Authority = yetkili;
        if (!string.IsNullOrWhiteSpace(apiKitlesi)) ayar.Audience = apiKitlesi;
        if (!string.IsNullOrWhiteSpace(metadataAdresi))
        {
            ayar.MetadataAddress = metadataAdresi;
            ayar.TokenValidationParameters.ValidIssuer = yetkili;
        }

        /* S48 — adresi OYNAK tünelde yayın (ör. localhost.run: *.lhr.life, adres yenilenebilir):
         * issuer tek sabit değere bağlanamaz; Kimlik:IssuerDeseni dolu ise desene uyan issuer
         * kabul edilir. Güvenlik daralması değil kayması: imza anahtarı yine TEK Keycloak'ın
         * jwks'inden, audience yine sabit — desen yalnız aynı sunucunun o anki dış adresine izin
         * verir. Boşsa davranış eskisiyle birebir. */
        var issuerDeseni = kurulumAyarlari.KimlikIssuerDeseni;
        if (!string.IsNullOrWhiteSpace(issuerDeseni))
        {
            var desen = new System.Text.RegularExpressions.Regex(
                issuerDeseni, System.Text.RegularExpressions.RegexOptions.Compiled);
            ayar.TokenValidationParameters.ValidIssuer = null;
            ayar.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                issuer is not null && desen.IsMatch(issuer)
                    ? issuer
                    : throw new Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException(
                        "Token issuer'ı yapılandırılmış desene uymuyor.");
        }
        // Anahtar yoksa guvenli varsayilan: HTTPS zorunlu (yalniz dev'de false'a cekilir).
        ayar.RequireHttpsMetadata = kurulumAyarlari.KimlikHttpsZorunlu;
        // Claim adlarini olduklari gibi tut (sub -> NameIdentifier map'i kapali);
        // boylece 'sub', 'tenant_id', 'email' token'daki adiyla okunur.
        ayar.MapInboundClaims = false;
        ayar.TokenValidationParameters.NameClaimType = "preferred_username";
    });

// Varsayilan: her ucun kimlik dogrulanmis kullanici istemesi; public uclar ACIKCA
// [AllowAnonymous] der. Yeni bir uca [Authorize] koymayi unutmak onu anonim ACMAZ.
olusturucu.Services.AddAuthorization(secenekler =>
    secenekler.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

olusturucu.Services.AddTransient<IClaimsTransformation, KeycloakRoleMapping>();

// Tenant baglami (istegin token'indan) + JIT provisioning onbellegi.
olusturucu.Services.AddHttpContextAccessor();
olusturucu.Services.AddScoped<ITenantContext, HttpTenantContext>();
// User baglami (S10): rapor sahibi/favori/gorunurluk kararlari icin.
olusturucu.Services.AddScoped<Kds.Business.Identity.IUserContext, HttpUserContext>();
// Aksiyon bağlamı (olay günlüğü korelasyonu): istek-başına ActionId + kullanıcı/tenant/ip.
olusturucu.Services.AddScoped<Kds.Contracts.Logging.IActionContext, HttpActionContext>();
olusturucu.Services.AddMemoryCache();

// Uygulama geneli Data Protection halkası KALICI veri dizinindedir: varsayılan konum
// (container'da ~/.aspnet) kap yeniden yaratıldığında uçar ve kayıtlı BAĞLANTI PAROLALARI
// çözülemez hâle gelirdi. Uygulama adı bilerek ayarlanmaz — değiştirilseydi aynı anahtarlarla
// bile eski şifreli değerler açılamazdı. Eski konumda halka varsa bir kereye mahsus kopyalanır.
var uygulamaAnahtarDizini = Kds.Api.Kurulum.VeriDizini.UygulamaAnahtarDizini();
var tasinanAnahtar = Kds.Api.Kurulum.AnahtarHalkasi.EskiHalkayiTasi(uygulamaAnahtarDizini);
olusturucu.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(uygulamaAnahtarDizini));

// Bağlantı yönetimi (S4): parola koruma (Data Protection) + çalıştırma dünyası bağlantı denetçisi.
olusturucu.Services.AddSingleton<Kds.Business.Connections.IPasswordProtector, Kds.Api.Connections.PasswordProtector>();
// S42 — gömme: anahtar imzalayıcı (Data Protection, kalıcı halka) + kiracı kapsamı (AmbientTenant
// sarmalayıcısı; gömülü istekte oturum yok, kiracı anahtardan öğrenilir).
olusturucu.Services.AddSingleton<Kds.Business.Embedding.IGommeAnahtarKoruyucu, Kds.Api.Embedding.GommeAnahtarKoruyucu>();
olusturucu.Services.AddSingleton<Kds.Business.Embedding.IKiraciKapsami, Kds.Api.Embedding.KiraciKapsami>();
// S45 — KDS Ajanı: anahtar imzalayıcı (gömmeyle aynı desen, AYRI amaç dizesi) + push depo yazıcı.
olusturucu.Services.AddSingleton<Kds.Business.Agents.IAjanAnahtarKoruyucu, Kds.Api.Agents.AjanAnahtarKoruyucu>();
olusturucu.Services.AddSingleton<Kds.Execution.IAjanDepoYazici, Kds.Execution.AjanDepoYazici>();
olusturucu.Services.AddSingleton<Kds.Execution.IConnectionProber, Kds.Execution.SqlConnectionProber>();

// Calistirma cekirdegi (S5): SELECT-only bekcisi + sorgu calistirici + sema kesfi (hepsi durumsuz).
olusturucu.Services.AddSingleton<Kds.Execution.IQueryGuard, Kds.Execution.ScriptDomQueryGuard>();
olusturucu.Services.AddSingleton<Kds.Execution.IQueryExecutor, Kds.Execution.SqlQueryExecutor>();
// M33 "Sorun Ne?": sorguyu çalıştırmadan tahmini planı yorumlayan tanılayıcı. Durumsuz.
olusturucu.Services.AddSingleton<Kds.Execution.IQueryDiagnostician, Kds.Execution.SqlQueryDiagnostician>();
olusturucu.Services.AddSingleton<Kds.Execution.ISchemaDiscovery, Kds.Execution.SqlSchemaDiscovery>();
// Sorgu modeli → parametreli SQL üretici (S7, durumsuz).
olusturucu.Services.AddSingleton<Kds.Execution.IQueryGenerator, Kds.Execution.MssqlQueryGenerator>();
// İçe alım çalıştırıcı (S12 · Mod B): kaynak→depo tam yükleme (SqlBulkCopy). Durumsuz.
olusturucu.Services.AddSingleton<Kds.Execution.IIngestRunner, Kds.Execution.SqlIngestRunner>();
// İçe alım deposu ayarı: kendi depo SUNUCUMUZ (tenant başına DB yaratılır). Config yoksa boş.
olusturucu.Services.AddSingleton(new Kds.Business.Ingest.IceAlimAyarlari
{
    DepoSunucuBaglantisi = kurulumAyarlari.DepoBaglantisi,
});
// İçe alım zamanlayıcı (S12 · Faz 2): dakikada bir vadesi gelen cron'lu işleri tenant adına çalıştırır.
olusturucu.Services.AddHostedService<Kds.Api.Ingest.IceAlimZamanlayici>();
// Dışa aktarım (S13): sorgu sonucunu CSV/Excel/PDF'e çevirir (durumsuz; font çözücüsü tek sefer kurulur).
olusturucu.Services.AddSingleton<Kds.Export.IResultExporter, Kds.Export.ResultExporter>();

// Rapor abonelikleri (S15). S16/A3: SMTP bilgisi artık config'ten DI zamanında değil, her
// gönderimde Sistem Ayarları'ndan okunur (ekrandan değişince yeniden başlatma gerekmez);
// sunucu/gönderen boşsa gönderici yalnız günlüğe yazar → boru hattı SMTP'siz de uçtan uca çalışır.
olusturucu.Services.AddScoped<Kds.Business.Subscriptions.IEpostaGonderici, Kds.Api.Subscriptions.EpostaGonderici>();
// Abonelik zamanlayıcı: dakikada bir vadesi gelen cron'lu abonelikleri tenant adına gönderir.
olusturucu.Services.AddHostedService<Kds.Api.Subscriptions.AbonelikZamanlayici>();
// Uyarı zamanlayıcı (S20): aynı hafif desen — vadesi gelen eşik uyarılarını değerlendirir ve
// YALNIZ durum değişince bildirir; aynı e-posta göndericisini paylaşır.
olusturucu.Services.AddHostedService<Kds.Api.Alerts.UyariZamanlayici>();
// Soru Sor (S23): yerel Ollama adaptörü — EpostaGonderici ile aynı karar: adres/model her
// çağrıda Sistem Ayarları'ndan okunur, ekrandan değişince yeniden başlatma gerekmez.
olusturucu.Services.AddScoped<Kds.Business.Nlq.ISoruCevirici, Kds.Api.Nlq.OllamaSoruCevirici>();
// Roles & Yetkiler (S27): Keycloak Admin API adaptörü — roller realm rolleri; servis hesabı
// kimliği kurulum yapılandırmasından gelir (kds-yonetim; infra/KURULUM.md).
olusturucu.Services.AddScoped<Kds.Business.Identity.IRolYonetimi, Kds.Api.Identity.KeycloakRolYonetimi>();

olusturucu.Services.AddCors(ayar => ayar.AddPolicy(OnyuzCors, ilke =>
{
    // Kurulum modunda önyüzün adresi henüz bilinmiyor → sihirbaz herhangi bir kaynaktan
    // çalışabilsin diye serbest (kurulum uçları anonim + kurulum ANAHTARI ister, çerez yok).
    if (string.IsNullOrWhiteSpace(onyuzAdres)) ilke.AllowAnyOrigin();
    else ilke.WithOrigins(onyuzAdres);
    ilke.AllowAnyHeader()
        .AllowAnyMethod()
        // Dışa aktarımda (S13) önyüz indirilecek dosya adını Content-Disposition'dan okur;
        // CORS bu başlığı açıkça exposed etmezse tarayıcı fetch'e vermez (ad "indirilen"e düşer).
        .WithExposedHeaders("Content-Disposition");
}));

/* S42 — GÖMÜLÜ uç için AYRI CORS politikası. Gömülü istek müşterinin KENDİ portalından gelir;
 * önyüz adresine kilitli ana politika bunu engellerdi.
 *
 * AllowAnyOrigin bilinçlidir ve güvenliği GEVŞETMEZ: CORS bir tarayıcı kuralıdır, sunucu kapısı
 * değil. Asıl kapı RunEmbeddedHandler'dadır — Origin/Referer başlığı anahtarın DB'deki izinli
 * alan listesiyle karşılaştırılır. Burada çerez de yoktur (kimlik anahtarla taşınır), yani
 * "AllowCredentials + AllowAnyOrigin" tehlikeli bileşimi oluşmaz. */
olusturucu.Services.AddCors(ayar => ayar.AddPolicy(GomuluCors, ilke =>
    ilke.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var uygulama = olusturucu.Build();

var acilisGunluk = uygulama.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Kds.Kurulum");
if (tasinanAnahtar > 0)
    acilisGunluk.LogInformation(
        "Şifreleme anahtar halkası kalıcı dizine taşındı: {Sayi} anahtar → {Dizin} " +
        "(eski konum çerçevenin varsayılanıydı; container'da kalıcı değildi).",
        tasinanAnahtar, uygulamaAnahtarDizini);
if (!kurulumDurumu.Yapilandirildi)
{
    acilisGunluk.LogWarning(
        "KDS KURULUM MODUNDA: yönetim veritabanı yapılandırılmamış (kaynak: {Kaynak}). " +
        "Kurulum sihirbazını açın; veri dizini: {Dizin}", kurulumKaynagi, Kds.Api.Kurulum.VeriDizini.Yol());
    // Kurulum anahtarını AÇILIŞTA üret + logla (yönetici ilk bakışta görsün; tembel üretimde
    // anahtar ancak ilk istekte doğar ve açılış logunda görünmezdi).
    uygulama.Services.GetRequiredService<Kds.Api.Kurulum.KurulumAnahtari>();
}
else if (kurulumKaynagi.StartsWith("appsettings"))
{
    acilisGunluk.LogWarning(
        "Yapılandırma appsettings'ten okundu (eski yol). Ürün kurulumunda ortam değişkeni ya da " +
        "kurulum dosyası önerilir; Sistem Ayarları'ndan taşınabilir.");
}

// Metadata şemasını otomatik oluştur/güncelle (migration'ları uygula). Dev'de hep; yayında
// (tek-VM Compose) MigrationUygula=true ile — ilk açılışta DB'yi kurar. İdempotent.
// Kurulum modunda ATLANIR: hangi veritabanına kurulacağı henüz bilinmiyor (sihirbaz kuracak).
if (kurulumDurumu.Yapilandirildi &&
    (uygulama.Environment.IsDevelopment() || uygulama.Configuration.GetValue("MigrationUygula", false)))
{
    using var kapsam = uygulama.Services.CreateScope();
    kapsam.ServiceProvider.GetRequiredService<KdsDbContext>().Database.Migrate();
}

uygulama.UseExceptionHandler();
uygulama.UseCors(OnyuzCors);
uygulama.UseAuthentication();
uygulama.UseAuthorization();
// Aksiyon olayı: ActionId'yi erken üretir (interceptor/exception aynı Id'yi görsün) + istek
// özetini yazar. UserProvisioning'ten ÖNCE — provisioning'in DataChange'i de bu ActionId'ye bağlanır.
uygulama.UseMiddleware<ActionLogMiddleware>();
uygulama.UseMiddleware<UserProvisioningMiddleware>();

if (uygulama.Environment.IsDevelopment())
{
    uygulama.MapOpenApi().AllowAnonymous();
}

uygulama.MapControllers();

uygulama.Run();

// Entegrasyon testleri (WebApplicationFactory<Program>) giriş noktası tipine erişebilsin diye
// üst-seviye Program'ı açığa çıkarır; üretim davranışını etkilemez.
public partial class Program;
