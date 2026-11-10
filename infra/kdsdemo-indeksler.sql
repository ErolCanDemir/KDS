-- KdsDemo FK/join indeksleri (test turu M7 devamı — "sorgu hızlı çalışsın").
-- Demo üretici script'i tabloları İNDEKSSİZ kurmuştu: 2M'lik tablolarda her join tam taramaya
-- düşüyor, referans sorgu soğukta 177 sn sürüyordu. Gerçek müşteri DB'lerinde FK indeksleri
-- genelde vardır — demo da aynı koşula getirildi. İdempotent (varsa atlar); veri DEĞİŞMEZ.
-- Uygulama: sqlcmd -S "(localdb)\MSSQLLocalDB" -d KdsDemo -i infra/kdsdemo-indeksler.sql

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Talepler_AracId' AND object_id = OBJECT_ID('talep.Talepler'))
    CREATE INDEX IX_Talepler_AracId ON talep.Talepler (AracId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Talepler_GetirenKisiId' AND object_id = OBJECT_ID('talep.Talepler'))
    CREATE INDEX IX_Talepler_GetirenKisiId ON talep.Talepler (GetirenKisiId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Talepler_ServisId' AND object_id = OBJECT_ID('talep.Talepler'))
    CREATE INDEX IX_Talepler_ServisId ON talep.Talepler (ServisId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Talepler_GetirmeSebebiId' AND object_id = OBJECT_ID('talep.Talepler'))
    CREATE INDEX IX_Talepler_GetirmeSebebiId ON talep.Talepler (GetirmeSebebiId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Talepler_TalepDurumuId' AND object_id = OBJECT_ID('talep.Talepler'))
    CREATE INDEX IX_Talepler_TalepDurumuId ON talep.Talepler (TalepDurumuId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TalepParcalari_TalepId' AND object_id = OBJECT_ID('talep.TalepParcalari'))
    CREATE INDEX IX_TalepParcalari_TalepId ON talep.TalepParcalari (TalepId) INCLUDE (ParcaId, Adet, Tutar);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TalepParcalari_ParcaId' AND object_id = OBJECT_ID('talep.TalepParcalari'))
    CREATE INDEX IX_TalepParcalari_ParcaId ON talep.TalepParcalari (ParcaId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Odemeler_TalepId' AND object_id = OBJECT_ID('finans.Odemeler'))
    CREATE INDEX IX_Odemeler_TalepId ON finans.Odemeler (TalepId) INCLUDE (Tutar);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Faturalar_TalepId' AND object_id = OBJECT_ID('finans.Faturalar'))
    CREATE INDEX IX_Faturalar_TalepId ON finans.Faturalar (TalepId) INCLUDE (GenelToplam);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KisiAdresleri_KisiId' AND object_id = OBJECT_ID('kisi.KisiAdresleri'))
    CREATE INDEX IX_KisiAdresleri_KisiId ON kisi.KisiAdresleri (KisiId) INCLUDE (AdresId, AktifMi);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KisiAdresleri_AdresId' AND object_id = OBJECT_ID('kisi.KisiAdresleri'))
    CREATE INDEX IX_KisiAdresleri_AdresId ON kisi.KisiAdresleri (AdresId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Adresler_IlceId' AND object_id = OBJECT_ID('adres.Adresler'))
    CREATE INDEX IX_Adresler_IlceId ON adres.Adresler (IlceId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Adresler_IlId' AND object_id = OBJECT_ID('adres.Adresler'))
    CREATE INDEX IX_Adresler_IlId ON adres.Adresler (IlId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Araclar_MarkaId' AND object_id = OBJECT_ID('arac.Araclar'))
    CREATE INDEX IX_Araclar_MarkaId ON arac.Araclar (MarkaId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Araclar_ModelId' AND object_id = OBJECT_ID('arac.Araclar'))
    CREATE INDEX IX_Araclar_ModelId ON arac.Araclar (ModelId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Araclar_TescilIlId' AND object_id = OBJECT_ID('arac.Araclar'))
    CREATE INDEX IX_Araclar_TescilIlId ON arac.Araclar (TescilIlId);

-- M5/M8 bileşik join deseni: ss.ServisId = t.ServisId AND ss.ParcaId = tp.ParcaId
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ServisStoklari_ServisId_ParcaId' AND object_id = OBJECT_ID('stok.ServisStoklari'))
    CREATE INDEX IX_ServisStoklari_ServisId_ParcaId ON stok.ServisStoklari (ServisId, ParcaId) INCLUDE (MevcutAdet);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ilceler_IlId' AND object_id = OBJECT_ID('ortak.Ilceler'))
    CREATE INDEX IX_Ilceler_IlId ON ortak.Ilceler (IlId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Modeller_MarkaId' AND object_id = OBJECT_ID('ortak.Modeller'))
    CREATE INDEX IX_Modeller_MarkaId ON ortak.Modeller (MarkaId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Kisiler_DogumYeriIlId' AND object_id = OBJECT_ID('kisi.Kisiler'))
    CREATE INDEX IX_Kisiler_DogumYeriIlId ON kisi.Kisiler (DogumYeriIlId);

/* =============================================================================
   S17 kapanışı — KALAN FK indeks açığının kapatılması (17 Ağu 2026)
   -----------------------------------------------------------------------------
   Yukarıdaki blok yalnız "referans sorgunun" join yolunu kapsıyordu. 15 örnek
   raporun tamamı tasarımcıyla kurulup çalıştırılınca SQL Server'ın kendi eksik-
   indeks kayıtları (sys.dm_db_missing_index_details) üç öneri verdi ve şema
   taraması, 10.000+ satırlı tablolarda İLK ANAHTARI o kolon OLMAYAN 51 FK kolonu
   buldu. Tasarımcı artık her kolondan filtre/join kurabildiği için (Y1-Y11), bir
   sonraki raporun hangi FK'yi kullanacağı bilinemez: açık FK BAZINDA kapatılır.

   Ölçüm (denemeden): darboğaz sanılan R1 aslında ~2,2 sn'de dönüyordu; 45 sn'lik
   zaman aşımları eşzamanlı koşumların yükündendi. Gerçek açık aşağıdakilerdi.
   ============================================================================= */

-- (a) SQL Server'ın ÖLÇÜLEN önerileri — kapsayan (INCLUDE'lu) hâlleriyle.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Randevular_KanalId_Kapsayan' AND object_id = OBJECT_ID('talep.Randevular'))
    CREATE INDEX IX_Randevular_KanalId_Kapsayan ON talep.Randevular (RandevuKanaliId) INCLUDE (RandevuDurumuId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AracHasarKayitlari_AracId_Kapsayan' AND object_id = OBJECT_ID('arac.AracHasarKayitlari'))
    CREATE INDEX IX_AracHasarKayitlari_AracId_Kapsayan ON arac.AracHasarKayitlari (AracId) INCLUDE (HasarTutari);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ServisMarkaYetkileri_ServisId_Kapsayan' AND object_id = OBJECT_ID('servis.ServisMarkaYetkileri'))
    CREATE INDEX IX_ServisMarkaYetkileri_ServisId_Kapsayan ON servis.ServisMarkaYetkileri (ServisId) INCLUDE (MarkaId);

-- (b) Kalan FK açığı: 10.000+ satırlı tablolarda ilk anahtarı o kolon olmayan HER FK kolonuna
--     indeks. Dinamik üretilir (yeni tablo/FK eklenirse script'i güncellemek gerekmez) ve
--     İDEMPOTENTtir: var olan atlanır. Ad deseni: IX_<Tablo>_<Kolon>_FK.
DECLARE @sql nvarchar(max) = N'', @ad sysname, @sema sysname, @tablo sysname, @kolon sysname;
DECLARE eksikler CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name, c.name
    FROM sys.foreign_keys f
    JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = f.object_id
    JOIN sys.tables t  ON t.object_id = f.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = fc.parent_column_id
    WHERE (SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = t.object_id AND p.index_id IN (0,1)) > 10000
      AND NOT EXISTS (SELECT 1 FROM sys.index_columns ic
                      WHERE ic.object_id = t.object_id AND ic.column_id = c.column_id AND ic.key_ordinal = 1)
    GROUP BY s.name, t.name, c.name;   -- çok kolonlu FK'de aynı kolon iki kez gelmesin
OPEN eksikler;
FETCH NEXT FROM eksikler INTO @sema, @tablo, @kolon;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @ad = N'IX_' + @tablo + N'_' + @kolon + N'_FK';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = @ad AND object_id = OBJECT_ID(QUOTENAME(@sema) + N'.' + QUOTENAME(@tablo)))
    BEGIN
        SET @sql = N'CREATE INDEX ' + QUOTENAME(@ad) + N' ON ' + QUOTENAME(@sema) + N'.' + QUOTENAME(@tablo)
                 + N' (' + QUOTENAME(@kolon) + N');';
        PRINT @sql;
        EXEC sys.sp_executesql @sql;
    END
    FETCH NEXT FROM eksikler INTO @sema, @tablo, @kolon;
END
CLOSE eksikler; DEALLOCATE eksikler;

-- İstatistikleri tazele: yeni indekslerden sonra plan seçimi güncel dağılıma göre yapılsın.
EXEC sys.sp_updatestats;
