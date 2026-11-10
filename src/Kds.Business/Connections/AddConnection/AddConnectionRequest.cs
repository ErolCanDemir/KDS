using Kds.Contracts.Connections;
using Kds.Common.Cqrs;

namespace Kds.Business.Connections;

/// <summary>Yeni bağlantı ekler. KullaniciAdi boşsa entegre (Windows) kimlik; Sifre boş
/// olabilir. Parola Data Protection ile şifreli saklanır. <see cref="DepoBaglantiId"/> yalnız
/// İçe Alım modunda anlamlıdır (M22b: depo hedefi; null = varsayılan tenant deposu).</summary>
public sealed record AddConnectionRequest(
    string Ad,
    string Sunucu,
    string Veritabani,
    string? KullaniciAdi,
    string? Sifre,
    DataMode Mod,
    Guid? DepoBaglantiId = null,
    /// <summary>S45 — dolu ise kaynağa sunucu değil müşteri ağındaki AJAN bağlanır (gerçek dize
    /// ajanın yerel yapılandırmasında); yalnız İçe Alım modunda geçerli.</summary>
    Guid? AjanId = null) : ICommand<ConnectionSummary>;
