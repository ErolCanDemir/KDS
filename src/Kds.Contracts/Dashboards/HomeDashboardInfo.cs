namespace Kds.Contracts.Dashboards;

/// <summary>Ana sayfa panosu seçimi. <paramref name="PanoId"/> null = seçim yok (ya da seçilen pano
/// silinmiş/artık görünmüyor) → ana sayfa karşılama görünümünü çizer.
/// Çıplak <c>Guid?</c> yerine SARMALAYICI kayıt: null dönerken ASP.NET gövdesiz 204 üretiyordu ve
/// ön yüzdeki JSON ayrıştırıcı boş gövdede patlıyordu. Kayıt her hâlde gövde garantiler.</summary>
public sealed record HomeDashboardInfo(Guid? PanoId);
