using Microsoft.EntityFrameworkCore;

namespace Kds.Data;

/// <summary>KdsDbContext yardımcıları. EF'in DbContext'i zaten Unit of Work'tür (K5) — ayrı UoW
/// soyutlaması yoktur; handler'lar doğrudan <c>context.SaveChangesAsync()</c> çağırır (tek SaveChanges
/// zaten atomiktir). Bu uzantı yalnız ÇOK-ADIMLI atomiklik gereken yerlere transaction sınırı verir.</summary>
public static class KdsDbContextExtensions
{
    /// <summary>Gövdeyi tek transaction içinde çalıştırır: sonunda kaydedip commit eder; gövde hata
    /// fırlatırsa transaction geri alınır. SQL Server yürütme stratejisi (retry) ile uyumludur.</summary>
    public static async Task IslemdeCalistirAsync(this KdsDbContext context,
        Func<CancellationToken, Task> govde, CancellationToken cancellationToken = default)
    {
        var strateji = context.Database.CreateExecutionStrategy();
        await strateji.ExecuteAsync(async ic =>
        {
            await using var islem = await context.Database.BeginTransactionAsync(ic);
            await govde(ic);
            await context.SaveChangesAsync(ic);
            await islem.CommitAsync(ic);
        }, cancellationToken);
    }
}
