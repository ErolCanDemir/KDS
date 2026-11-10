using Kds.Contracts.Reports;

namespace Kds.Data.Entities;

/// <summary>Kayıtlı rapor (S10): İKİ tasarım türü — (a) tasarımcıda kurulan sorgu MODELİ (JSON AST;
/// açılınca güncel haritayla yeniden SQL'e çevrilir, 00-analiz §5), (b) SQL Editörü'nde yazılan
/// HAM SQL (rapor SQL ile de tasarlanabilsin; kayıtta ve her icrada SELECT-only bekçisinden geçer).
/// Model raporunda <see cref="HaritaId"/>+<see cref="ModelJson"/>, SQL raporunda
/// <see cref="BaglantiId"/>+<see cref="HamSql"/> doludur. Harita/bağlantı/sahip yumuşak referanstır
/// (silinse de rapor kalır; açılışta anlaşılır hatayla döner).</summary>
public class Report : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Ad { get; set; } = string.Empty;

    /// <summary>Düz klasör (v1: iç içe yok). Null = klasörsüz. Yumuşak referans.</summary>
    public Guid? KlasorId { get; set; }

    /// <summary>Modelin kurulduğu Veri Haritası (yumuşak referans). SQL raporunda null.</summary>
    public Guid? HaritaId { get; set; }

    /// <summary>Sorgu modeli (Kds.Contracts.QueryModel.QueryModel) JSON'u — camelCase + string enum.
    /// SQL raporunda null.</summary>
    public string? ModelJson { get; set; }

    /// <summary>SQL raporunun ham SELECT'i (model raporunda null).</summary>
    public string? HamSql { get; set; }

    /// <summary>SQL raporunun koşacağı bağlantı (yumuşak referans). Model raporunda null — bağlantı haritadan gelir.</summary>
    public Guid? BaglantiId { get; set; }

    public VisualizationType Gorsellestirme { get; set; }

    public ReportSharing Paylasim { get; set; }

    /// <summary>S27 — <see cref="ReportSharing.Roller"/> paylaşımında görebilecek rol adları
    /// (virgülle ayrılmış; <c>RowRule.Roller</c> ile aynı desen). Diğer paylaşımlarda null.</summary>
    public string? PaylasimRolleri { get; set; }

    /// <summary>Local <c>User.Id</c> (yumuşak referans). Özel raporu yalnız sahibi + admin görür.</summary>
    public Guid SahipId { get; set; }

    public List<ReportFavorite> Favoriler { get; set; } = [];
}

/// <summary>Report klasörü — düz liste (v1), tenant'a ait.</summary>
public class ReportFolder : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Ad { get; set; } = string.Empty;

    /// <summary>S27 — klasöre ROL erişimi (virgüllü rol adları; null = ek erişim yok). Kesişen
    /// roldeki kullanıcı, klasördeki ÖZEL OLMAYAN tüm raporları görür — "şu klasör şu rolün"
    /// senaryosunun tek kayıtla hâli. Özel raporlar her durumda yalnız sahibinindir.</summary>
    public string? ErisimRolleri { get; set; }
}

/// <summary>Kullanıcı-bazlı favori işareti (yıldız).</summary>
public class ReportFavorite : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RaporId { get; set; }
    /// <summary>Local <c>User.Id</c>.</summary>
    public Guid KullaniciId { get; set; }
}
