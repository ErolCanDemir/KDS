using Kds.Contracts.Connections;

namespace Kds.Data.Entities;

/// <summary>Bir bağlantıdaki ham tabloları müşteri için kullanılabilir hâle getiren dataMap
/// (00-analiz §4). Report kurmaz — hazırlar: tablolar + kolonlar (dostça ad, rol) + ilişkiler
/// (join). Aggregate kökü; alt varlıklar bunun üzerinden yüklenir.</summary>
public class DataMap : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public Guid BaglantiId { get; set; }

    /// <summary>Harita düzeyi veri alma modu (K11 — bağlantı varsayılanını geçersiz kılar).</summary>
    public DataMode Mod { get; set; }

    public List<DataMapTable> Tablolar { get; set; } = [];
    public List<DataMapRelationship> Iliskiler { get; set; } = [];
}
