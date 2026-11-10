namespace Kds.Contracts.Reports;

/// <summary>Klasör + içindeki (görünür) rapor sayısı.</summary>
public sealed record FolderDto(Guid Id, string Ad, int RaporSayisi,
    /// <summary>S27 — klasöre rol erişimi (null = yok). Kesişen rol, klasördeki özel olmayan raporları görür.</summary>
    IReadOnlyList<string>? ErisimRolleri = null);
