using Kds.Common.Cqrs;

namespace Kds.Contracts.Dashboards;

/// <summary>Kullanıcının ana sayfasında açılacak panoyu belirler. <paramref name="PanoId"/> null →
/// seçim kaldırılır (ana sayfa karşılama görünümüne döner). Pano görünür değilse false.</summary>
public sealed record SetHomeDashboardRequest(Guid? PanoId) : ICommand<bool>;
