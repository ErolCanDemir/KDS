using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>S27 — klasöre ROL erişimi ver/güncelle (boş liste = erişimi kaldır). Kesişen roldeki
/// kullanıcı, klasördeki özel olmayan tüm raporları görür; Roles &amp; Yetkiler ekranındaki
/// "Klasör Ekle" ile rapor tarafındaki klasör yönetimi aynı kaydı günceller.</summary>
public sealed record SetFolderRolesRequest(Guid Id, IReadOnlyList<string>? Roller) : ICommand<FolderDto>;
