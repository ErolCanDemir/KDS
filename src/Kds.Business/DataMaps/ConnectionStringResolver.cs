using Kds.Business.Connections;
using Kds.Execution;
using Kds.Data;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.DataMaps;

/// <summary>Bir bağlantı Id'sinden (tenant-filtreli) çalıştırılabilir bağlantı dizesi üretir:
/// parolayı çözer + dizeyi kurar. Şema keşfi handler'ları paylaşır.</summary>
public sealed class ConnectionStringResolver(KdsDbContext context, IPasswordProtector koruma)
{
    public async Task<string> ResolveAsync(Guid baglantiId, CancellationToken cancellationToken)
    {
        var b = await context.Baglantilar.FirstOrDefaultAsync(x => x.Id == baglantiId, cancellationToken)
            ?? throw new KeyNotFoundException("Bağlantı bulunamadı.");
        var sifre = string.IsNullOrEmpty(b.SifreSifreli) ? null : koruma.Unprotect(b.SifreSifreli);
        return SqlConnectionString.Kur(b.Sunucu, b.Veritabani, b.KullaniciAdi, sifre);
    }
}
