using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kds.Data;

/// <summary>Tasarım-zamanı (dotnet ef migrations/update) için DbContext üretir. Runtime DI'dan
/// bağımsızdır; tenant bağlamı boş (migration şemayı üretir, veri sorgulamaz).
/// <para>S16/A1: bağlantı dizesi ARTIK KODA GÖMÜLÜ DEĞİL — <c>KDS_METADATA_BAGLANTI</c> ortam
/// değişkeninden okunur. Yoksa hangi veritabanına yazacağını uydurmak yerine AÇIKÇA hata verir
/// (yanlış sunucuya migration atmak, sessiz varsayılandan çok daha pahalıdır).</para>
/// <example>PowerShell: <c>$env:KDS_METADATA_BAGLANTI='Server=...;Database=KdsMetadata;...'</c></example></summary>
public sealed class KdsDbContextFactory : IDesignTimeDbContextFactory<KdsDbContext>
{
    public const string OrtamDegiskeni = "KDS_METADATA_BAGLANTI";

    public KdsDbContext CreateDbContext(string[] args)
    {
        var dize = Environment.GetEnvironmentVariable(OrtamDegiskeni);
        if (string.IsNullOrWhiteSpace(dize))
            throw new InvalidOperationException(
                $"Tasarım-zamanı bağlantısı yok: {OrtamDegiskeni} ortam değişkenini ayarlayın " +
                "(ör. $env:KDS_METADATA_BAGLANTI='Server=(localdb)\\MSSQLLocalDB;Database=KdsMetadata;" +
                "Trusted_Connection=True;TrustServerCertificate=True'). Bu değişken YALNIZ " +
                "'dotnet ef' komutları içindir; uygulama kendi kurulumundan okur.");

        var secenekler = new DbContextOptionsBuilder<KdsDbContext>().UseSqlServer(dize).Options;
        return new KdsDbContext(secenekler, new FixedTenantContext(null));
    }
}
