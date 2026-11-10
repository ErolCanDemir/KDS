namespace Kds.Business.Connections;

/// <summary>Bağlantı parolalarını şifreler/çözer (00-analiz §7). Uygulaması Api katmanındadır
/// (Data Protection sağlayıcısı orada barınır); Business yalnız bu arayüzü bilir.</summary>
public interface IPasswordProtector
{
    /// <summary>Düz metni şifreler (boşsa boş döner).</summary>
    string Protect(string duzMetin);

    /// <summary>Şifreli metni çözer (boşsa boş döner).</summary>
    string Unprotect(string sifreli);
}
