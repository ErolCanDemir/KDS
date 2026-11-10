namespace Kds.Contracts.Logging;

/// <summary>İstek-başına korelasyon bağlamı. <see cref="ActionId"/> istek boyunca sabittir; aynı
/// istekte üretilen tüm <see cref="DataChangeEvent"/> ve <see cref="ExceptionEvent"/> kayıtları bu
/// Id'yi taşır → hangi aksiyonun neyi değiştirdiği/hangi hatayı attığı izlenir. Kullanıcı/tenant/ip
/// istek claim'lerinden türetilir (scoped).</summary>
public interface IActionContext
{
    Guid ActionId { get; }
    Guid? TenantId { get; }
    Guid? UserId { get; }
    string? UserName { get; }
    string? Ip { get; }
}
