using Kds.Common.Cqrs;

namespace Kds.Contracts.Metrics;

/// <summary>Gösterim biçimi — ekran değeri buna göre biçimler (₺ / sayı / %).</summary>
public enum MetrikBicimi { Sayi = 0, Para, Yuzde }

public sealed record MetricSummary(
    Guid Id,
    string Ad,
    Guid VeriHaritasiId,
    string? HaritaAd,
    string Ifade,
    MetrikBicimi Bicim,
    string? Aciklama,
    string? SahipAd,
    bool Aktif,
    /// <summary>Kaç kayıtlı rapor bu metriği kullanıyor — silme onayı ve katalog bunun üstünden
    /// konuşur ("3 raporda kullanılıyor").</summary>
    int KullananRaporSayisi);

public sealed record ListMetricsRequest : IQuery<IReadOnlyList<MetricSummary>>;

public sealed record GetMetricRequest(Guid Id) : IQuery<MetricSummary>;

public sealed record SaveMetricRequest(
    Guid? Id,
    string Ad,
    Guid VeriHaritasiId,
    string Ifade,
    MetrikBicimi Bicim,
    string? Aciklama,
    bool Aktif = true) : ICommand<MetricSummary>;

public sealed record DeleteMetricRequest(Guid Id) : ICommand<bool>;

/// <summary>Metriği kullanan raporların adları — silme onayında "şu raporlar kullanıyor" uyarısı.</summary>
public sealed record GetMetricUsageRequest(Guid Id) : IQuery<IReadOnlyList<string>>;

/// <summary>Metriğin GÜNCEL değerini ölçer: kırılımsız tek satırlık sorgu. Otomatik ölçüm yoktur —
/// ekranı açan herkes için tüm metrikleri koşturmak büyük veritabanında pahalıdır; isteyen tıklar.</summary>
public sealed record MeasureMetricRequest(Guid Id) : ICommand<MetricMeasureResult>;

public sealed record MetricMeasureResult(decimal? Deger, MetrikBicimi Bicim, int SureMs);

// ——— S30 · Sürüm geçmişi ———

/// <summary>Bir metrik sürümü = tanımın ESKİ bir hâli (güncel hâl metriğin kendisidir).
/// <paramref name="DegistirenAd"/>: bu hâli tarihe gömen güncellemeyi yapan kişi.</summary>
public sealed record MetricVersionSummary(
    int SurumNo,
    string Ad,
    string Ifade,
    MetrikBicimi Bicim,
    string? Aciklama,
    string? DegistirenAd,
    DateTimeOffset Tarih);

public sealed record ListMetricVersionsRequest(Guid MetrikId) : IQuery<IReadOnlyList<MetricVersionSummary>>;

/// <summary>Seçili sürümün tanımını metriğe geri yükler. Normal kaydetme yolundan geçer: formül
/// yeniden doğrulanır ve dönüşün kendisi de bir sürüm üretir — geçmiş hiç silinmez.</summary>
public sealed record RestoreMetricVersionRequest(Guid MetrikId, int SurumNo) : ICommand<MetricSummary>;

// ——— S30 · Dışa / içe aktarma ———

/// <summary>Taşınabilir metrik tanımı: harita ADI ile — GUID'ler kiracıya özeldir, ad ise hedef
/// ortamda (test → canlı) aynı kurulabilir. İçe alım haritayı adından çözer.</summary>
public sealed record MetricExportItem(
    string HaritaAd,
    string Ad,
    string Ifade,
    MetrikBicimi Bicim,
    string? Aciklama);

/// <summary>Kataloğu (ya da tek haritanın metriklerini) taşınabilir biçimde verir.</summary>
public sealed record ExportMetricsRequest(Guid? VeriHaritasiId = null) : IQuery<IReadOnlyList<MetricExportItem>>;

public enum MetrikIceAlmaSonucu { Eklendi = 0, Guncellendi, Atlandi, Hata }

public sealed record MetricImportItemResult(string Ad, MetrikIceAlmaSonucu Sonuc, string? Mesaj);

/// <summary>Dışa aktarılmış metrikleri içe alır. Ad çakışmasında strateji:
/// <paramref name="CakismadaGuncelle"/> true → mevcut tanım güncellenir (sürüm geçmişine yazılır),
/// false → atlanır. Her öğe kendi başına değerlendirilir; biri düşerse diğerleri etkilenmez.</summary>
public sealed record ImportMetricsRequest(
    IReadOnlyList<MetricExportItem> Ogeler,
    bool CakismadaGuncelle = false) : ICommand<IReadOnlyList<MetricImportItemResult>>;

// ——— S46 · OSI (Apache Ossie) uyumu ———

/// <summary>Semantik katmanı (harita + metrikler) OSI — Open Semantic Interchange / Apache
/// Ossie — YAML biçiminde verir: satıcıdan bağımsız değişim standardı ("kapalı ada" riskinden
/// kaçınma, rakip analizi §5). null = TÜM haritalar (her biri ayrı <c>semantic_model</c> öğesi).</summary>
public sealed record ExportOsiMetricsRequest(Guid? VeriHaritasiId = null) : IQuery<string>;

/// <summary>OSI YAML dosyasından metrikleri içe alır. Sonuç sözleşmesi S30 içe alımıyla AYNI
/// (ekran aynı sonuç diyaloğunu kullanır); her öğe kendi başına değerlendirilir.</summary>
public sealed record ImportOsiMetricsRequest(
    string Yaml,
    bool CakismadaGuncelle = false) : ICommand<IReadOnlyList<MetricImportItemResult>>;

// ——— S47 · Formül yardımı ———

/// <summary>Formül düzenleyicinin "fonksiyonlar" ipucunda gösterilen tek kayıt.</summary>
public sealed record MetricFunctionInfo(string Ad, string Ornek, string Aciklama);

/// <summary>Fonksiyon ipucunun bir grubu (Toplama / Sayı / Metin / Tarih / Mantık).</summary>
public sealed record MetricFunctionGroup(string Grup, IReadOnlyList<MetricFunctionInfo> Fonksiyonlar);

/// <summary>Formül düzenleyicide gösterilecek fonksiyon yardımı. Kaynak, ifade BEKÇİSİNİN
/// (IfadeDenetimi) yanındaki liste — UI ile bekçi ayrışamaz (testle kilitli).</summary>
public sealed record ListMetricFunctionsRequest : IQuery<IReadOnlyList<MetricFunctionGroup>>;
