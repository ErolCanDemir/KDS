using Kds.Contracts.Queries;

namespace Kds.Execution;

/// <summary>M33 — sorgu tanılayıcı: sorguyu ÇALIŞTIRMADAN tahmini planını (SHOWPLAN_XML) alır ve
/// kural motoruyla bulgulara çevirir. Plan alınamazsa (SHOWPLAN yetkisi yok / sunucu yanıtsız /
/// bağlantı hatası) bunu bulgu olarak raporlar; SQL metninden genel öneriler ekler.</summary>
public interface IQueryDiagnostician
{
    Task<QueryDiagnosis> DiagnoseAsync(DiagnosisRequest request, CancellationToken cancellationToken);
}

/// <summary>Tanılanacak sorgu: hedef bağlantı dizesi + parametreli SQL (koşumla aynı biçim).
/// S16/A3: zaman aşımı ve bulgu eşikleri Sistem Ayarları'ndan gelir; verilmezse ürün
/// varsayılanları kullanılır (ortama bağlı değer değildir).</summary>
public sealed record DiagnosisRequest(
    string BaglantiDizesi,
    string Sql,
    IReadOnlyDictionary<string, object?> Parametreler,
    int PlanZamanAsimiSn = 30,
    double TaramaEsigi = QueryPlanRules.VarsayilanTaramaEsigi,
    double SiralamaEsigi = QueryPlanRules.VarsayilanSiralamaEsigi);
