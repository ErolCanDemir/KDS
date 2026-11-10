using System.Text.Json;
using Kds.Contracts.Dashboards;

namespace Kds.Business.Dashboards;

/// <summary>Pano rapor-listesi + widget yerleşimi JSON dönüşümü (dar-tipli parse — AGENTS §4.6 izinli
/// bölge). Elle bozulmuş DB değeri panoyu çökertmemeli → boş dön.</summary>
internal static class DashboardCommon
{
    public static IReadOnlyList<Guid> DeserializeReports(string json)
    {
        try { return JsonSerializer.Deserialize<List<Guid>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string SerializeReports(IEnumerable<Guid> raporlar) => JsonSerializer.Serialize(raporlar);

    public static IReadOnlyList<DashboardWidget> DeserializeLayout(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<DashboardWidget>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string SerializeLayout(IEnumerable<DashboardWidget> yerlesim) => JsonSerializer.Serialize(yerlesim);
}
