namespace Kds.Contracts.Ingest;

/// <summary>Bir içe alım koşusunun sonucu: kaç tablo, kaç satır, kaç saniye.
/// <see cref="ArtimliTabloSayisi"/> (S44): kaçı artımlı (delta) yolla yüklendi — kalanı tam
/// yüklemedir (ilk koşu, şema kayması ya da PK yokluğu tam yüklemeye düşürür; kullanıcı
/// "artımlı seçtim ama tam koştu"yu buradan görür).</summary>
public sealed record IngestRunResult(int TabloSayisi, long ToplamSatir, int SaniyeSure,
    int ArtimliTabloSayisi = 0,
    /// <summary>S45 — kaynak AJANLI: iş sunucuda koşmadı, elle tetik damgalandı; müşteri
    /// ağındaki ajan bir sonraki yoklamasında koşacak. Diğer sayılar 0 döner.</summary>
    bool AjanaDevredildi = false);
