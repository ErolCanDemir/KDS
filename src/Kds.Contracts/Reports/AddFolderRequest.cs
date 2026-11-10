using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Yeni klasör açar (düz liste; aynı ada izin verilmez).</summary>
public sealed record AddFolderRequest(string Ad) : ICommand<FolderDto>;
