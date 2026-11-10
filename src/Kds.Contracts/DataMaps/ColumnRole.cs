namespace Kds.Contracts.DataMaps;

/// <summary>Bir kolonun veri haritasındaki rolü (S6). Son kullanıcı yalnız görünür kolonları
/// kullanır: <see cref="Boyut"/> ile gruplar/filtreler, <see cref="Olcu"/>'yü toplar,
/// <see cref="Anahtar"/> join'de kullanılır; <see cref="Gizli"/> son kullanıcıya kapalıdır.</summary>
public enum ColumnRole
{
    Boyut = 0,
    Olcu = 1,
    Anahtar = 2,
    Gizli = 3,
}
