using System.Text.Json;
using Kds.Contracts.Ingest;

namespace Kds.Business.Ingest;

/// <summary>İçe alım işi seçili-tablolar JSON dönüşümü (dar-tipli parse — AGENTS §4.6 izinli bölge).</summary>
internal static class IngestCommon
{
    public static IReadOnlyList<IngestTableDto> Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<List<IngestTableDto>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string Serialize(IEnumerable<IngestTableDto> tablolar) => JsonSerializer.Serialize(tablolar);

    /// <summary>S44 — artımlı su seviyeleri ("sema.tablo" → son değer). Bozuk JSON boş sözlük döner:
    /// etkisi en kötü ihtimalle gereksiz bir TAM yüklemedir, veri kaybı değil.</summary>
    public static Dictionary<string, string> DeserializeSuSeviyeleri(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string? SerializeSuSeviyeleri(IReadOnlyDictionary<string, string>? seviyeler) =>
        seviyeler is { Count: > 0 } ? JsonSerializer.Serialize(seviyeler) : null;
}
