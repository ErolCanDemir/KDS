using Kds.Contracts.Reports;

namespace Kds.Data.Entities;

/// <summary>Pano (S11 Dashboard): adlandırılmış, SIRALI rapor koleksiyonu. Sahibi + paylaşım
/// rapordaki gibi (Özel → sahibi + admin; Ekip → tenant'taki herkes). Rapor referansları JSON
/// dizisidir (yumuşak referans — silinen rapor panoda sessizce atlanır). Widget yerleşimi/boyutu
/// ayrı JSON'da (<see cref="YerlesimJson"/>) tutulur → yerleşimi olmayan pano varsayılan ızgaraya düşer.</summary>
public class Dashboard : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Ad { get; set; } = string.Empty;

    public ReportSharing Paylasim { get; set; }

    /// <summary>S27 — <see cref="ReportSharing.Roller"/> paylaşımında görebilecek rol adları
    /// (virgülle ayrılmış; rapor tarafıyla aynı desen). Diğer paylaşımlarda null.</summary>
    public string? PaylasimRolleri { get; set; }

    /// <summary>Local <c>User.Id</c> (yumuşak referans). Özel panoyu yalnız sahibi + admin görür.</summary>
    public Guid SahipId { get; set; }

    /// <summary>Sıralı rapor GUID'leri (JSON dizisi). Yumuşak referans.</summary>
    public string RaporlarJson { get; set; } = "[]";

    /// <summary>Widget yerleşimi (react-grid-layout): rapor başına konum/boyut JSON dizisi
    /// (<c>[{raporId,x,y,w,h}]</c>). Null = yerleşim tanımlanmamış → görüntüleyici varsayılan akış
    /// ızgarasına düşer. RaporlarJson'dan AYRI: rapor ekle/çıkar (editör) yerleşimi bozmaz.</summary>
    public string? YerlesimJson { get; set; }
}
