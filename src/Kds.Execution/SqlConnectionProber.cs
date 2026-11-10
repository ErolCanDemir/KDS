using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Kds.Execution;

/// <summary>MSSQL bağlantı denetimi: kısa timeout ile açar + <c>SELECT 1</c> koşar, süreyi
/// ölçer. <paramref name="kullaniciAdi"/> boşsa entegre (Windows) kimlik. Hata yakalama
/// burada meşrudur — IO adaptörü sınırı (AGENTS §4.6): SqlException/bağlantı hatası kullanıcıya
/// dönecek kısa mesaja çevrilir.</summary>
public sealed class SqlConnectionProber : IConnectionProber
{
    public async Task<ProbeResult> ProbeAsync(
        string sunucu, string veritabani, string? kullaniciAdi, string? sifre, CancellationToken cancellationToken)
    {
        var connectionString = SqlConnectionString.Kur(sunucu, veritabani, kullaniciAdi, sifre, zamanAsimiSn: 5);

        var kronometre = Stopwatch.StartNew();
        try
        {
            await using var baglanti = new SqlConnection(connectionString);
            await baglanti.OpenAsync(cancellationToken);
            await using var komut = baglanti.CreateCommand();
            komut.CommandText = "SELECT 1";
            komut.CommandTimeout = 5;
            await komut.ExecuteScalarAsync(cancellationToken);
            kronometre.Stop();
            return new ProbeResult(true, (int)kronometre.ElapsedMilliseconds, null);
        }
        catch (Exception hata) when (hata is SqlException or InvalidOperationException or ArgumentException)
        {
            return new ProbeResult(false, null, Kisalt(hata.Message));
        }
    }

    private static string Kisalt(string mesaj) => mesaj.Length > 220 ? mesaj[..220] + "…" : mesaj;
}
