namespace Kds.Data.Entities;

/// <summary>Bir tenant'a ait (çok-kiracılık kapsamındaki) varlık. Global query filter
/// bu arayüzü uygulayan varlıklara TenantId'ye göre uygulanır; SaveChanges eklemede
/// TenantId'yi existing tenant'tan damgalar.</summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
