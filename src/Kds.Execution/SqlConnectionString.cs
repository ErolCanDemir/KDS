using Microsoft.Data.SqlClient;

namespace Kds.Execution;

/// <summary>Parçalardan güvenli MSSQL bağlantı dizesi kurar (S4 test + S5 çalıştırma ortak
/// kullanır). <paramref name="kullaniciAdi"/> boşsa entegre (Windows) kimlik. Dev'de
/// TrustServerCertificate=true — sertifikasız yerel kaynaklar için.</summary>
public static class SqlConnectionString
{
    public static string Kur(string sunucu, string veritabani, string? kullaniciAdi, string? sifre, int zamanAsimiSn = 15)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = sunucu,
            InitialCatalog = veritabani,
            ConnectTimeout = zamanAsimiSn,
            TrustServerCertificate = true,
        };
        if (string.IsNullOrWhiteSpace(kullaniciAdi))
            b.IntegratedSecurity = true;
        else
        {
            b.UserID = kullaniciAdi;
            b.Password = sifre ?? string.Empty;
        }
        return b.ConnectionString;
    }

    /// <summary>Bir sunucu bağlantı dizesine (DB'siz) hedef veritabanını yerleştirir. İçe alım
    /// deposu (KdsDepo_&lt;tenant&gt;) ve depo yükleme yolu bunu paylaşır.</summary>
    public static string VeritabaniIle(string sunucuDizesi, string veritabani) =>
        new SqlConnectionStringBuilder(sunucuDizesi) { InitialCatalog = veritabani }.ConnectionString;
}
