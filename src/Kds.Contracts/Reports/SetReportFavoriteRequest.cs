using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Mevcut kullanıcının yıldızını ekler/kaldırır. Report görünür değilse false.</summary>
public sealed record SetReportFavoriteRequest(Guid Id, bool Favori) : ICommand<bool>;
