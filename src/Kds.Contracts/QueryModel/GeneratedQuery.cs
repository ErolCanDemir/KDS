namespace Kds.Contracts.QueryModel;

/// <summary>Üretilen PARAMETRELİ SQL + parametre değerleri. "SQL'i Göster" bunu sunar (şeffaflık);
/// çalıştırıcı da bunu (SELECT-only bekçisinden geçirerek) koşar.</summary>
/// <param name="AraTablolar">S40 — sorguya KULLANICI İSTEMEDİĞİ hâlde, iki tabloyu birleştirmek
/// için giren tabloların dostça adları. Üretici tabloları haritadaki ilişkilerden BFS ile
/// birleştirir; bir tabloya birden çok eşit uzunlukta yol varsa ilk bulunanı seçer ve sorgu
/// hatasız çalışıp YANLIŞ ŞEYİ ölçebilir.
///
/// <para>Ölçülen vaka (25 Ağu 2026): "AUDİ marka araçların tutarı" sorusunda Markalar'a
/// <c>Araclar.MarkaId</c> yerine <c>Servisler.AnaBayiMarkaId</c> üzerinden bağlanıldı — aracın
/// markası değil servisin bayi markası süzüldü, hiçbir hata çıkmadan.</para>
///
/// <para>Yol seçimi burada DEĞİŞTİRİLMEZ (hangisinin doğru olduğu sorunun anlamına bağlıdır);
/// seçilen yol GÖRÜNÜR kılınır. Yalnız kullanıcının seçmediği tablolar listelenir: kullanıcı
/// Araclar'ı zaten seçmişse yol tektir ve söylemek gürültü olurdu.</para></param>
public sealed record GeneratedQuery(
    string Sql,
    IReadOnlyDictionary<string, object?> Parametreler,
    IReadOnlyList<string>? AraTablolar = null);
