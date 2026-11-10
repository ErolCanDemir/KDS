using Kds.Contracts.DataMaps;
using Kds.Contracts.QueryModel;

namespace Kds.Execution;

/// <summary>Sorgu modelini (AST) motor lehçesine göre PARAMETRELİ SQL'e çevirir (00-analiz §5).
/// Kurallar (MersisMSSQL mirası): nesne adları daima QUOTENAME mantığıyla parantezlenir; değerler
/// asla birleştirilmez, daima parametre. Girdi şema olarak Veri Haritası'nı (<see cref="DataMapDetail"/>)
/// alır — yalnız haritadaki tablo/kolon/ilişkiler sorgulanabilir. v1: <c>MssqlQueryGenerator</c>.</summary>
public interface IQueryGenerator
{
    /// <summary><paramref name="parametreDegerleri"/>: çalıştırma-anı parametre değerleri
    /// (anahtar→değer). Filtrede <c>{ "$param": "anahtar" }</c> referansları bundan (yoksa
    /// parametrenin varsayılanından) çözülür. Eksik + zorunlu parametre → <see cref="QueryModelException"/>.
    /// <para><paramref name="satirKurallari"/>: satır-düzeyi güvenlik (RLS). Kullanıcının
    /// GÖREBİLECEĞİ satırları daraltır; modelden değil ÇAĞIRANDAN gelir (kullanıcı düzenleyemesin)
    /// ve WHERE'e VE ile eklenir. Kuralın tablosu sorguda yoksa yok sayılır — o tablodan zaten satır
    /// dönmez. Birleşim (UNION) dallarına da uygulanır.</para></summary>
    GeneratedQuery Generate(QueryModel model, DataMapDetail dataMap,
        IReadOnlyDictionary<string, object?>? parametreDegerleri = null,
        IReadOnlyList<RowFilter>? satirKurallari = null);
}

/// <summary>Model haritayla tutarsızsa: haritada olmayan tablo/kolon, ilişkisiz (join yolu olmayan)
/// tablolar, boş alan seçimi, tutarsız filtre/sıralama. İnsana okunur sebeple fırlatılır.</summary>
public sealed class QueryModelException(string mesaj) : Exception(mesaj);
