using Kds.Common.Cqrs;

namespace Kds.Contracts.Queries;

/// <summary>M33 "Sorun Ne?" — yavaş/zaman aşımı almış bir sorgunun tanısı. Sorgu ÇALIŞTIRILMAZ:
/// tahmini plan (SHOWPLAN_XML) alınır ve kural motoru bulgulara çevirir. Plan alınamazsa
/// (yetki yok / sunucu yanıtsız) bunun kendisi bir bulgudur; SQL metninden genel öneriler eklenir.</summary>
public sealed record QueryDiagnosis(string Ozet, IReadOnlyList<DiagnoseFinding> Bulgular);

/// <summary>Tek tanı bulgusu. <see cref="Siddet"/>: "yuksek" | "orta" | "bilgi".
/// <see cref="KopyalanabilirSql"/>: kullanıcının DBA'ya götürebileceği öneri metni
/// (ör. CREATE INDEX) — KDS kaynak veritabanına asla kendisi yazmaz.</summary>
public sealed record DiagnoseFinding(
    string Baslik,
    string Aciklama,
    string Siddet,
    string? Oneri = null,
    string? KopyalanabilirSql = null);

/// <summary>Ham SQL tanılaması (SQL Editörü): bağlantının sorgu hedefinde (Canlı kaynak ya da
/// İçe Alım deposu — koşumla AYNI yönlendirme) tahmini plan alınıp yorumlanır.</summary>
public sealed record DiagnoseQueryRequest(
    Guid BaglantiId,
    string Sql,
    IReadOnlyDictionary<string, object?>? Parametreler = null) : ICommand<QueryDiagnosis>;
