using Kds.Contracts.Connections;

namespace Kds.Data.Entities;

/// <summary>Bir kaynak veritabanı bağlantı profili (00-analiz §4). Parola Data Protection ile
/// şifreli saklanır (<see cref="SifreSifreli"/>); düz metin hiçbir yerde durmaz (§7).
/// <see cref="KullaniciAdi"/> boşsa entegre (Windows) kimlik doğrulama kullanılır.</summary>
public class Connection : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Ad { get; set; } = string.Empty;
    public string Sunucu { get; set; } = string.Empty;
    public string Veritabani { get; set; } = string.Empty;

    /// <summary>Salt-okunur kaynak kimliği. Boş = entegre kimlik doğrulama.</summary>
    public string? KullaniciAdi { get; set; }

    /// <summary>Data Protection ile şifreli parola (boş = parolasız/entegre).</summary>
    public string SifreSifreli { get; set; } = string.Empty;

    public DataMode Mod { get; set; }

    /// <summary>M22b — İçe Alım modunda depo HEDEFİ: dolu ise çekilen tablolar bu (Canlı modlu)
    /// bağlantının veritabanına yazılır ve bu kaynağın sorguları ORADAN okur; null = varsayılan
    /// (KDS depo sunucusunda <c>KdsDepo_&lt;tenant&gt;</c> otomatik). Yumuşak referans.</summary>
    public Guid? DepoBaglantiId { get; set; }

    /// <summary>S45 — dolu ise bu kaynağa SUNUCU DOĞRUDAN BAĞLANMAZ: içe alım işlerini müşteri
    /// ağındaki KDS Ajanı koşturur ve veriyi depoya iter. Gerçek bağlantı dizesi ajanın YEREL
    /// yapılandırmasında durur (bu kayıttaki sunucu/DB alanları bilgi amaçlıdır, parola boş
    /// bırakılır) — kimlik bilgisi müşteri ağından çıkmaz. Yumuşak referans.</summary>
    public Guid? AjanId { get; set; }

    // Son bağlantı denemesinin sonucu (liste ekranında durum çipi bunu gösterir).
    public bool? SonTestBasarili { get; set; }
    public int? SonTestMs { get; set; }
    public DateTimeOffset? SonTestTarihi { get; set; }
}
