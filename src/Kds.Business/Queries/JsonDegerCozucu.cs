using System.Text.Json;

namespace Kds.Business.Queries;

/// <summary>System.Text.Json'un <c>object?</c> için ürettiği <see cref="JsonElement"/>'i ADO.NET'in
/// bağlayabileceği CLR değerine çevirir (string/sayı/bool/null). Model yolu (üretici parametreleri)
/// ile ham SQL yolu (M11: editör @parametreleri wire'dan JsonElement gelir) paylaşır.</summary>
internal static class JsonDegerCozucu
{
    public static object? Coz(object? deger) => deger switch
    {
        JsonElement e => e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            // Y2: tam sayı değilse decimal (para hassasiyeti); decimal'e sığmayan uçlar için double.
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l
                : e.TryGetDecimal(out var m) ? m : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => e.ToString(),
        },
        _ => deger,
    };
}
