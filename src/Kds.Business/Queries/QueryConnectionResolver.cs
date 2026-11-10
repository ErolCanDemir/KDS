using Kds.Business.Connections;
using Kds.Business.DataMaps;
using Kds.Business.Ingest;
using Kds.Contracts.Connections;
using Kds.Data;
using Kds.Execution;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Queries;

/// <summary>Bir bağlantının SORGUNUN KOŞACAĞI yerin dizesini çözer — koşum (RunQuery) ve tanılama
/// (M33 DiagnoseQuery) aynı yönlendirmeyi paylaşır: Canlı → kaynağın kendisi; İçe Alım → depo
/// hedefi seçiliyse o bağlantı (M22b), yoksa varsayılan tenant deposu (KdsDepo_&lt;tenant&gt;).</summary>
public sealed class QueryConnectionResolver(
    KdsDbContext context, IPasswordProtector koruma, DepoCozucu depo,
    ITenantContext tenant, ConnectionStringResolver dizeCozucu)
{
    public async Task<string> ResolveAsync(Guid baglantiId, CancellationToken cancellationToken)
    {
        var baglanti = await context.Baglantilar.FirstOrDefaultAsync(b => b.Id == baglantiId, cancellationToken)
            ?? throw new KeyNotFoundException("Bağlantı bulunamadı.");

        return baglanti.Mod == DataMode.IceAlim
            ? await DepoDizesiAsync(baglanti.DepoBaglantiId, cancellationToken)
            : SqlConnectionString.Kur(baglanti.Sunucu, baglanti.Veritabani, baglanti.KullaniciAdi,
                string.IsNullOrEmpty(baglanti.SifreSifreli) ? null : koruma.Unprotect(baglanti.SifreSifreli));
    }

    /// <summary>İçe Alım modlu bağlantının depo dizesi. M22b: bağlantıda depo HEDEFİ seçiliyse
    /// (yazma oraya gitti) okuma da o bağlantının veritabanından yapılır; yoksa varsayılan
    /// tenant deposu (KdsDepo_&lt;tenant&gt;).</summary>
    private async Task<string> DepoDizesiAsync(Guid? depoBaglantiId, CancellationToken cancellationToken)
    {
        if (depoBaglantiId is Guid hedefId)
            return await dizeCozucu.ResolveAsync(hedefId, cancellationToken);
        var sunucu = depo.SunucuDizesi();
        if (string.IsNullOrWhiteSpace(sunucu))
            throw new InvalidOperationException(
                "İçe alım deposu yapılandırılmamış — Yönetim → Sistem Ayarları → Depo bölümünden girin.");
        return SqlConnectionString.VeritabaniIle(sunucu, depo.DbAdi(tenant.CurrentTenantId ?? Guid.Empty));
    }
}
