namespace Kds.Contracts.DataMaps;

/// <summary>Haritadaki bir tablo. <see cref="Id"/> istemci tarafından üretilir; ilişkiler bu
/// Id'lere referans verir. Kaydetmede aynen korunur.</summary>
public sealed record DataMapTableDto(
    Guid Id,
    string Sema,
    string HamAd,
    string DostcaAd,
    IReadOnlyList<DataMapColumnDto> Kolonlar);
