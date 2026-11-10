namespace Kds.Contracts.Logging;

/// <summary>Olay günlüğü yazma kapısı. <see cref="Write"/> BLOKLAMAZ — olayı dayanıklı bir arka plan
/// kanalına bırakır; asıl Mongo yazımı hosted service'te olur. İstek yolunu yavaşlatmaz ve Mongo
/// düşse bile isteği bozmaz. Yapılandırma yoksa no-op (yutar).</summary>
public interface IEventLog
{
    void Write(ILogEvent olay);
}
