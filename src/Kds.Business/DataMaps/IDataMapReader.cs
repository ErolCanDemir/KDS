using Kds.Contracts.DataMaps;

namespace Kds.Business.DataMaps;

/// <summary>Harita okuma servisi. Başka handler'ların (SQL üretimi/çalıştırma) dataMap içeriğine
/// erişimi BU servisten geçer — handler içinden handler Send'lemek (gizli bağımlılık + pipeline'ın
/// ikinci kez koşması) istenmez; tek istisna bilinçli "tek kapı" olan RunQueryRequest'dur.
/// Tenant izolasyonu DbContext global query filter'ından gelir.</summary>
public interface IDataMapReader
{
    /// <summary>Haritayı tam içeriğiyle getirir; yoksa/başka tenant'ınsa null.</summary>
    Task<DataMapDetail?> GetDetailAsync(Guid haritaId, CancellationToken cancellationToken);
}
