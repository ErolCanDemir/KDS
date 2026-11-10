using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Tenant'ın klasörlerini (görünür rapor sayılarıyla) listeler.</summary>
public sealed record ListFoldersRequest : IQuery<IReadOnlyList<FolderDto>>;
