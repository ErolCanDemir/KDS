using Kds.Common.Cqrs;

namespace Kds.Contracts.Subscriptions;

/// <summary>Aboneliği ŞİMDİ çalıştırır: raporu koşar → seçilen biçime çevirir → alıcılara e-postayla
/// gönderir → son gönderim durumunu günceller. Hem elle "Şimdi Gönder" ucu hem zamanlayıcı bunu çağırır.</summary>
public sealed record RunSubscriptionRequest(Guid Id) : ICommand<SubscriptionRunResult>;
