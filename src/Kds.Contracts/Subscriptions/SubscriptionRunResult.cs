namespace Kds.Contracts.Subscriptions;

/// <summary>Bir abonelik gönderiminin sonucu: kaç alıcıya, kaç satırlık ek, kaç saniyede, hangi dosya
/// — ve <b>gerçekten gönderildi mi</b>. <see cref="EpostaGonderildi"/> false ise ek üretildi ama SMTP
/// yapılandırılmadığı için yalnız günlüğe yazıldı; ekran bunu varsaymak yerine buradan okur.</summary>
public sealed record SubscriptionRunResult(
    int AliciSayisi, long SatirSayisi, int SaniyeSure, string DosyaAdi, bool EpostaGonderildi);
