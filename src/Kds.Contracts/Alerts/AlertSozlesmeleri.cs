using Kds.Common.Cqrs;

namespace Kds.Contracts.Alerts;

/// <summary>Satırları tek değere indirgeme biçimi. <see cref="SatirSayisi"/> kolona bakmaz —
/// "hatalı kayıt VARSA haber ver" demenin doğal yolu budur ve pratikte en çok kullanılan biçimdir.</summary>
public enum OzetTuru { SatirSayisi = 0, Toplam, Ortalama, EnBuyuk, EnKucuk, IlkSatir }

/// <summary>Eşik karşılaştırması.</summary>
public enum UyariOp { Buyuk = 0, BuyukEsit, Kucuk, KucukEsit, Esit, EsitDegil }

/// <summary>Uyarının o anki hâli. <see cref="Tetiklendi"/>/<see cref="Sakin"/> ayrımı bildirimin
/// YALNIZ geçişte gitmesini sağlar; <see cref="Hata"/> koşumun kendisi düştüğünde yazılır.</summary>
public enum UyariDurumu { HicKosmadi = 0, Sakin, Tetiklendi, Hata }

public sealed record AlertSummary(
    Guid Id,
    string Ad,
    Guid? RaporId,
    string? RaporAd,
    string? Kolon,
    OzetTuru Ozet,
    UyariOp Op,
    decimal Esik,
    int AliciSayisi,
    string? Zamanlama,
    bool Aktif,
    bool SurekliBildir,
    UyariDurumu Durum,
    decimal? SonDeger,
    DateTimeOffset? SonKontrolTarihi,
    DateTimeOffset? SonBildirimTarihi,
    string? SonHata,
    bool BenimMi,
    Guid? MetrikId = null,
    string? MetrikAd = null);

public sealed record AlertDetail(
    Guid Id,
    string Ad,
    Guid? RaporId,
    string? Kolon,
    OzetTuru Ozet,
    UyariOp Op,
    decimal Esik,
    IReadOnlyList<string> Alicilar,
    string? Zamanlama,
    bool Aktif,
    bool SurekliBildir,
    IReadOnlyDictionary<string, object?>? ParametreDegerleri,
    Guid? MetrikId = null);

/// <summary>Bir uyarı denemesinin sonucu. <see cref="BildirimGonderildi"/> false ise ya durum
/// değişmedi (susulması gerekiyordu) ya da SMTP yapılandırılmamış — ekran bunu VARSAYMAZ, buradan
/// okur (18 Ağu 2026 dersi: sabit metin, ayar değişince yalan söylemeye başlıyor).</summary>
public sealed record AlertRunResult(
    decimal? Deger,
    bool Tetiklendi,
    bool DurumDegisti,
    bool BildirimGonderildi,
    int AliciSayisi,
    long SatirSayisi,
    int SaniyeSure);

public sealed record ListAlertsRequest : IQuery<IReadOnlyList<AlertSummary>>;

public sealed record GetAlertRequest(Guid Id) : IQuery<AlertDetail>;

/// <summary>Uyarı kaydı. S30'dan beri kaynak İKİLİDİR: <paramref name="RaporId"/> ya da
/// <paramref name="MetrikId"/> — tam biri dolu olmalı (doğrulama zorlar). Metrik uyarısında
/// Kolon/Ozet anlamsızdır ve yok sayılır: metrik zaten tek değere iner.</summary>
public sealed record SaveAlertRequest(
    Guid? Id,
    string Ad,
    Guid? RaporId,
    string? Kolon,
    OzetTuru Ozet,
    UyariOp Op,
    decimal Esik,
    IReadOnlyList<string> Alicilar,
    string? Zamanlama,
    bool Aktif,
    bool SurekliBildir,
    IReadOnlyDictionary<string, object?>? ParametreDegerleri = null,
    Guid? MetrikId = null) : ICommand<AlertSummary>;

public sealed record DeleteAlertRequest(Guid Id) : ICommand<bool>;

/// <summary>Uyarıyı ŞİMDİ değerlendirir. <paramref name="BildirimGonder"/> false (ekrandaki
/// "Şimdi Dene") → ölçer, durumu yazar ama POSTA ATMAZ: deneme yaparken alıcılara e-posta gitmesi
/// özelliği kullanılmaz hâle getirir. Zamanlayıcı true ile çağırır.</summary>
public sealed record RunAlertRequest(Guid Id, bool BildirimGonder = true) : ICommand<AlertRunResult>;
