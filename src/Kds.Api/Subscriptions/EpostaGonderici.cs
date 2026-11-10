using Kds.Business.Settings;
using Kds.Business.Subscriptions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Kds.Api.Subscriptions;

/// <summary>S16/A3 — e-posta göndericisi. SMTP ayarları DI zamanında config'ten okunmaz; her
/// gönderimde <b>Sistem Ayarları</b>'ndan alınır (Yönetim → Sistem Ayarları → E-posta), böylece
/// SMTP bilgisi ekrandan değiştirilince yeniden başlatma gerekmez.
/// <para>Sunucu ya da gönderen girilmemişse GERÇEKTEN GÖNDERMEZ, ne gönderileceğini günlüğe yazar —
/// abonelik boru hattı (rapor→export→e-posta) SMTP'siz ortamda da uçtan uca çalışsın diye.</para>
/// <para><b>Neden MailKit (18 Ağu 2026):</b> önceki uygulama <c>System.Net.Mail.SmtpClient</c>
/// kullanıyordu; o istemci yalnız <b>STARTTLS</b> konuşur, <b>örtük TLS</b>'i (genelde port 465)
/// desteklemez. Canlı doğrulamada ölçüldü: birçok ağ spam koruması için 25/587'yi kapatıp yalnız
/// 465'e izin veriyor — o ağlarda ürün hiç e-posta gönderemiyordu (465'e TCP bağlanıp düz metin
/// karşılama beklediği için bağlantı kapanıyordu). MailKit (MIT) her iki kipi de konuşur ve
/// <c>SmtpClient</c>'ın <c>SYSLIB0014</c> ile bastırılan obsolete uyarısını da ortadan kaldırır.</para>
/// IO adaptörü — log/try burada meşru (AGENTS §4.6).</summary>
public sealed class EpostaGonderici(ISistemAyarlari ayarlar, ILogger<EpostaGonderici> gunlukcu) : IEpostaGonderici
{
    /// <summary>Bağlantının şifreleme kipini seçer. Kullanıcıya ayrı bir "TLS kipi" ayarı
    /// SORULMAZ; port zaten kipi belli eder ve yanlış eşleşme sessiz başarısızlığın kaynağıdır:
    /// <list type="bullet">
    /// <item>SSL kapalı → şifresiz (yerel test sunucuları; smtp4dev vb.).</item>
    /// <item>SSL açık + port 465 → <b>örtük TLS</b>: bağlantı daha ilk anda şifrelenir.</item>
    /// <item>SSL açık + diğer portlar (587, 25…) → <b>STARTTLS</b>: düz başlar, sonra yükseltilir.</item>
    /// </list></summary>
    public static SecureSocketOptions TlsKipi(int port, bool sslKullan) => (port, sslKullan) switch
    {
        (_, false) => SecureSocketOptions.None,
        (465, true) => SecureSocketOptions.SslOnConnect,
        (_, true) => SecureSocketOptions.StartTls,
    };

    public async Task<bool> GonderAsync(EpostaMesaji mesaj, CancellationToken cancellationToken)
    {
        var sunucu = ayarlar.Metin(AyarTanimlari.EpostaSunucu);
        var gonderen = ayarlar.Metin(AyarTanimlari.EpostaGonderen);

        if (string.IsNullOrWhiteSpace(sunucu) || string.IsNullOrWhiteSpace(gonderen))
        {
            gunlukcu.LogInformation(
                "E-posta GÖNDERİLMEDİ (SMTP sunucusu/gönderen adresi Sistem Ayarları'nda boş) → {Alicilar} · " +
                "konu \"{Konu}\" · ek {Ek} ({Boyut} bayt)",
                string.Join(", ", mesaj.Alicilar), mesaj.Konu, mesaj.Ek?.DosyaAdi ?? "(yok)",
                mesaj.Ek?.Icerik.Length ?? 0);
            return false;   // gönderim OLMADI — çağıran bunu kullanıcıya doğru söyleyebilsin
        }

        using var ileti = IletiKur(mesaj, gonderen);
        var port = ayarlar.Sayi(AyarTanimlari.EpostaPort);

        try
        {
            using var istemci = new SmtpClient();
            await istemci.ConnectAsync(
                sunucu, port, TlsKipi(port, ayarlar.EvetHayir(AyarTanimlari.EpostaSsl)), cancellationToken);
            var kullanici = ayarlar.Metin(AyarTanimlari.EpostaKullanici);
            if (!string.IsNullOrWhiteSpace(kullanici))
            {
                await istemci.AuthenticateAsync(kullanici, ayarlar.Metin(AyarTanimlari.EpostaParola), cancellationToken);
            }

            await istemci.SendAsync(ileti, cancellationToken);
            await istemci.DisconnectAsync(quit: true, cancellationToken);
            return true;
        }
        catch (Exception hata) when (hata is not OperationCanceledException)
        {
            // IO adaptörü (AGENTS §4.6): teknik SMTP hatasını yöneticinin ANLAYIP ÇÖZEBİLECEĞİ
            // bir mesaja çevir. Sarmalanmazsa bilinmeyen istisna sayılır ve ekrana genel bir
            // "bir hata oluştu" çıkar — sebebi yalnız sunucu günlüğünde kalırdı.
            throw new EpostaGonderimHatasi(HataAciklamasi(hata, sunucu, port), hata);
        }
    }

    /// <summary>SMTP hatasını yöneticiye dönük açıklamaya çevirir: ne oldu + ne yapmalı + sunucunun
    /// ham yanıtı. Ham yanıt korunur çünkü sağlayıcılar sebebi orada söyler
    /// ("Application-specific password required" gibi).</summary>
    public static string HataAciklamasi(Exception hata, string sunucu, int port) => hata switch
    {
        AuthenticationException => "SMTP kullanıcı adı ya da parolası kabul edilmedi. Gmail/Office365 " +
            "gibi sağlayıcılarda normal hesap parolası ÇALIŞMAZ; hesap ayarlarından bir " +
            "\"uygulama parolası\" üretip Sistem Ayarları → E-posta → SMTP parolası alanına yazın. " +
            $"Sunucunun yanıtı: {hata.Message}",

        SslHandshakeException => $"{sunucu}:{port} ile şifreli bağlantı kurulamadı. Port ile TLS kipi " +
            "uyuşmuyor olabilir: 465 örtük TLS ister, 587 ise STARTTLS. Portu ve \"SSL/TLS kullan\" " +
            $"ayarını birlikte gözden geçirin. Ayrıntı: {hata.Message}",

        System.Net.Sockets.SocketException => $"{sunucu}:{port} adresine bağlanılamadı. Sunucu adı ya da " +
            "port yanlış olabilir; ayrıca birçok ağ spam koruması için 25 ve 587'yi kapatıp yalnız " +
            $"465'e izin verir — 465'i deneyin. Ayrıntı: {hata.Message}",

        SmtpCommandException komut => $"SMTP sunucusu isteği reddetti ({komut.ErrorCode}). Gönderen adresi " +
            "kimlik doğrulanan hesapla aynı olmalıdır; alıcı adreslerini de kontrol edin. " +
            $"Sunucunun yanıtı: {hata.Message}",

        SmtpProtocolException => $"{sunucu}:{port} ile SMTP konuşması bozuldu. Genellikle port/TLS " +
            $"eşleşmesi yanlıştır (465 örtük TLS · 587 STARTTLS). Ayrıntı: {hata.Message}",

        _ => $"E-posta gönderilemedi ({sunucu}:{port}). Ayrıntı: {hata.Message}",
    };

    private static MimeMessage IletiKur(EpostaMesaji mesaj, string gonderen)
    {
        var ileti = new MimeMessage();
        ileti.From.Add(MailboxAddress.Parse(gonderen));
        foreach (var alici in mesaj.Alicilar)
        {
            ileti.To.Add(MailboxAddress.Parse(alici));
        }

        ileti.Subject = mesaj.Konu;

        var govde = new BodyBuilder { HtmlBody = mesaj.GovdeHtml };
        if (mesaj.Ek is { } ek)
        {
            govde.Attachments.Add(ek.DosyaAdi, ek.Icerik, ContentType.Parse(ek.IcerikTipi));
        }

        ileti.Body = govde.ToMessageBody();
        return ileti;
    }
}
