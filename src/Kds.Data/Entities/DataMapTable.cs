namespace Kds.Data.Entities;

/// <summary>Haritaya eklenen bir kaynak tablo: ham ad (FKTHR_MSTR) → dostça ad (Fatura).
/// Kolonları alt varlıktır. Yalnız aggregate kökü (<see cref="DataMap"/>) üzerinden erişilir.</summary>
public class DataMapTable : EntityBase
{
    public Guid VeriHaritasiId { get; set; }
    public string Sema { get; set; } = "dbo";
    public string HamAd { get; set; } = string.Empty;
    public string DostcaAd { get; set; } = string.Empty;

    public List<DataMapColumn> Kolonlar { get; set; } = [];
}
