using Kds.Common.Cqrs;

namespace Kds.Contracts.Reports;

/// <summary>Raporu ÇALIŞTIRIR (kod incelemesi K1): görünürlük denetimi handler'da (Özel → sahibi +
/// admin; Ekip → tenant'taki herkes) — böylece <c>musteri</c> rolü de paylaşılan raporu koşabilir.
/// İcra yine tek kapıdan (bekçi + satır sınırı + zaman aşımı + denetim) geçer.
/// <see cref="ParametreDegerleri"/>: parametreli raporun çalıştırma-anı değerleri (anahtar→değer).</summary>
public sealed record RunReportRequest(
    Guid Id,
    IReadOnlyDictionary<string, object?>? ParametreDegerleri = null) : ICommand<Queries.QueryRunResult>;
