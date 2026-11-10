namespace Kds.Api.Kurulum;

/// <summary>Kurulum sihirbazının (A2) istek/yanıt sözleşmeleri. Parolalar yalnız İÇERİ girer;
/// hiçbir yanıt parola döndürmez.</summary>
public sealed record KurulumDurumYanit(bool Yapilandirildi, int Surum, string? AnahtarDosyasi);

/// <summary>Veritabanı adımının girdisi. <see cref="KullaniciAdi"/> boş = entegre (Windows) kimlik.</summary>
public sealed record VeritabaniIstek(
    string Sunucu,
    string Veritabani,
    string? KullaniciAdi,
    string? Sifre);

/// <summary>Bağlantı testinin sonucu — sihirbaz bunu okunur biçimde gösterir.</summary>
public sealed record VeritabaniTestYanit(
    bool Basarili,
    string Mesaj,
    string? SunucuSurumu = null,
    bool VeritabaniVar = false,
    bool OlusturabilirMi = false,
    bool DdlYetkisi = false);

/// <summary>Kimlik sağlayıcı (Keycloak) erişilebilirlik testi.</summary>
public sealed record KimlikIstek(string Yetkili, string Istemci, string? MetadataAdresi, bool HttpsZorunlu = true);

public sealed record KimlikTestYanit(bool Basarili, string Mesaj, string? Yayinci = null);

/// <summary>Kurulumu tamamlar: (gerekirse) veritabanını yaratır, şemayı kurar, ilk kiracıyı
/// ekler, yapılandırmayı şifreli dosyaya yazar. <see cref="KiraciId"/> boşsa yeni üretilir ve
/// yanıtta döner — Keycloak'taki <c>tenant_id</c> talebi bununla eşleşmelidir.</summary>
public sealed record TamamlaIstek(
    VeritabaniIstek Veritabani,
    KimlikIstek? Kimlik,
    string KiraciAdi,
    Guid? KiraciId,
    string? OnyuzAdres,
    string? DepoSunucusu,
    string? LogBaglantisi);

public sealed record TamamlaYanit(bool Basarili, string Mesaj, Guid KiraciId);
