namespace Kds.Data;

/// <summary>S16/A1 — yönetim (metadata) veritabanının bağlantı dizesini ÇALIŞMA ZAMANINDA verir.
/// Eskiden dize açılışta <c>AddKdsData(dize)</c> ile closure'a hapsediliyordu; bu yüzden bağlantı
/// ancak dosya düzenleyip yeniden başlatarak değişebiliyordu. Artık DbContext seçenekleri her
/// scope'ta bunu sorar → kurulum sihirbazı (A2) bağlantıyı yazınca uygulama YENİDEN BAŞLAMADAN
/// yeni veritabanını kullanır.</summary>
public interface IMetadataBaglantiSaglayici
{
    /// <summary>Yapılandırılmamışsa <see cref="KurulumGerekliException"/> fırlatır.</summary>
    string Dize();
}

/// <summary>Uygulama henüz kurulmamış (yönetim veritabanı yapılandırılmamış): API bunu 503 +
/// <c>kurulumGerekli</c> olarak döner, önyüz kurulum sihirbazına yönlendirir.</summary>
public sealed class KurulumGerekliException()
    : InvalidOperationException("Uygulama henüz kurulmamış: yönetim veritabanı yapılandırılmadı.");
