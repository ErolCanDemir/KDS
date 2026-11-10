namespace Kds.Logging;

/// <summary>Olay günlüğü (KdsLog) Mongo ayarları. <see cref="ConnectionString"/> boşsa günlük
/// KAPALI (no-op) — dev'de Mongo olmadan çalışır.</summary>
public sealed class EventLogOptions
{
    public string? ConnectionString { get; set; }
    public string Database { get; set; } = "KdsLog";

    /// <summary>Arka plan kanalı kapasitesi; dolarsa en eski olay düşürülür (log gecikmesi isteği
    /// asla bloklamaz/bozmaz).</summary>
    public int Capacity { get; set; } = 10_000;
}
