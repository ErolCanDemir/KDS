using Kds.Contracts.Connections;

namespace Kds.Contracts.DataMaps;

/// <summary>Liste ekranı için dataMap özeti.</summary>
public sealed record DataMapSummary(
    Guid Id,
    string Ad,
    Guid BaglantiId,
    string? BaglantiAd,
    DataMode Mod,
    int TabloSayisi,
    int IliskiSayisi);
