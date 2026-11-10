using Kds.Common.Cqrs;

namespace Kds.Contracts.Usage;

/// <summary>S41 — Yönetim → Kullanım. "Neyi kullanıyoruz, ne yavaş?" sorusunu DENETİM KAYITLARINDAN
/// cevaplar; yeni bir ölçüm altyapısı kurulmaz (S5'te zaten her koşum yazılıyor).
///
/// <para><b>Kapsam sınırı (dürüst olmak gerekirse):</b> denetim kaydı SORGU KOŞUMUdur; "pano
/// açıldı" diye bir olay yoktur. Bu yüzden pano kullanımı DOLAYLI ölçülür — bir panonun kartları
/// hiç koşmuyorsa pano da ölüdür. Doğrudan pano ölçümü ayrı bir iş.</para></summary>
/// <param name="GunSayisi">Bakılacak dönem (1–365). Varsayılan 30.</param>
public sealed record GetUsageRequest(int GunSayisi = 30) : IQuery<UsageResult>;

public sealed record UsageResult(
    int GunSayisi,
    UsageOzet Ozet,
    IReadOnlyList<YavasKosum> YavasKosumlar,
    IReadOnlyList<RaporKullanimi> RaporKullanimlari,
    /// <summary>Dönem içinde HİÇ çalışmamış raporlar — "silinebilir mi?" sorusunun muhatabı.</summary>
    IReadOnlyList<OluRapor> OluRaporlar);

/// <param name="YavasEsikMs">Sistem Ayarları'ndaki "yavaş sorgu" eşiği — ekranda tekrar
/// tanımlanmaz, oradan OKUNUR (aynı eşik "Sorun ne?" düğmesini de açar).</param>
public sealed record UsageOzet(
    int ToplamKosum,
    int BasarisizKosum,
    int YavasKosum,
    int YavasEsikMs,
    int OrtalamaSureMs,
    int EnUzunSureMs,
    int AktifKullanici);

public sealed record YavasKosum(
    Guid Id,
    DateTimeOffset Zaman,
    int SureMs,
    int? SatirSayisi,
    string? RaporAd,
    string? Kullanici,
    /// <summary>SQL'in ilk satırları — tam metin Denetim ekranındadır.</summary>
    string SqlOnizleme);

public sealed record RaporKullanimi(
    Guid RaporId,
    string Ad,
    int KosumSayisi,
    int OrtalamaSureMs,
    DateTimeOffset SonKosum,
    int BasarisizSayisi);

public sealed record OluRapor(Guid Id, string Ad, DateTimeOffset OlusturmaTarihi);
