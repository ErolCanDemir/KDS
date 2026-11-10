using Kds.Contracts.QueryModel;
using Kds.Execution;
using Kds.Data;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Metrics;

/// <summary>S22 — modeldeki metrik REFERANSLARINI katalogdan formüle çözer.
///
/// <para>Model, formülün kopyasını değil metriğin kimliğini taşır; çözüm her koşumda burada
/// yapılır. Böylece metrik güncellenince onu kullanan her rapor bir sonraki koşumda yeni tanımı
/// kullanır — "tek doğruluk kaynağı" vaadi ancak koşum-anı çözümüyle tutar (kopya olsaydı ilk
/// güncellemede bozulurdu).</para>
///
/// <para><b>Fail-closed:</b> metrik silinmiş/pasifse ya da BAŞKA haritaya aitse sorgu çalışmaz,
/// mesaj hangi metriğin neden kullanılamadığını söyler. Sessizce atlamak, kolonun yok olduğunu
/// kimse fark etmeden rapor üretmek demekti — Y1'deki "sessizce 0 dönme" dersiyle aynı sınıf.</para></summary>
public interface IMetrikCozucu
{
    Task<QueryModel> CozAsync(QueryModel model, CancellationToken ct);
}

public sealed class MetrikCozucu(KdsDbContext context) : IMetrikCozucu
{
    /// <summary>Modelde metrik referansı varsa bunları <see cref="ComputedColumn"/>'a çevirip
    /// modelin hesaplanan alanlarına EKLER; yoksa modeli olduğu gibi döndürür. Üretici metrik
    /// kavramını hiç bilmez — Y1 ifade hattı aynen kullanılır.</summary>
    public async Task<QueryModel> CozAsync(QueryModel model, CancellationToken ct)
    {
        if (model.MetrikAlanlar is not { Count: > 0 } referanslar)
        {
            return model;
        }

        var kimlikler = referanslar.Select(r => r.MetrikId).ToList();
        var metrikler = await context.Metrikler.AsNoTracking()
            .Where(m => kimlikler.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var cozulen = new List<ComputedColumn>(model.HesaplananAlanlar ?? []);
        foreach (var referans in referanslar)
        {
            if (!metrikler.TryGetValue(referans.MetrikId, out var metrik))
            {
                throw new QueryModelException(
                    "Bu sorgudaki bir metrik katalogdan silinmiş. Raporu düzenleyip metriği " +
                    "kaldırın ya da katalogda yeniden tanımlayın.");
            }

            if (!metrik.Aktif)
            {
                throw new QueryModelException(
                    $"'{metrik.Ad}' metriği pasife alınmış — bu sorgu onu kullanıyor. Metriği " +
                    "katalogda aktifleştirin ya da rapordan çıkarın.");
            }

            if (metrik.VeriHaritasiId != model.HaritaId)
            {
                throw new QueryModelException(
                    $"'{metrik.Ad}' metriği başka bir veri haritasına tanımlı; bu sorgunun " +
                    "haritasında kolonları yok. Rapordan çıkarın ya da bu haritada eşdeğerini tanımlayın.");
            }

            cozulen.Add(new ComputedColumn(metrik.Ifade, referans.TakmaAd ?? metrik.Ad));
        }

        return model with { HesaplananAlanlar = cozulen, MetrikAlanlar = null };
    }
}
