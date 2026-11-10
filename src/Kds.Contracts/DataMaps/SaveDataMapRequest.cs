using Kds.Contracts.Connections;
using Kds.Common.Cqrs;

namespace Kds.Contracts.DataMaps;

/// <summary>Haritayı kaydeder: <see cref="Id"/> boşsa newEntity oluşturur, doluysa günceller.
/// Tüm içerik (tablolar + kolonlar + ilişkiler) gönderilir; alt varlıklar tümüyle değiştirilir.</summary>
public sealed record SaveDataMapRequest(
    Guid? Id,
    string Ad,
    Guid BaglantiId,
    DataMode Mod,
    IReadOnlyList<DataMapTableDto> Tablolar,
    IReadOnlyList<DataMapRelationshipDto> Iliskiler) : ICommand<DataMapSummary>;
