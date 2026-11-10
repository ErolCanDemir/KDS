using Kds.Business.Identity;
using Kds.Contracts.Queries;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kds.Business.Queries;

/// <summary>Sorgu yürütmeyi saran denetim (audit) davranışı — cross-cutting concern, MediatR
/// pipeline (middleware zonu, AGENTS §4.6). Her icrayı (başarılı VE başarısız/engellenmiş)
/// <see cref="AuditRecord"/> olarak yazar: tenant (SaveChanges damgalar), bağlantı, SQL,
/// parametre değerleri, süre, satır sayısı, hata. Hatayı YUTMAZ — kaydeder ve yükseltir.</summary>
public sealed class QueryAuditBehavior(IServiceScopeFactory kapsamFabrikasi)
    : IPipelineBehavior<RunQueryRequest, QueryRunResult>
{
    public async Task<QueryRunResult> Handle(
        RunQueryRequest request, RequestHandlerDelegate<QueryRunResult> sonraki, CancellationToken cancellationToken)
    {
        var record = new AuditRecord
        {
            BaglantiId = request.BaglantiId,
            Sql = request.Sql,
            // Kod incelemesi O5: SQL şablonu tek başına "ne arandı"yı göstermez — değerler de yazılır.
            ParametrelerJson = ParametreJson(request.Parametreler),
            RaporId = request.RaporId, // M26: kayıtlı rapor koşumuysa denetimde rapor adı görünür
        };
        try
        {
            var result = await sonraki();
            record.Basarili = true;
            record.SatirSayisi = result.Satirlar.Count;
            record.SureMs = result.SureMs;
            await Yaz(record);
            return result;
        }
        catch (Exception hata)
        {
            record.Basarili = false;
            record.Hata = hata.Message.Length > 1000 ? hata.Message[..1000] : hata.Message;
            await Yaz(record);
            throw;
        }
    }

    /// <summary>Kod incelemesi O5: denetim İZOLE bir scope'un DbContext'iyle yazılır — isteğin
    /// scope'lu context'inde o an TRACKED duran başka değişikliklerin denetimle birlikte
    /// kaydedilmesi tuzağı kapanır. İptalden de bağımsız yazılır.</summary>
    private async Task Yaz(AuditRecord record)
    {
        using var kapsam = kapsamFabrikasi.CreateScope();
        var context = kapsam.ServiceProvider.GetRequiredService<KdsDbContext>();
        // "Kim": token'daki Keycloak sub → local User.Id (denetim ekranı bunu gösterir). Kullanıcı
        // bağlamı yoksa (ör. zamanlayıcı) null kalır. İzole scope, singleton HttpContextAccessor
        // üzerinden aynı isteğin kullanıcısını görür.
        var kb = kapsam.ServiceProvider.GetRequiredService<IUserContext>();
        if (kb.KeycloakId is Guid kid)
        {
            record.KullaniciId = await context.Kullanicilar
                .Where(k => k.KeycloakId == kid)
                .Select(k => (Guid?)k.Id)
                .FirstOrDefaultAsync(CancellationToken.None);
        }
        context.DenetimKayitlari.Add(record);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static string? ParametreJson(IReadOnlyDictionary<string, object?>? parametreler)
    {
        if (parametreler is null || parametreler.Count == 0) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(parametreler);
        }
        catch (NotSupportedException)
        {
            // Dar-tipli serileştirme koruması (izinli bölge): beklenmedik değer tipi denetimi düşürmesin.
            return System.Text.Json.JsonSerializer.Serialize(
                parametreler.ToDictionary(p => p.Key, p => p.Value?.ToString()));
        }
    }
}
