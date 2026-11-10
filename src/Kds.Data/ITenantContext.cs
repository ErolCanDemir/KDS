namespace Kds.Data;

/// <summary>Mevcut isteğin tenant bağlamı. Global query filter ve TenantId damgalama
/// bunu kullanır. Gerçekleştirme (HTTP token'ından okuma) Api katmanındadır; böylece
/// Dal, ASP.NET bağımlılığı taşımaz.</summary>
public interface ITenantContext
{
    Guid? CurrentTenantId { get; }
}

/// <summary>Sabit tenant bağlamı — testler ve tasarım-zamanı (migration) için.</summary>
public sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? CurrentTenantId { get; } = tenantId;
}
