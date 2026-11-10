using Kds.Contracts.Queries;
using Microsoft.Data.SqlClient;

namespace Kds.Execution;

/// <summary>M33 "Sorun Ne?" — MSSQL tanılayıcı (IO adaptörü). Sorgu bekçiden geçirilir, sonra
/// <c>SET SHOWPLAN_XML ON</c> ile TAHMİNİ plan alınır (veri okunmaz) ve yorumu
/// <see cref="QueryPlanRules"/>'a devredilir. Plan alınamayan durumların HER BİRİ ayrı yanıtlanır:
/// SHOWPLAN yetkisi yok (Msg 262) → yetki bulgusu + SQL metninden genel öneriler; zaman aşımı →
/// "sunucu yanıtsız/ilk derleme"; diğer bağlantı hataları → bağlantı bulgusu. try/catch meşru —
/// IO adaptörü sınırı (AGENTS §4.6).</summary>
public sealed class SqlQueryDiagnostician(IQueryGuard bekci) : IQueryDiagnostician
{
    // Zaman aşımı isteğin kendisinde (Sistem Ayarları'ndan): tahmini plan normalde saniyeler
    // sürer ama İLK derleme, eksik istatistikleri oluştururken (auto-create stats gerçek IO
    // yapar) büyük tablolarda uzayabilir — ölçüldü (varsayılan 30 sn).
    public async Task<QueryDiagnosis> DiagnoseAsync(DiagnosisRequest request, CancellationToken cancellationToken)
    {
        // Koşumla aynı tek kapı: bekçiyi geçemeyen SQL tanılanmaz da.
        var karar = bekci.Inspect(request.Sql);
        if (!karar.Gecerli)
            throw new QuerySecurityException(karar.Sebep!);

        string? planXml;
        try
        {
            planXml = await PlanAlAsync(request, cancellationToken);
        }
        catch (SqlException hata) when (hata.Number == 262 ||
            hata.Message.Contains("SHOWPLAN", StringComparison.OrdinalIgnoreCase))
        {
            // Kullanıcının plana erişim yetkisi yok — "teşhis edemedik" demek DEĞİL: söyle + genel öner.
            var bulgular = new List<DiagnoseFinding>
            {
                new("Plan görüntüleme yetkisi yok",
                    "Bu bağlantının SQL kullanıcısında sorgu planını görme (SHOWPLAN) yetkisi yok; " +
                    "kesin teşhis için plan gerekli.",
                    "yuksek",
                    "Veritabanı yöneticisinden (DBA) bu kullanıcıya SHOWPLAN yetkisi vermesini isteyin; " +
                    "yetki gelene kadar aşağıdaki genel önerilere bakın.",
                    "GRANT SHOWPLAN TO [<kds_kullanicisi>];"),
            };
            bulgular.AddRange(QueryPlanRules.MetindenIpuclari(request.Sql));
            return new QueryDiagnosis(
                "Plan alınamadı (yetki yok) — kesin teşhis için DBA'dan SHOWPLAN yetkisi isteyin; genel öneriler listelendi.",
                bulgular);
        }
        catch (SqlException hata) when (hata.Number == -2)
        {
            return new QueryDiagnosis(
                "Sorun büyük olasılıkla sorguda değil: kaynak sunucu şu an plana bile yanıt veremiyor (yüklü/yanıtsız).",
                [new DiagnoseFinding("Kaynak sunucu yanıt vermiyor",
                    $"Sorgunun tahmini planını almak bile {request.PlanZamanAsimiSn} sn içinde tamamlanamadı — sunucu aşırı " +
                    "yüklü/kilitli olabilir ya da ilk plan derlemesi (eksik istatistiklerin oluşturulması) uzun sürüyor.",
                    "yuksek",
                    "Birkaç dakika sonra yeniden deneyin (istatistikler oluşunca hızlanır); sürerse sunucu yükünü/" +
                    "bloklanan oturumları DBA ile inceleyin.")]);
        }
        catch (SqlException hata)
        {
            return new QueryDiagnosis(
                "Hedefe bağlanılamadı — sorun sorgudan önce bağlantıda.",
                [new DiagnoseFinding("Bağlantı hatası",
                    $"Tanılama hedefe bağlanamadı: {hata.Message}",
                    "yuksek",
                    "Bağlantılar ekranından bu bağlantıyı test edin; sunucu adı/kimlik bilgilerini doğrulayın.")]);
        }

        if (string.IsNullOrWhiteSpace(planXml))
            return new QueryDiagnosis(
                "Plan alınamadı — sunucu boş plan döndürdü; genel öneriler listelendi.",
                QueryPlanRules.MetindenIpuclari(request.Sql));

        return QueryPlanRules.PlandanTani(planXml, request.TaramaEsigi, request.SiralamaEsigi);
    }

    /// <summary>Tahmini planı çeker: SET SHOWPLAN_XML ON altındaki komut ÇALIŞMAZ, plan XML'i döner.</summary>
    private static async Task<string?> PlanAlAsync(DiagnosisRequest request, CancellationToken ct)
    {
        await using var baglanti = new SqlConnection(request.BaglantiDizesi);
        await baglanti.OpenAsync(ct);

        await using (var ac = baglanti.CreateCommand())
        {
            ac.CommandText = "SET SHOWPLAN_XML ON";
            await ac.ExecuteNonQueryAsync(ct);
        }

        await using var komut = baglanti.CreateCommand();
        komut.CommandText = request.Sql;
        komut.CommandTimeout = request.PlanZamanAsimiSn;
        foreach (var (ad, deger) in request.Parametreler)
            komut.Parameters.AddWithValue(ad, deger ?? DBNull.Value);
        return await komut.ExecuteScalarAsync(ct) as string;
    }
}
