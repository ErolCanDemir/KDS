using Kds.Contracts.Metrics;

namespace Kds.Execution;

/// <summary>S47 — formül düzenleyicinin "fonksiyonlar" ipucu içeriği. BİLEREK
/// <see cref="IfadeDenetimi"/>'nin yanında yaşar: ipucunda gösterilen her ad bekçinin izinli
/// kümesinde olmalıdır (test kilitler) — liste başka yerde (ör. ön yüzde sabit) tutulsaydı
/// bekçiyle sessizce ayrışır, kullanıcıya "önerip reddetme" yaşatırdık.
///
/// İçerik, izinli kümenin KULLANICIYA ANLAMLI alt kümesidir: CAST tip adları, DATEADD parça
/// adları (YY/MM/DD…) gibi tek başına çağrılamayan kelimeler ipucuna girmez.</summary>
public static class IfadeYardimi
{
    public static readonly IReadOnlyList<MetricFunctionGroup> Gruplar =
    [
        new("Toplama",
        [
            new("SUM", "SUM({Odemeler.Tutar})", "Toplar — metriklerin çoğu budur."),
            new("AVG", "AVG({Odemeler.Tutar})", "Ortalama alır."),
            new("COUNT", "COUNT({Talepler.Id})", "Sayar; tekilleri saymak için COUNT(DISTINCT …)."),
            new("MIN", "MIN({Odemeler.Tutar})", "En küçük değer."),
            new("MAX", "MAX({Odemeler.Tutar})", "En büyük değer."),
        ]),
        new("Sayı",
        [
            new("ROUND", "ROUND(AVG({Odemeler.Tutar}), 2)", "Yuvarlar (basamak sayısıyla)."),
            new("ABS", "ABS({Hareketler.Fark})", "Mutlak değer."),
            new("ISNULL", "ISNULL({Odemeler.Tutar}, 0)", "Boş (NULL) değeri verilenle değiştirir."),
            new("NULLIF", "SUM({Odemeler.Tutar}) / NULLIF(COUNT({Talepler.Id}), 0)", "İki değer eşitse NULL — sıfıra bölünmeyi önler."),
            new("CEILING", "CEILING({Odemeler.Tutar})", "Yukarı yuvarlar."),
            new("FLOOR", "FLOOR({Odemeler.Tutar})", "Aşağı yuvarlar."),
        ]),
        new("Metin",
        [
            new("UPPER", "UPPER({Musteriler.Ad})", "Büyük harfe çevirir."),
            new("LOWER", "LOWER({Musteriler.Ad})", "Küçük harfe çevirir."),
            new("LEN", "LEN({Musteriler.Ad})", "Karakter sayısı."),
            new("CONCAT", "CONCAT({Musteriler.Ad}, ' ', {Musteriler.Soyad})", "Metinleri birleştirir."),
            new("TRIM", "TRIM({Musteriler.Ad})", "Baş/son boşlukları atar."),
        ]),
        new("Tarih",
        [
            new("YEAR", "YEAR({Talepler.GirisTarihi})", "Yıl bileşeni."),
            new("MONTH", "MONTH({Talepler.GirisTarihi})", "Ay bileşeni."),
            new("DATEDIFF", "DATEDIFF(DAY, {Talepler.GirisTarihi}, {Talepler.CikisTarihi})", "İki tarih arası fark (gün/ay/yıl)."),
            new("DATEADD", "DATEADD(MONTH, -1, GETDATE())", "Tarihe süre ekler/çıkarır."),
            new("GETDATE", "GETDATE()", "Şu an (sunucu saati)."),
            new("EOMONTH", "EOMONTH({Talepler.GirisTarihi})", "Ayın son günü."),
        ]),
        new("Mantık",
        [
            new("CASE", "SUM(CASE WHEN {Talepler.Durum} = 'Acik' THEN 1 ELSE 0 END)", "Koşula göre değer — koşullu sayım/toplamın temeli."),
            new("IIF", "IIF({Odemeler.Tutar} > 0, 1, 0)", "Kısa koşul: doğruysa ilk, değilse ikinci değer."),
            new("COALESCE", "COALESCE({A.X}, {A.Y}, 0)", "Soldan ilk dolu (NULL olmayan) değer."),
            new("CAST", "CAST({Odemeler.Tutar} AS DECIMAL(18,2))", "Tip dönüşümü."),
        ]),
    ];
}
