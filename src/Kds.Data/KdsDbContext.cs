using Kds.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kds.Data;

/// <summary>Metadata dünyasının DbContext'i (00-analiz §6). Çok-kiracılık: ITenantEntity
/// uygulayan varlıklara TenantId global query filter uygulanır; eklemede TenantId ve
/// denetim zaman damgaları otomatik doldurulur. Çalıştırma dünyası (Kds.Execution) bu
/// context'e ASLA referans veremez.</summary>
public sealed class KdsDbContext(DbContextOptions<KdsDbContext> secenekler, ITenantContext tenantBaglami)
    : DbContext(secenekler)
{
    private static readonly DateTimeOffset TohumTarihi = new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

    public DbSet<Tenant> Tenantlar => Set<Tenant>();
    public DbSet<User> Kullanicilar => Set<User>();
    public DbSet<Connection> Baglantilar => Set<Connection>();
    public DbSet<AuditRecord> DenetimKayitlari => Set<AuditRecord>();
    public DbSet<DataMap> Haritalar => Set<DataMap>();
    public DbSet<Report> Raporlar => Set<Report>();
    public DbSet<ReportFolder> RaporKlasorleri => Set<ReportFolder>();
    public DbSet<ReportFavorite> RaporFavorileri => Set<ReportFavorite>();
    public DbSet<Dashboard> Panolar => Set<Dashboard>();
    public DbSet<EmbedToken> GommeAnahtarlari => Set<EmbedToken>();
    /// <summary>S45 — müşteri ağına kurulan KDS Ajanı kayıtları (push tabanlı içe alım).</summary>
    public DbSet<Agent> Ajanlar => Set<Agent>();
    public DbSet<IngestJob> IceAlimIsleri => Set<IngestJob>();
    public DbSet<ReportSubscription> Abonelikler => Set<ReportSubscription>();
    /// <summary>S20 — eşik uyarıları: rapor değeri sınırı aşınca bildirim (aboneliğin kardeşi).</summary>
    public DbSet<Alert> Uyarilar => Set<Alert>();
    /// <summary>S22 — merkezi ölçüt kataloğu (tek doğruluk kaynağı; raporlar referansla kullanır).</summary>
    public DbSet<Metric> Metrikler => Set<Metric>();
    public DbSet<MetricVersion> MetrikSurumleri => Set<MetricVersion>();
    public DbSet<SystemSetting> SistemAyarlari => Set<SystemSetting>();
    /// <summary>RLS — satır-düzeyi güvenlik kuralları (kiracı içi daraltma).</summary>
    public DbSet<RowRule> SatirKurallari => Set<RowRule>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Tenant>(e =>
        {
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Plan).HasMaxLength(100);
            e.Property(x => x.MarkaRengi).HasMaxLength(7);           // #RRGGBB
            e.Property(x => x.LogoDataUri).HasMaxLength(400_000);    // ~300 KB'lık data: URI'ye yer
            e.HasQueryFilter(t => t.Id == tenantBaglami.CurrentTenantId);
        });

        model.Entity<User>(e =>
        {
            e.Property(x => x.KullaniciAdi).HasMaxLength(200).IsRequired();
            e.Property(x => x.EPosta).HasMaxLength(320);
            e.Property(x => x.AdSoyad).HasMaxLength(300);
            e.HasIndex(x => x.KeycloakId).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.KullaniciAdi });
            e.HasOne(x => x.Tenant)
                .WithMany(t => t.Kullanicilar)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(k => k.TenantId == tenantBaglami.CurrentTenantId);
        });

        model.Entity<Connection>(e =>
        {
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Sunucu).HasMaxLength(400).IsRequired();
            e.Property(x => x.Veritabani).HasMaxLength(200).IsRequired();
            e.Property(x => x.KullaniciAdi).HasMaxLength(200);
            e.Property(x => x.Mod).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.Ad });
            e.HasQueryFilter(b => b.TenantId == tenantBaglami.CurrentTenantId);
        });

        model.Entity<AuditRecord>(e =>
        {
            e.Property(x => x.Sql).IsRequired();
            e.Property(x => x.Hata).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.OlusturmaTarihi });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // Veri Haritası aggregate'i (S6): kök tenant-filtreli; alt varlıklar kök üzerinden yüklenir.
        model.Entity<DataMap>(e =>
        {
            e.ToTable("VeriHaritalari");
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Mod).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.Ad });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
            e.HasMany(x => x.Tablolar).WithOne().HasForeignKey(t => t.VeriHaritasiId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Iliskiler).WithOne().HasForeignKey(i => i.VeriHaritasiId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<DataMapTable>(e =>
        {
            e.ToTable("HaritaTablolar");
            e.Property(x => x.Sema).HasMaxLength(128).IsRequired();
            e.Property(x => x.HamAd).HasMaxLength(256).IsRequired();
            e.Property(x => x.DostcaAd).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Kolonlar).WithOne().HasForeignKey(k => k.HaritaTabloId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<DataMapColumn>(e =>
        {
            e.ToTable("HaritaKolonlar");
            e.Property(x => x.HamAd).HasMaxLength(256).IsRequired();
            e.Property(x => x.DostcaAd).HasMaxLength(200);
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.VeriTipi).HasMaxLength(60);
        });
        model.Entity<DataMapRelationship>(e =>
        {
            e.ToTable("HaritaIliskiler");
            e.Property(x => x.KaynakKolon).HasMaxLength(256).IsRequired();
            e.Property(x => x.HedefKolon).HasMaxLength(256).IsRequired();
        });

        /* S42 — Gömme anahtarları. Kimlik anlık görüntüsü ve izinli alan listesi metin olarak
         * tutulur: ikisi de tek bir anahtara ait, ayrı tabloya bölmenin sorgusal faydası yok.
         * Doğrulama sıcak yolda (her gömülü istek) çalışır → TenantId+IptalTarihi indekslenir.
         *
         * Query filter DİĞERLERİYLE AYNI: yönetim ekranı yalnız kendi kiracısının anahtarlarını
         * görür. Gömülü doğrulama ise kiracıyı HENÜZ bilmez (istekte oturum yok) — orada kayıt
         * bilerek `IgnoreQueryFilters` ile okunur ve TenantId token'dan öğrenilir; sonraki her
         * sorgu o kiracı bağlamında koşar. */
        model.Entity<EmbedToken>(e =>
        {
            e.ToTable("GommeAnahtarlari");
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.KimlikJson).HasMaxLength(4000).IsRequired();
            e.Property(x => x.IzinliAlanlar).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.IptalTarihi });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // S45 — KDS Ajanı (push tabanlı içe alım; anahtar deseni Gömme'yle aynı).
        model.Entity<Agent>(e =>
        {
            e.ToTable("Ajanlar");
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Ad });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // Raporlar (S10): rapor = sorgu modeli JSON'u + görselleştirme tercihi. Harita/klasör/sahip
        // YUMUŞAK referans (silinince rapor yaşar); yalnız favoriler rapora gerçek FK (cascade).
        model.Entity<Report>(e =>
        {
            e.ToTable("Raporlar");
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            // ModelJson/HamSql: tasarım türüne göre tam biri dolu (doğrulama handler+validator'da).
            e.Property(x => x.Gorsellestirme).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Paylasim).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaylasimRolleri).HasMaxLength(500);   // S27: virgüllü rol adları
            e.HasIndex(x => new { x.TenantId, x.Ad });
            e.HasIndex(x => new { x.TenantId, x.KlasorId });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
            e.HasMany(x => x.Favoriler).WithOne().HasForeignKey(f => f.RaporId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<ReportFolder>(e =>
        {
            e.ToTable("RaporKlasorleri");
            e.Property(x => x.Ad).HasMaxLength(120).IsRequired();
            e.Property(x => x.ErisimRolleri).HasMaxLength(500);   // S27: virgüllü rol adları
            e.HasIndex(x => new { x.TenantId, x.Ad }).IsUnique();
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });
        model.Entity<ReportFavorite>(e =>
        {
            e.ToTable("RaporFavorileri");
            e.HasIndex(x => new { x.RaporId, x.KullaniciId }).IsUnique();
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // Panolar (S11): pano = sıralı rapor id'leri (RaporlarJson) + paylaşım. Rapor/sahip yumuşak referans.
        model.Entity<Dashboard>(e =>
        {
            e.ToTable("Panolar");
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Paylasim).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaylasimRolleri).HasMaxLength(500);   // S27: virgüllü rol adları
            e.HasIndex(x => new { x.TenantId, x.Ad });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // İçe alım işleri (S12): seçili tablolar JSON'da; kaynak/depo yumuşak referans.
        model.Entity<IngestJob>(e =>
        {
            e.ToTable("IceAlimIsleri");
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Zamanlama).HasMaxLength(100);
            e.Property(x => x.SonDurum).HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.Ad });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // Rapor abonelikleri (S15 · Zamanlanmış Raporlar): rapor → cron/elle → biçim + alıcılar (JSON);
        // rapor/sahip yumuşak referans. Durum alanları son gönderimi tutar.
        model.Entity<ReportSubscription>(e =>
        {
            e.ToTable("RaporAbonelikleri");
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Bicim).HasMaxLength(10).IsRequired();
            e.Property(x => x.Zamanlama).HasMaxLength(100);
            e.Property(x => x.SonDurum).HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.Ad });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // S20 Uyarılar: rapor → eşik → bildirim. Karşılaştırma tanımı ve DURUM (Tetiklendi) burada
        // yaşar; bildirim yalnız durum değişince gider (gürültü uyarıyı işe yaramaz hâle getirir).
        model.Entity<Alert>(e =>
        {
            e.ToTable("Uyarilar");
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Kolon).HasMaxLength(200);
            e.Property(x => x.Ozet).HasMaxLength(20).IsRequired();
            e.Property(x => x.Op).HasMaxLength(20).IsRequired();
            // Para/oran eşikleri kayıpsız karşılaştırılsın diye decimal (double değil).
            e.Property(x => x.Esik).HasPrecision(38, 6);
            e.Property(x => x.SonDeger).HasPrecision(38, 6);
            e.Property(x => x.Zamanlama).HasMaxLength(100);
            e.Property(x => x.SonDurum).HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.Ad });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // S22 Metrikler: formül haritaya bağlı — harita silinirse metrik ÖKSÜZ kalmasın (kaskad).
        // Ad kiracı içinde benzersiz: "iki farklı Toplam Ciro" tam da bu özelliğin çözdüğü sorundur.
        model.Entity<Metric>(e =>
        {
            e.ToTable("Metrikler");
            e.Property(x => x.Ad).HasMaxLength(150).IsRequired();
            e.Property(x => x.Ifade).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Bicim).HasMaxLength(10).IsRequired();
            e.Property(x => x.Aciklama).HasMaxLength(500);
            e.HasOne<DataMap>().WithMany().HasForeignKey(x => x.VeriHaritasiId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TenantId, x.Ad }).IsUnique();
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // S30 Metrik sürüm geçmişi: her güncellemede ÖNCEKİ hâl buraya yazılır (satır = eski hâl).
        // Metrik silinirse geçmişi de gider — öksüz geçmişin alıcısı yok (kaskad).
        model.Entity<MetricVersion>(e =>
        {
            e.ToTable("MetrikSurumleri");
            e.Property(x => x.Ad).HasMaxLength(150).IsRequired();
            e.Property(x => x.Ifade).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Bicim).HasMaxLength(10).IsRequired();
            e.Property(x => x.Aciklama).HasMaxLength(500);
            e.HasOne<Metric>().WithMany().HasForeignKey(x => x.MetrikId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.MetrikId, x.SurumNo }).IsUnique();
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // S16/A3 Sistem Ayarları: BİLEREK tenant filtresi YOK — TenantId null satırlar kurulum
        // geneli ayardır ve her kiracıya görünmelidir; kiracıya özel ezme dolu TenantId ile
        // saklanır ve okuma katmanı (SistemAyarlari servisi) "kiracı varsa o, yoksa genel" der.
        model.Entity<SystemSetting>(e =>
        {
            e.ToTable("SistemAyarlari");
            e.Property(x => x.Anahtar).HasMaxLength(100).IsRequired();
            e.Property(x => x.Deger).HasMaxLength(4000);
            e.HasIndex(x => new { x.TenantId, x.Anahtar }).IsUnique();
        });

        // RLS — satır kuralları. Kiracıya aittir (query filter) ve bir HARİTA TABLOSUNA bağlıdır:
        // sorguda o tablo kullanılıyorsa kural devreye girer. Harita silinince kural öksüz kalmasın
        // diye Cascade; tablo tarafında NoAction — iki cascade yolu SQL Server'da döngü sayılır.
        model.Entity<RowRule>(e =>
        {
            e.ToTable("SatirKurallari");
            e.Property(x => x.Ad).HasMaxLength(150).IsRequired();
            e.Property(x => x.Kolon).HasMaxLength(128).IsRequired();
            e.Property(x => x.Deger).HasMaxLength(1000);
            e.Property(x => x.Roller).HasMaxLength(400);
            e.HasOne(x => x.VeriHaritasi).WithMany().HasForeignKey(x => x.VeriHaritasiId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.HaritaTablo).WithMany().HasForeignKey(x => x.HaritaTabloId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.TenantId, x.VeriHaritasiId });
            e.HasQueryFilter(x => x.TenantId == tenantBaglami.CurrentTenantId);
        });

        // Geliştirme tenant'ları — Keycloak realm'indeki tenant_id'lerle birebir (infra/kds-realm.json).
        model.Entity<Tenant>().HasData(
            new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Ad = "Deneme A.Ş.", Durum = TenantStatus.Aktif, OlusturmaTarihi = TohumTarihi },
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Ad = "Örnek Ltd.", Durum = TenantStatus.Aktif, OlusturmaTarihi = TohumTarihi });
    }

    public override int SaveChanges()
    {
        Stamp();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Stamp();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void Stamp()
    {
        var simdi = DateTimeOffset.Now;
        foreach (var giris in ChangeTracker.Entries<EntityBase>())
        {
            if (giris.State == EntityState.Added && giris.Entity.OlusturmaTarihi == default)
                giris.Entity.OlusturmaTarihi = simdi;
            if (giris.State == EntityState.Modified)
                giris.Entity.GuncellemeTarihi = simdi;
        }

        if (tenantBaglami.CurrentTenantId is Guid tenant)
        {
            foreach (var giris in ChangeTracker.Entries<ITenantEntity>())
            {
                if (giris.State == EntityState.Added && giris.Entity.TenantId == Guid.Empty)
                    giris.Entity.TenantId = tenant;
            }
        }
    }
}
