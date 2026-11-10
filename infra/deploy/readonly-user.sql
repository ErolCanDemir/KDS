-- LST-KDS — başarı ölçütü #4, İKİNCİ KATMAN (read-only DB kullanıcısı).
-- Birinci katman: uygulamanın SELECT-only bekçisi (ScriptDom, fail-closed) — hiçbir yazma
-- komutu üretilmez/çalıştırılmaz. İkinci katman: KDS'in kaynak veritabanına bağlandığı SQL
-- kullanıcısı DB düzeyinde YALNIZ okuyabilir — bekçi bir gün atlansa bile yazma reddedilir.
-- Bu iki katman AYRI AYRI doğrulanır (bekçi: 28 test; bu katman: ReadOnlyKullaniciTests).
--
-- Kullanım: her RAPORLANACAK kaynak veritabanında bir kez çalıştırın; <PAROLA>'yı güçlü bir
-- değerle değiştirin; sonra bu kullanıcıyı LST-KDS "Bağlantılar" ekranına girin.

-- 1) Sunucu düzeyinde login (bir kez).
USE [master];
GO
IF SUSER_ID(N'kds_okuyucu') IS NULL
    CREATE LOGIN [kds_okuyucu] WITH PASSWORD = N'<PAROLA>', CHECK_POLICY = ON;
GO

-- 2) Her raporlanacak veritabanı için (<VeritabaniAdi>'nı değiştirin) — YALNIZ okuma rolü.
USE [<VeritabaniAdi>];
GO
IF USER_ID(N'kds_okuyucu') IS NULL
    CREATE USER [kds_okuyucu] FOR LOGIN [kds_okuyucu];
GO
ALTER ROLE [db_datareader] ADD MEMBER [kds_okuyucu];   -- okuma
-- YAZMA/DDL rolleri BİLİNÇLİ OLARAK verilmez:
--   db_datawriter / db_ddladmin / db_owner  →  YOK.
GO
