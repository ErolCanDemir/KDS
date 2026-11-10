using Kds.Contracts.Queries;

namespace Kds.Export;

/// <summary>Bir sorgu sonucunu (kolon + satır) seçilen biçime (CSV/Excel/PDF) çevirir. Durumsuz;
/// gerçek veri erişimi yok — yalnız biçimlendirme (S13 export).</summary>
public interface IResultExporter
{
    ExportedFile DisaAktar(QueryRunResult sonuc, ExportFormat bicim, string baslik);
}
