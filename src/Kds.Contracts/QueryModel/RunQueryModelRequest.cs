using Kds.Common.Cqrs;
using Kds.Contracts.Queries;

namespace Kds.Contracts.QueryModel;

/// <summary>Sorgu modelini SQL'e çevirir VE haritanın bağlantısında çalıştırır (tasarımcının
/// "Çalıştır"ı). Üretilen SQL yine SELECT-only bekçisinden geçer; denetim kaydı bırakılır.
/// <see cref="ParametreDegerleri"/>: modeldeki çalıştırma-anı parametrelerinin değerleri (anahtar→değer).</summary>
public sealed record RunQueryModelRequest(
    QueryModel Model,
    IReadOnlyDictionary<string, object?>? ParametreDegerleri = null,
    // M26: koşum kayıtlı bir RAPOR üzerinden geldiyse kimliği — RunQueryRequest'e aktarılır.
    Guid? RaporId = null,
    /// <summary>RLS'in KİME uygulanacağı. Normal (HTTP) koşumda null → isteğin kendi kullanıcısı.
    /// <para>Zamanlanmış işler (abonelik · uyarı) arka planda, istek bağlamı olmadan koşar; orada
    /// boş kimlik <b>sessizce filtresiz</b> sonuç üretirdi. Bu yüzden iş, kurulurken saklanan
    /// <b>sahip kimliği anlık görüntüsünü</b> buradan geçirir ve satır kuralları o kimliğe
    /// uygulanır — piyasadaki karşılığı da budur (zamanlanmış gönderim, rapor sahibinin
    /// yetkisiyle koşar).</para></summary>
    Identity.CalistiranKullanici? KosanKimlik = null) : ICommand<QueryRunResult>;
