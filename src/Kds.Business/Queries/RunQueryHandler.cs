using Kds.Business.Connections;
using Kds.Business.Ingest;
using Kds.Contracts.Connections;
using Kds.Execution;
using Kds.Contracts.Queries;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Queries;

/// <summary>Sorgu yürütme orkestrasyonu: bağlantıyı yükler, parolayı çözüp bağlantı dizesini
/// kurar, çalıştırıcıya verir (çalıştırıcı SELECT-only bekçisini zorlar), sonucu DTO'ya çevirir.
/// Denetim kaydı ayrı bir pipeline davranışında (<see cref="QueryAuditBehavior"/>) bırakılır.
///
/// <b>Mod yönlendirmesi (S12/Faz 3):</b> bağlantı <see cref="DataMode.IceAlim"/> ise sorgu CANLI
/// kaynağa değil tenant'ın kendi deposuna (<c>KdsDepo_&lt;tenant&gt;</c>) gider — üretilen SQL,
/// bekçi ve audit aynıdır; yalnız bağlantı dizesi değişir. Böylece bir raporun bağlantısını
/// Canlı↔İçe Alım arasında çevirmek raporu bozmaz (başarı ölçütü #3).</summary>
public sealed class RunQueryHandler(
    IQueryExecutor calistirici, QueryConnectionResolver cozucu, Settings.ISistemAyarlari ayarlar)
    : IRequestHandler<RunQueryRequest, QueryRunResult>
{
    public async Task<QueryRunResult> Handle(RunQueryRequest request, CancellationToken cancellationToken)
    {
        // S16/A3: limitler ve zaman aşımı artık koddaki sabitler değil, Sistem Ayarları'ndan
        // (kurulum geneli ya da kiracıya özel). Varsayılanlar AyarTanimlari'nda.
        var enCokSatir = ayarlar.Sayi(Settings.AyarTanimlari.SorguEnCokSatir);
        var varsayilanSatir = ayarlar.Sayi(Settings.AyarTanimlari.SorguVarsayilanSatir);
        var zamanAsimi = TimeSpan.FromSeconds(ayarlar.Sayi(Settings.AyarTanimlari.SorguZamanAsimiSn));
        var sinir = request.SatirSiniri is int s and > 0 ? Math.Min(s, enCokSatir) : varsayilanSatir;
        // Mod yönlendirmesi (Canlı/İçe Alım + M22b depo hedefi) paylaşılan çözücüde — tanılama da aynı yolu kullanır.
        var connectionString = await cozucu.ResolveAsync(request.BaglantiId, cancellationToken);

        // M11: ham SQL yolunda parametreler wire'dan JsonElement olarak gelebilir (editör
        // @parametreleri) — SqlParameter'a bağlanabilmeleri için CLR'a çözülür (model yolunda
        // zaten çözülmüş değerler olduğu gibi geçer).
        var parametreler = (request.Parametreler ?? new Dictionary<string, object?>())
            .ToDictionary(p => p.Key, p => JsonDegerCozucu.Coz(p.Value));

        var result = await calistirici.ExecuteAsync(
            new QueryRequest(connectionString, request.Sql, parametreler, sinir, zamanAsimi), cancellationToken);

        return new QueryRunResult(
            result.Kolonlar.Select(k => new QueryColumnDto(k.Ad, k.VeriTipi)).ToList(),
            result.Satirlar.Select(r => (IReadOnlyList<object?>)r).ToList(),
            result.SatirSiniriUygulandi,
            (int)result.Sure.TotalMilliseconds);
    }

}
