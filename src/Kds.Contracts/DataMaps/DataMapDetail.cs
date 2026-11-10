using Kds.Contracts.Connections;

namespace Kds.Contracts.DataMaps;

/// <summary>Editör için haritanın tam içeriği (tablolar + kolonlar + ilişkiler).</summary>
public sealed record DataMapDetail(
    Guid Id,
    string Ad,
    Guid BaglantiId,
    DataMode Mod,
    IReadOnlyList<DataMapTableDto> Tablolar,
    IReadOnlyList<DataMapRelationshipDto> Iliskiler);
