using Kds.Contracts.DataMaps;

namespace Kds.Data.Entities;

/// <summary>Bir dataMap tablosunun kolonu: ham ad → dostça ad + rol (boyut/ölçü/anahtar/gizli).</summary>
public class DataMapColumn : EntityBase
{
    public Guid HaritaTabloId { get; set; }
    public string HamAd { get; set; } = string.Empty;
    public string? DostcaAd { get; set; }
    public ColumnRole Rol { get; set; }
    public string? VeriTipi { get; set; }
}
