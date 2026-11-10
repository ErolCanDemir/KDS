using Kds.Contracts.QueryModel;

namespace Kds.Data.Entities;

/// <summary>Satır-düzeyi güvenlik (RLS) kuralı. Kiracı yalıtımı zaten var; bu, kiracının İÇİNDE
/// kullanıcı/rol bazlı daraltmadır: "bayi kullanıcısı yalnız kendi servisinin taleplerini görsün".
/// <para>Kural bir VERİ HARİTASI TABLOSUNA bağlıdır: sorguda o tablo kullanılıyorsa devreye girer,
/// kullanılmıyorsa yok sayılır (o tablodan zaten satır dönmez). Uygulama noktası sorgu ÜRETİMİdir —
/// kullanıcı kuralı kaldıramaz, filtresini VEYA'layarak atlatamaz (üretici kullanıcı filtresinin
/// tamamını paranteze alır ve kuralı VE ile ekler).</para>
/// <para><see cref="Roller"/> boşsa kural (admin hariç) HERKESE uygulanır; doluysa yalnız listedeki
/// rollere. Admin muaftır — yöneticinin veriyi bütün görmesi gerekir; bu bilinçli ve belgelidir.</para></summary>
public class RowRule : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Kuralın ait olduğu veri haritası (kural harita tablosuna bağlıdır).</summary>
    public Guid VeriHaritasiId { get; set; }
    public DataMap? VeriHaritasi { get; set; }

    /// <summary>Kısıtlanan harita tablosu.</summary>
    public Guid HaritaTabloId { get; set; }
    public DataMapTable? HaritaTablo { get; set; }

    /// <summary>Kısıtlama hangi kolon üzerinden yapılır (ör. ServisId).</summary>
    public string Kolon { get; set; } = string.Empty;

    public KarsilastirmaOp Op { get; set; } = KarsilastirmaOp.Esit;

    public KuralDegerKaynagi DegerKaynagi { get; set; } = KuralDegerKaynagi.TokenIddiasi;

    /// <summary>Kaynağa göre anlamı değişir: SabitDeger'de DEĞERİN kendisi (çoklu değer için
    /// virgülle ayrılır), TokenIddiasi'nda İDDİA ADI (ör. servis_kodu), KullaniciAdi/EPosta'da
    /// kullanılmaz.</summary>
    public string? Deger { get; set; }

    /// <summary>Virgülle ayrılmış rol adları; boş = admin dışında herkes.</summary>
    public string? Roller { get; set; }

    public bool Aktif { get; set; } = true;

    /// <summary>Yöneticiye hatırlatma ve hata mesajlarında görünen ad ("Bayi kapsamı" gibi).</summary>
    public string Ad { get; set; } = string.Empty;
}
