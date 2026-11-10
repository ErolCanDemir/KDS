using Kds.Common.Cqrs;

namespace Kds.Contracts.Dashboards;

/// <summary>Mevcut kullanıcının GÖREBİLDİĞİ panoları listeler (Ekip + kendi Özel'leri; admin hepsini).</summary>
public sealed record ListDashboardsRequest : IQuery<IReadOnlyList<DashboardSummary>>;
