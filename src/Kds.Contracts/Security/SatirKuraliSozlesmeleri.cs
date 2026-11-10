using Kds.Common.Cqrs;
using Kds.Contracts.QueryModel;

namespace Kds.Contracts.Security;

/// <summary>Satır-düzeyi güvenlik (RLS) kuralının yönetim görünümü. Kural, bir veri haritası
/// TABLOSUNU kısıtlar: sorguda o tablo kullanılıyorsa devreye girer.</summary>
public sealed record SatirKuraliDto(
    Guid Id,
    Guid VeriHaritasiId,
    string VeriHaritasiAdi,
    Guid HaritaTabloId,
    string TabloAdi,
    string Ad,
    string Kolon,
    KarsilastirmaOp Op,
    KuralDegerKaynagi DegerKaynagi,
    string? Deger,
    string? Roller,
    bool Aktif);

/// <summary>Bir haritanın kuralları (harita verilmezse kiracının tüm kuralları).</summary>
public sealed record ListSatirKurallariRequest(Guid? VeriHaritasiId = null)
    : IQuery<IReadOnlyList<SatirKuraliDto>>;

/// <summary><see cref="Id"/> boşsa yeni kural, doluysa günceller. Yalnız admin.</summary>
public sealed record SaveSatirKuraliRequest(
    Guid? Id,
    Guid VeriHaritasiId,
    Guid HaritaTabloId,
    string Ad,
    string Kolon,
    KarsilastirmaOp Op,
    KuralDegerKaynagi DegerKaynagi,
    string? Deger,
    string? Roller,
    bool Aktif) : ICommand<SatirKuraliDto>;

public sealed record DeleteSatirKuraliRequest(Guid Id) : ICommand<bool>;
