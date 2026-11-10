using Kds.Common.Cqrs;

namespace Kds.Business.Connections;

/// <summary>Form değerleriyle (kaydetmeden) bağlantıyı dener. Parola düz metin gelir, yalnız
/// denemede kullanılır, saklanmaz.</summary>
public sealed record TestConnectionRequest(
    string Sunucu,
    string Veritabani,
    string? KullaniciAdi,
    string? Sifre) : ICommand<ConnectionTestResult>;
