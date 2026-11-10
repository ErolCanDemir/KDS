using Kds.Api.Subscriptions;
using MailKit.Security;
using Xunit;

namespace Kds.Tests;

/// <summary>SMTP bağlantısının şifreleme kipi seçimi. Bu seçim 18 Ağu 2026'da canlı doğrulamada
/// ürünü tamamen durduran sınırdan doğdu: eski <c>System.Net.Mail.SmtpClient</c> yalnız STARTTLS
/// konuşuyordu ve birçok ağ spam koruması için 25/587'yi kapatıp SADECE 465'e izin verdiği için
/// o ağlarda hiç e-posta gönderilemiyordu. Kip yanlış seçilirse belirti sinsidir — bağlantı kurulur,
/// sonra el sıkışma tutmaz ve hata "Failure sending mail." gibi sebebi söylemeyen bir mesaja döner.</summary>
public sealed class EpostaTlsKipiTests
{
    [Fact]
    public void ssl_kapaliysa_sifresiz_baglanir()
    {
        // Yerel test sunucuları (smtp4dev vb.) TLS istemez; ssl kapalıyken port ne olursa olsun düz.
        Assert.Equal(SecureSocketOptions.None, EpostaGonderici.TlsKipi(2525, sslKullan: false));
        Assert.Equal(SecureSocketOptions.None, EpostaGonderici.TlsKipi(465, sslKullan: false));
    }

    [Fact]
    public void port_465_ortuk_tls_kullanir()
    {
        // 465 = SMTPS: sunucu daha ilk anda TLS bekler. STARTTLS denenirse bağlantı düşer.
        Assert.Equal(SecureSocketOptions.SslOnConnect, EpostaGonderici.TlsKipi(465, sslKullan: true));
    }

    [Theory]
    [InlineData(587)]
    [InlineData(25)]
    [InlineData(2525)]
    public void diger_portlar_starttls_kullanir(int port)
    {
        Assert.Equal(SecureSocketOptions.StartTls, EpostaGonderici.TlsKipi(port, sslKullan: true));
    }
}

/// <summary>SMTP hatalarının yöneticiye dönük açıklamaya çevrilmesi. Bu sınıf 18 Ağu 2026'da
/// yaşanan somut olaydan doğdu: Gmail "Application-specific password required" diyordu, ekranda
/// ise "An error occurred while processing your request." görünüyordu — yani ürün, çözümü
/// kullanıcının elinde olan bir yapılandırma hatasını anlaşılmaz bir kazaya çeviriyordu.</summary>
public sealed class EpostaHataMesajiTests
{
    [Fact]
    public void kimlik_reddinde_uygulama_parolasi_yolu_gosterilir()
    {
        var mesaj = EpostaGonderici.HataAciklamasi(
            new MailKit.Security.AuthenticationException("534: 5.7.9 Application-specific password required"),
            "smtp.gmail.com", 465);

        Assert.Contains("uygulama parolası", mesaj);
        Assert.Contains("Sistem Ayarları", mesaj);
        // Sunucunun HAM yanıtı korunur — sebebi asıl orada yazıyor.
        Assert.Contains("Application-specific password required", mesaj);
    }

    [Fact]
    public void baglanti_reddinde_port_465_onerilir()
    {
        // Ölçülen gerçek durum: ağ 25/587'yi kapatıp yalnız 465'e izin veriyordu.
        var mesaj = EpostaGonderici.HataAciklamasi(
            new System.Net.Sockets.SocketException(10061), "smtp.gmail.com", 587);

        Assert.Contains("smtp.gmail.com:587", mesaj);
        Assert.Contains("465", mesaj);
    }

    [Fact]
    public void taninmayan_hata_da_sunucu_ve_portu_soyler()
    {
        var mesaj = EpostaGonderici.HataAciklamasi(new InvalidOperationException("beklenmedik"), "posta.firma.com", 25);

        Assert.Contains("posta.firma.com:25", mesaj);
        Assert.Contains("beklenmedik", mesaj);
    }
}
