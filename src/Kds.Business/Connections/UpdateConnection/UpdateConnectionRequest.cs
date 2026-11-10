using Kds.Contracts.Connections;
using Kds.Common.Cqrs;

namespace Kds.Business.Connections;

/// <summary>Bağlantıyı günceller. <see cref="Sifre"/> null/boşsa existing parola korunur
/// (UI parolayı geri göndermez). Bulunamazsa null döner (404). <see cref="DepoBaglantiId"/>
/// yalnız İçe Alım modunda anlamlıdır (M22b: depo hedefi; null = varsayılan tenant deposu).</summary>
public sealed record UpdateConnectionRequest(
    Guid Id,
    string Ad,
    string Sunucu,
    string Veritabani,
    string? KullaniciAdi,
    string? Sifre,
    DataMode Mod,
    Guid? DepoBaglantiId = null,
    /// <summary>S45 — dolu ise kaynağa sunucu değil müşteri ağındaki AJAN bağlanır.</summary>
    Guid? AjanId = null) : ICommand<ConnectionSummary?>;
