using Kds.Common.Cqrs;
using Kds.Contracts.Queries;

namespace Kds.Contracts.Embedding;

/// <summary>S42 — Yönetim → Gömme &amp; Marka. Dış siteye gömülen rapor/pano için imzalı anahtar.
///
/// <para><b>Güvenlik modeli (kullanıcı onaylı):</b> gömülü koşum sabit servis hesabıyla değil,
/// anahtarın taşıdığı KİMLİKLE yapılır; satır güvenliği (RLS) aynen uygulanır. Sızan bir anahtar
/// yalnız o kimliğin göreceği satırları açar.</para></summary>
/// <param name="KosanKeycloakId">Gömülü koşumun kimin adına yapılacağı (kimlik sağlayıcıdaki
/// <c>sub</c>). Kimliğin ANLIK GÖRÜNTÜSÜ anahtar kurulurken dondurulur (abonelik/uyarı deseni).</param>
/// <param name="IzinliAlanlar">Gömmeye izin verilen alan adları. BOŞ BIRAKILAMAZ — güvenli
/// varsayılan "kapalı"dır; "her yerden gömülebilir" diye bir seçenek bilerek yoktur.</param>
/// <param name="Roller">Anahtarın taşıyacağı roller — <b>RLS'in çalışması için zorunlu</b>.
/// Satır kuralları "hangi role uygulanır" diye bakar: rol taşımayan bir anahtar, role bağlı
/// kuralları ATLAR ve beklenenden geniş veri görür. Admin rolü verilemez (admin RLS'ten muaftır,
/// yani gömülü koşum bütün kuralları devre dışı bırakırdı).</param>
public sealed record CreateEmbedTokenRequest(
    string Ad,
    Guid? RaporId,
    Guid? PanoId,
    Guid KosanKeycloakId,
    IReadOnlyList<string> Roller,
    IReadOnlyList<string> IzinliAlanlar,
    int GecerlilikGun) : ICommand<CreateEmbedTokenResult>;

/// <param name="Anahtar">İmzalı anahtar — <b>yalnız burada, bir kez</b> döner. Listede bir daha
/// gösterilmez; kaybedilirse yenisi üretilir (parola deseni). Saklamıyoruz çünkü saklanan bir sır,
/// sızabilecek ikinci bir kopyadır.</param>
/// <param name="GomKodu">Kopyalanabilir iframe parçacığı — kullanıcı elle kurmasın.</param>
public sealed record CreateEmbedTokenResult(Guid Id, string Anahtar, string GomKodu);

public sealed record ListEmbedTokensRequest : IQuery<IReadOnlyList<EmbedTokenDto>>;

/// <param name="Durum">gecerli | suresi-dolmus | iptal — ekran rozeti bunu basar.</param>
public sealed record EmbedTokenDto(
    Guid Id,
    string Ad,
    string HedefTur,
    string? HedefAd,
    string? KosanKullanici,
    IReadOnlyList<string> IzinliAlanlar,
    DateTimeOffset GecerlilikBitis,
    DateTimeOffset? SonKullanim,
    string Durum);

/// <summary>Anahtarı İPTAL eder (silmez): sızıntı senaryosunda anında kapatmak için. Kayıt kalır ki
/// "hangi anahtar ne zaman iptal edildi" denetlenebilsin.</summary>
public sealed record RevokeEmbedTokenRequest(Guid Id) : ICommand<bool>;

/// <summary>Gömülü koşum: anahtar doğrulanır, kimliği yüklenir, hedef içerik ONUN yetkisiyle koşar.
/// Oturum YOKTUR — istek dış siteden gelir.</summary>
/// <param name="Anahtar">İmzalı anahtar dizesi.</param>
/// <param name="Kaynak">İsteği yapan sayfanın kaynağı (Origin/Referer). İzinli alan listesiyle
/// karşılaştırılır — anahtar doğru olsa bile başka bir siteden çalışmaz.</param>
public sealed record RunEmbeddedRequest(string Anahtar, string? Kaynak) : ICommand<EmbeddedResult>;

/// <param name="Marka">Gömülü görünümün beyaz-etiket kimliği (S35'teki kuruluş markası).</param>
public sealed record EmbeddedResult(
    string Baslik,
    string HedefTur,
    string? Gorsellestirme,
    QueryRunResult Sonuc,
    EmbedMarka Marka);

public sealed record EmbedMarka(string Ad, string? MarkaRengi, string? LogoDataUri);
