using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Kds.Data;

public static class DataRegistrations
{
    /// <summary>Sabit bağlantılı kayıt (testler/araçlar): dizeyi veren basit bir sağlayıcı kurar.</summary>
    public static IServiceCollection AddKdsData(this IServiceCollection servisler, string baglantiDizesi)
    {
        servisler.AddSingleton<IMetadataBaglantiSaglayici>(new SabitBaglantiSaglayici(baglantiDizesi));
        return servisler.AddKdsData();
    }

    /// <summary>S16/A1: bağlantı dizesi ÇALIŞMA ZAMANINDA <see cref="IMetadataBaglantiSaglayici"/>'den
    /// çözülür (sağlayıcıyı çağıran kaydeder). Böylece kurulum sihirbazı bağlantıyı yazınca
    /// uygulama yeniden başlamadan yeni veritabanını kullanır.</summary>
    public static IServiceCollection AddKdsData(this IServiceCollection servisler)
    {
        // Veri değişim izi (olay günlüğü) — SaveChanges interceptor'ı; IEventLog/IActionContext
        // Api tarafında kayıtlıdır (yoksa no-op). Scoped: ActionId istek-başına.
        servisler.AddScoped<DataChangeInterceptor>();
        servisler.AddDbContext<KdsDbContext>((sp, s) => s
            .UseSqlServer(sp.GetRequiredService<IMetadataBaglantiSaglayici>().Dize())
            // EF10 PendingModelChangesWarning yanlış-pozitifi: `ef migrations add` BOŞ migration
            // üretirken (gerçek model farkı yok) Migrate() bu uyarıyla düşüyordu. Boş-fark
            // doğrulandı (KontrolFark denemesi) → uyarı bilinçli olarak log'a indirildi.
            .ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(sp.GetRequiredService<DataChangeInterceptor>()));
        return servisler;
    }

    private sealed class SabitBaglantiSaglayici(string dize) : IMetadataBaglantiSaglayici
    {
        public string Dize() => string.IsNullOrWhiteSpace(dize) ? throw new KurulumGerekliException() : dize;
    }
}
