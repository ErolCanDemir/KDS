namespace Kds.Business.Subscriptions;

/// <summary>E-posta gönderim soyutlaması (rapor abonelikleri). Uygulaması Api katmanındadır
/// (<c>EpostaGonderici</c>): SMTP bilgisi HER GÖNDERİMDE Sistem Ayarları'ndan okunur (S16/A3);
/// sunucu ya da gönderen boşsa gerçekten göndermez, yalnız günlüğe yazar. Böylece abonelik boru
/// hattı SMTP olmadan da uçtan uca çalışır ("yarım özellik yok").</summary>
public interface IEpostaGonderici
{
    /// <summary><c>true</c> = ileti gerçekten SMTP sunucusuna teslim edildi. <c>false</c> = SMTP
    /// yapılandırılmadığı için YALNIZ günlüğe yazıldı, gönderim olmadı.
    /// <para>Bu ayrım bilerek dışarı taşınır: ekran "gönderildi" derken hangisinin gerçekleştiğini
    /// VARSAYMAMALI. Varsayıma dayanan mesaj, ayar yapıldığı anda yalan söylemeye başlar — nitekim
    /// 18 Ağu 2026'da öyle oldu: SMTP kurulup posta gerçekten gittiği hâlde ekran "dev'de SMTP yok,
    /// günlüğe yazıldı" demeye devam etti.</para></summary>
    Task<bool> GonderAsync(EpostaMesaji mesaj, CancellationToken cancellationToken);
}

/// <summary>Gönderilecek e-posta: alıcılar + konu + (HTML) gövde + isteğe bağlı tek ek.</summary>
public sealed record EpostaMesaji(IReadOnlyList<string> Alicilar, string Konu, string GovdeHtml, EpostaEki? Ek);

/// <summary>E-posta eki: ham içerik + indirilecek dosya adı + MIME tipi.</summary>
public sealed record EpostaEki(byte[] Icerik, string DosyaAdi, string IcerikTipi);

/// <summary>SMTP gönderimi başarısız oldu; <see cref="Exception.Message"/> yöneticiye NE YAPACAĞINI
/// söyleyecek biçimde yazılır (sunucunun ham yanıtı da eklenir).
/// <para>Neden ayrı bir tip: SMTP hataları kimlik/port/TLS gibi <b>yapılandırma</b> sorunlarıdır ve
/// çözümü yöneticinin elindedir. Sarmalanmasaydı bilinmeyen istisna sayılıp ekrana "An error
/// occurred while processing your request." diye çıkardı — nitekim 18 Ağu 2026'da öyle oldu:
/// sunucu "uygulama parolası gerekli" diyordu, kullanıcı bunu göremiyordu.</para></summary>
public sealed class EpostaGonderimHatasi(string mesaj, Exception ic) : Exception(mesaj, ic);
