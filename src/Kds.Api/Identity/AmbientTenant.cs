namespace Kds.Api.Identity;

/// <summary>Arka plan işleri (İçe alım zamanlayıcı) için ambient tenant bağlamı. HTTP isteğinde
/// AYARLANMAZ (null kalır → tenant token'dan okunur); bir iş belirli bir tenant adına çalışırken
/// <see cref="Baglan"/> ile açıkça bağlanır ve iş bitince (Dispose) temizlenir. AsyncLocal olduğu
/// için await zinciri boyunca akar; eşzamanlı işler birbirinin değerini görmez.</summary>
public static class AmbientTenant
{
    private static readonly AsyncLocal<Guid?> _mevcut = new();

    public static Guid? Current => _mevcut.Value;

    public static IDisposable Baglan(Guid tenantId)
    {
        _mevcut.Value = tenantId;
        return new Cozucu();
    }

    private sealed class Cozucu : IDisposable
    {
        public void Dispose() => _mevcut.Value = null;
    }
}
