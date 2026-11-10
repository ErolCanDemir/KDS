namespace Kds.Contracts.Connections;

/// <summary>Bir bağlantının veriyi nasıl getireceği (00-analiz §3, K11). Bağlantı düzeyinde
/// öntanımlıdır; veri haritası düzeyinde geçersiz kılınabilir.</summary>
public enum DataMode
{
    /// <summary>Canlı (DirectQuery): sorgu kaynağın kendisinde anlık koşar.</summary>
    Canli = 0,

    /// <summary>İçe Alım (Import): veri zamanlı işle KDS deposuna çekilir, sorgu orada koşar.</summary>
    IceAlim = 1,
}
