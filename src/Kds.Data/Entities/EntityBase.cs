namespace Kds.Data.Entities;

/// <summary>Tüm metadata varlıklarının ortak temeli: kimlik + denetim zaman damgaları.
/// Zaman damgaları SaveChanges'te otomatik doldurulur (KdsDbContext).</summary>
public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OlusturmaTarihi { get; set; }
    public DateTimeOffset? GuncellemeTarihi { get; set; }
}
