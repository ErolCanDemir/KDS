namespace Kds.Contracts.Queries;

/// <summary>Bir SELECT yürütmesinin sonucu: kolon meta + satırlar (kolon sırasıyla hizalı) +
/// satır sınırının kesip kesmediği + süre.</summary>
/// <param name="AraTablolar">S40 — kullanıcının seçmediği hâlde, tabloları birleştirmek için yola
/// giren tabloların adları. Bir tabloya birden çok eşit uzunlukta yol varsa üretici birini seçer;
/// sorgu hatasız çalışır ama YANLIŞ ŞEYİ ölçebilir (ölçülen vaka: "AUDİ marka araç" sorusunda
/// Markalar'a Araclar yerine Servisler üzerinden bağlanıldı — aracın markası değil servisin bayi
/// markası süzüldü). Boş/null = ara tablo yok, yani yol tekti ve söylenecek bir şey de yok.</param>
public sealed record QueryRunResult(
    IReadOnlyList<QueryColumnDto> Kolonlar,
    IReadOnlyList<IReadOnlyList<object?>> Satirlar,
    bool SatirSiniriUygulandi,
    int SureMs,
    IReadOnlyList<string>? AraTablolar = null);
