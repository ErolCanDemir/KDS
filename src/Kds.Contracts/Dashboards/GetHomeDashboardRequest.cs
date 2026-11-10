using Kds.Common.Cqrs;

namespace Kds.Contracts.Dashboards;

/// <summary>Ana sayfada açılacak panonun kimliği. Seçim yoksa — ya da seçilen pano silinmiş/
/// artık görünmüyorsa — <see cref="HomeDashboardInfo.PanoId"/> null döner; ana sayfa o durumda
/// karşılama görünümünü çizer.</summary>
public sealed record GetHomeDashboardRequest : IQuery<HomeDashboardInfo>;
