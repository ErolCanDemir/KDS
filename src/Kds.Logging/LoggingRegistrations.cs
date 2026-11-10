using Kds.Contracts.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kds.Logging;

public static class LoggingRegistrations
{
    /// <summary>Olay günlüğünü (KdsLog Mongo) kaydeder. Bağlantı dizesi boşsa no-op (dev'de Mongo
    /// olmadan çalışır). Doluysa tek <see cref="MongoEventLog"/> örneği hem <see cref="IEventLog"/>
    /// hem arka plan yazıcı olarak eklenir.</summary>
    public static IServiceCollection AddKdsLogging(this IServiceCollection servisler,
        string? baglantiDizesi, string veritabani = "KdsLog")
    {
        if (string.IsNullOrWhiteSpace(baglantiDizesi))
        {
            servisler.AddSingleton<IEventLog, NullEventLog>();
            return servisler;
        }

        servisler.Configure<EventLogOptions>(o =>
        {
            o.ConnectionString = baglantiDizesi;
            o.Database = veritabani;
        });
        servisler.AddSingleton<MongoEventLog>();
        servisler.AddSingleton<IEventLog>(sp => sp.GetRequiredService<MongoEventLog>());
        servisler.AddHostedService(sp => sp.GetRequiredService<MongoEventLog>());
        return servisler;
    }
}
