using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Klasörü siler; içindeki raporlar klasörsüze düşer (silinmez).</summary>
public sealed record DeleteFolderRequest(Guid Id) : ICommand<bool>;
