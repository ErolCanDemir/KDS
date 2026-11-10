namespace Kds.Execution;

/// <summary>Çalıştırma dünyası: bir kaynak DB'ye bağlanabiliyor muyuz denetler (saf ADO.NET).
/// Kds.Data'a referans vermez; Contracts'a da bağlanmamak için kendi sonuç tipini döndürür
/// (Business, Contracts'a çevirir).</summary>
public interface IConnectionProber
{
    Task<ProbeResult> ProbeAsync(
        string sunucu,
        string veritabani,
        string? kullaniciAdi,
        string? sifre,
        CancellationToken cancellationToken);
}

/// <summary>Bir bağlantı denemesinin ham sonucu.</summary>
public readonly record struct ProbeResult(bool Basarili, int? Ms, string? Mesaj);
