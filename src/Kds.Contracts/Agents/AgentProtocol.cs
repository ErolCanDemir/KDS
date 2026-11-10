using System.Text.Json;
using Kds.Common.Cqrs;

namespace Kds.Contracts.Agents;

/* S45 — AJAN PROTOKOLÜ. Bütün istekler müşteri ağındaki ajandan DIŞARI DOĞRU gelir ve imzalı ajan
 * anahtarını taşır (oturum yok; Gömme'deki desen). Akış:
 *   isler → (her iş için, her tablo için) tablo-basla → satirlar* → tablo-bitir → is-bitir
 * Sunucu istekler arasında bellek durumu TUTMAZ: her istek gerekli bağlamı (şema/kolonlar/mod)
 * kendisi taşır — API yeniden başlasa bile yarım iş "kayıp oturum"a dönüşmez, yalnız o koşu hata
 * alır ve bir sonraki vadede baştan koşar. */

/// <summary>Kaynak kolonunun taşınabilir tanımı (ajan → sunucu; DDL bundan üretilir).</summary>
public sealed record AjanKolonDto(
    string Ad, string Tip, int? Boyut, byte? Hassasiyet, byte? Olcek, bool NullOlabilir);

/// <summary>Ajanın koşacağı işler: vadesi gelmiş (cron) ya da elle tetiklenmiş olanlar.
/// Yoklamanın kendisi "son görülme"yi de günceller (ayrı kalp atışı ucu gerekmez).</summary>
public sealed record AjanIsleriRequest(string Anahtar) : ICommand<AjanIsleriResult>;

public sealed record AjanIsleriResult(string AjanAd, IReadOnlyList<AjanIsDto> Isler);

public sealed record AjanIsDto(
    Guid IsId,
    string IsAd,
    /// <summary>Ajanın YEREL yapılandırmasındaki bağlantı dizesinin anahtarı — gerçek dize
    /// müşteri ağından hiç çıkmaz, sunucu yalnız kimliği söyler.</summary>
    Guid BaglantiId,
    IReadOnlyList<AjanTabloDto> Tablolar);

public sealed record AjanTabloDto(
    string Sema,
    string Tablo,
    IReadOnlyList<string>? Kolonlar,
    string? ArtimliKolon,
    /// <summary>S44 su seviyesi (varsa) — ajan deltayı bundan başlatır.</summary>
    string? SuSeviyesi);

/// <summary>Tablo yüklemesini açar: sunucu hedefi hazırlar (Tam → DROP+CREATE; Artimli → staging).
/// Sunucu artımlıyı TAM'a düşürebilir (hedef tablo yok / kolonlar kaymış) — ajan, dönen moda uyar.</summary>
public sealed record AjanTabloBaslaRequest(
    string Anahtar, Guid IsId, string Sema, string Tablo,
    /// <summary>"Tam" | "Artimli"</summary>
    string Mod,
    IReadOnlyList<AjanKolonDto> Kolonlar) : ICommand<AjanTabloBaslaResult>;

public sealed record AjanTabloBaslaResult(string Mod);

/// <summary>Bir satır partisi (ajan 10k'lık partiler yollar). Değer kodlaması
/// <c>AjanVeri</c>'de tek yerde: binary=base64, decimal=invariant metin, tarih=ISO round-trip.</summary>
public sealed record AjanSatirlarRequest(
    string Anahtar, Guid IsId, string Sema, string Tablo,
    /// <summary>"Dogrudan" (tam yükleme hedefi) | "Staging" (artımlı delta).</summary>
    string Hedef,
    IReadOnlyList<AjanKolonDto> Kolonlar,
    IReadOnlyList<IReadOnlyList<JsonElement>> Satirlar) : ICommand<bool>;

/// <summary>Tabloyu kapatır: Artimli → staging PK üzerinden MERGE edilir; Tam → indeksler kurulur.
/// Su seviyesi TABLO BAŞINA burada yazılır — iş ortasında kesilse bile biten tabloların ilerlemesi
/// korunur (verisi gerçekten depoda).</summary>
public sealed record AjanTabloBitirRequest(
    string Anahtar, Guid IsId, string Sema, string Tablo, string Mod,
    IReadOnlyList<AjanKolonDto> Kolonlar,
    IReadOnlyList<string> Pk,
    string? YeniSuSeviyesi) : ICommand<AjanTabloBitirResult>;

public sealed record AjanTabloBitirResult(long SatirSayisi);

/// <summary>İşi kapatır: durum/istatistik iş satırına yazılır, elle tetik damgası silinir.</summary>
public sealed record AjanIsBitirRequest(
    string Anahtar, Guid IsId,
    /// <summary>"Basarili" | "Hata"</summary>
    string Durum,
    string? Hata, long ToplamSatir, int Saniye, int ArtimliTabloSayisi) : ICommand<bool>;
