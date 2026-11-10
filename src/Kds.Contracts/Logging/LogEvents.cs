namespace Kds.Contracts.Logging;

/// <summary>Olay günlüğü kaydı işaretçisi. Üç somut tür: <see cref="ActionEvent"/> (istek),
/// <see cref="ExceptionEvent"/> (hata), <see cref="DataChangeEvent"/> (veri değişimi). Exception ve
/// DataChange, ait oldukları Action'a <c>ActionId</c> ile bağlanır (korelasyon). Ayrı Mongo DB
/// (KdsLog) — QMS'ten bağımsız; her tür kendi koleksiyonunda (Action/Exception/DataChange).</summary>
public interface ILogEvent
{
    /// <summary>Yazılacağı koleksiyon (Action | Exception | DataChange).</summary>
    string Collection { get; }
}

/// <summary>Bir istek/aksiyon özeti — `_id` = ActionId (Exception/DataChange bununla ilişkilenir).</summary>
public sealed class ActionEvent : ILogEvent
{
    public string Collection => "Action";
    public Guid Id { get; set; }                      // _id = ActionId
    public string AppId { get; set; } = "lst-kds";
    public DateTime Timestamp { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Action { get; set; }               // uç adı / rota
    public string? Path { get; set; }
    public string? Method { get; set; }
    public int Status { get; set; }
    public long DurationMs { get; set; }
    public string? Ip { get; set; }
}

/// <summary>Yakalanan hata — ait olduğu aksiyona <see cref="ActionId"/> ile bağlanır.</summary>
public sealed class ExceptionEvent : ILogEvent
{
    public string Collection => "Exception";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActionId { get; set; }
    public string AppId { get; set; } = "lst-kds";
    public DateTime Timestamp { get; set; }
    public string? Type { get; set; }
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
    public string? Path { get; set; }
    public string? Method { get; set; }
    public int Status { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
}

/// <summary>Bir varlığın Insert/Update/Delete izi (eski→yeni) — ait olduğu aksiyona
/// <see cref="ActionId"/> ile bağlanır. EF SaveChanges interceptor'ından üretilir.</summary>
public sealed class DataChangeEvent : ILogEvent
{
    public string Collection => "DataChange";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActionId { get; set; }
    public string AppId { get; set; } = "lst-kds";
    public DateTime Timestamp { get; set; }
    public string? EntityName { get; set; }
    public string? Key { get; set; }
    public string? Operation { get; set; }            // Insert | Update | Delete
    public string? Before { get; set; }               // JSON (Update/Delete)
    public string? After { get; set; }                // JSON (Insert/Update)
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
}
