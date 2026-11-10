namespace Kds.Contracts.SystemStatus;

/// <summary>Sistem durumu ucu yanıtı (sürüm + sunucu zamanı).</summary>
public sealed record SystemStatusResponse(string Surum, DateTimeOffset SunucuZamani);
