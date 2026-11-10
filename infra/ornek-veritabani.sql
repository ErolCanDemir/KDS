/* =============================================================================
   KDS - Buyuk test veritabani: KdsDemo (arac / kisi / servis / talep domain'i)
   -----------------------------------------------------------------------------
   Amac : Gercekci, FK'li, MILYONLUK hacimli bir test kaynagi.
          - ~80 elle modellenmis tablo (lookup + cekirdek)
          - ~120 yillik ARSIV tablosu (TalepArsiv_2015... deseni) -> ~200 tablo
          - ~25 milyon satir (Kisiler 2M, Adresler 2.5M, Talepler 2M, ...)
   Veri  : DETERMINISTIK uretilir (mod aritmetigi; NEWID yok) - tekrarlanabilir.
           TC kimlik / plaka / VIN degerleri SAHTEDIR (gercek kisi verisi yok).
   Sure  : LocalDB'de ~10-25 dakika. Idempotent: her calistirmada bastan kurar.
   Kullanim: sqlcmd -S "(localdb)\MSSQLLocalDB" -i infra\ornek-veritabani.sql
   ============================================================================= */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF DB_ID(N'KdsDemo') IS NOT NULL
BEGIN
    ALTER DATABASE KdsDemo SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE KdsDemo;
END;
GO
-- On-boyutlandirma: milyonlarca satirda autogrow sancisini onler.
CREATE DATABASE KdsDemo;
ALTER DATABASE KdsDemo SET RECOVERY SIMPLE;
-- KRITIK (LocalDB): yeni DB'ler AUTO_CLOSE ON gelir. ~7.7 GB'lik bu DB bostayken kapanir; bir
-- sonraki BAGLANTI ac+kurtarma maliyeti 30 sn'lik komut zaman asimini asar (uygulama "Yurutme
-- Zaman Asimi" verir; editorde DB sicak oldugu icin hizli gorunur). OFF ile hep ONLINE kalir.
ALTER DATABASE KdsDemo SET AUTO_CLOSE OFF;
ALTER DATABASE KdsDemo MODIFY FILE (NAME = KdsDemo,     SIZE = 6144MB, FILEGROWTH = 512MB);
ALTER DATABASE KdsDemo MODIFY FILE (NAME = KdsDemo_log, SIZE = 1024MB, FILEGROWTH = 256MB);
GO
USE KdsDemo;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

RAISERROR('==> Sayi tablosu (tally, 3M)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.__Sayi (n INT NOT NULL PRIMARY KEY);
INSERT dbo.__Sayi (n)
SELECT TOP (3000000) ROW_NUMBER() OVER (ORDER BY (SELECT 1))
FROM master.sys.all_columns a CROSS JOIN master.sys.all_columns b;
GO

/* =====================================================================
   1) LOOKUP TABLOLARI
   ===================================================================== */
RAISERROR('==> Lookup tablolari...', 0, 1) WITH NOWAIT;

CREATE TABLE dbo.Iller (Id INT NOT NULL PRIMARY KEY, PlakaKodu CHAR(2) NOT NULL, Ad NVARCHAR(40) NOT NULL, BolgeAdi NVARCHAR(30) NOT NULL);
INSERT dbo.Iller (Id, PlakaKodu, Ad, BolgeAdi) VALUES
(1,'01',N'Adana',N'Akdeniz'),(2,'02',N'Adıyaman',N'Güneydoğu Anadolu'),(3,'03',N'Afyonkarahisar',N'Ege'),(4,'04',N'Ağrı',N'Doğu Anadolu'),(5,'05',N'Amasya',N'Karadeniz'),
(6,'06',N'Ankara',N'İç Anadolu'),(7,'07',N'Antalya',N'Akdeniz'),(8,'08',N'Artvin',N'Karadeniz'),(9,'09',N'Aydın',N'Ege'),(10,'10',N'Balıkesir',N'Marmara'),
(11,'11',N'Bilecik',N'Marmara'),(12,'12',N'Bingöl',N'Doğu Anadolu'),(13,'13',N'Bitlis',N'Doğu Anadolu'),(14,'14',N'Bolu',N'Karadeniz'),(15,'15',N'Burdur',N'Akdeniz'),
(16,'16',N'Bursa',N'Marmara'),(17,'17',N'Çanakkale',N'Marmara'),(18,'18',N'Çankırı',N'İç Anadolu'),(19,'19',N'Çorum',N'Karadeniz'),(20,'20',N'Denizli',N'Ege'),
(21,'21',N'Diyarbakır',N'Güneydoğu Anadolu'),(22,'22',N'Edirne',N'Marmara'),(23,'23',N'Elazığ',N'Doğu Anadolu'),(24,'24',N'Erzincan',N'Doğu Anadolu'),(25,'25',N'Erzurum',N'Doğu Anadolu'),
(26,'26',N'Eskişehir',N'İç Anadolu'),(27,'27',N'Gaziantep',N'Güneydoğu Anadolu'),(28,'28',N'Giresun',N'Karadeniz'),(29,'29',N'Gümüşhane',N'Karadeniz'),(30,'30',N'Hakkari',N'Doğu Anadolu'),
(31,'31',N'Hatay',N'Akdeniz'),(32,'32',N'Isparta',N'Akdeniz'),(33,'33',N'Mersin',N'Akdeniz'),(34,'34',N'İstanbul',N'Marmara'),(35,'35',N'İzmir',N'Ege'),
(36,'36',N'Kars',N'Doğu Anadolu'),(37,'37',N'Kastamonu',N'Karadeniz'),(38,'38',N'Kayseri',N'İç Anadolu'),(39,'39',N'Kırklareli',N'Marmara'),(40,'40',N'Kırşehir',N'İç Anadolu'),
(41,'41',N'Kocaeli',N'Marmara'),(42,'42',N'Konya',N'İç Anadolu'),(43,'43',N'Kütahya',N'Ege'),(44,'44',N'Malatya',N'Doğu Anadolu'),(45,'45',N'Manisa',N'Ege'),
(46,'46',N'Kahramanmaraş',N'Akdeniz'),(47,'47',N'Mardin',N'Güneydoğu Anadolu'),(48,'48',N'Muğla',N'Ege'),(49,'49',N'Muş',N'Doğu Anadolu'),(50,'50',N'Nevşehir',N'İç Anadolu'),
(51,'51',N'Niğde',N'İç Anadolu'),(52,'52',N'Ordu',N'Karadeniz'),(53,'53',N'Rize',N'Karadeniz'),(54,'54',N'Sakarya',N'Marmara'),(55,'55',N'Samsun',N'Karadeniz'),
(56,'56',N'Siirt',N'Güneydoğu Anadolu'),(57,'57',N'Sinop',N'Karadeniz'),(58,'58',N'Sivas',N'İç Anadolu'),(59,'59',N'Tekirdağ',N'Marmara'),(60,'60',N'Tokat',N'Karadeniz'),
(61,'61',N'Trabzon',N'Karadeniz'),(62,'62',N'Tunceli',N'Doğu Anadolu'),(63,'63',N'Şanlıurfa',N'Güneydoğu Anadolu'),(64,'64',N'Uşak',N'Ege'),(65,'65',N'Van',N'Doğu Anadolu'),
(66,'66',N'Yozgat',N'İç Anadolu'),(67,'67',N'Zonguldak',N'Karadeniz'),(68,'68',N'Aksaray',N'İç Anadolu'),(69,'69',N'Bayburt',N'Karadeniz'),(70,'70',N'Karaman',N'İç Anadolu'),
(71,'71',N'Kırıkkale',N'İç Anadolu'),(72,'72',N'Batman',N'Güneydoğu Anadolu'),(73,'73',N'Şırnak',N'Güneydoğu Anadolu'),(74,'74',N'Bartın',N'Karadeniz'),(75,'75',N'Ardahan',N'Doğu Anadolu'),
(76,'76',N'Iğdır',N'Doğu Anadolu'),(77,'77',N'Yalova',N'Marmara'),(78,'78',N'Karabük',N'Karadeniz'),(79,'79',N'Kilis',N'Güneydoğu Anadolu'),(80,'80',N'Osmaniye',N'Akdeniz'),
(81,'81',N'Düzce',N'Karadeniz');

CREATE TABLE dbo.Ilceler (Id INT NOT NULL PRIMARY KEY, IlId INT NOT NULL, Ad NVARCHAR(50) NOT NULL, NufusTahmini INT NOT NULL);
INSERT dbo.Ilceler (Id, IlId, Ad, NufusTahmini)
SELECT n, ((n - 1) / 12) + 1,
       CASE WHEN n % 12 = 1 THEN N'Merkez' ELSE N'İlçe ' + CAST(((n - 1) % 12) + 1 AS NVARCHAR(2)) END,
       15000 + (n * 7919) % 850000
FROM dbo.__Sayi WHERE n <= 81 * 12;
-- Ankara (Id 61-72) gercek ilce adlari (ornek raporlar Etimesgut kullanir). Etimesgut'un
-- Id 70 (ofset 9) secimi BILINCLI: adres uretimi (n*3)%12 yalniz 0/3/6/9 ofsetlerini
-- doldurur ve talep/arac mod siniflariyla kesisen tek yuva 9'dur (rapor bos donmesin).
UPDATE dbo.Ilceler SET Ad = CASE Id
 WHEN 61 THEN N'Çankaya' WHEN 62 THEN N'Keçiören' WHEN 63 THEN N'Yenimahalle' WHEN 64 THEN N'Polatlı'
 WHEN 65 THEN N'Mamak' WHEN 66 THEN N'Sincan' WHEN 67 THEN N'Altındağ' WHEN 68 THEN N'Pursaklar'
 WHEN 69 THEN N'Gölbaşı' WHEN 70 THEN N'Etimesgut' WHEN 71 THEN N'Çubuk' WHEN 72 THEN N'Beypazarı' END
WHERE Id BETWEEN 61 AND 72;

CREATE TABLE dbo.Markalar (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(40) NOT NULL, UlkeAdi NVARCHAR(30) NOT NULL, KurulusYili INT NOT NULL);
INSERT dbo.Markalar (Id, Ad, UlkeAdi, KurulusYili) VALUES
(1,N'Fiat',N'İtalya',1899),(2,N'Renault',N'Fransa',1899),(3,N'Toyota',N'Japonya',1937),(4,N'Volkswagen',N'Almanya',1937),(5,N'Ford',N'ABD',1903),
(6,N'Hyundai',N'Güney Kore',1967),(7,N'Opel',N'Almanya',1862),(8,N'Peugeot',N'Fransa',1810),(9,N'Citroen',N'Fransa',1919),(10,N'Dacia',N'Romanya',1966),
(11,N'Honda',N'Japonya',1948),(12,N'Nissan',N'Japonya',1933),(13,N'Skoda',N'Çekya',1895),(14,N'Seat',N'İspanya',1950),(15,N'Kia',N'Güney Kore',1944),
(16,N'BMW',N'Almanya',1916),(17,N'Mercedes-Benz',N'Almanya',1926),(18,N'Audi',N'Almanya',1909),(19,N'Volvo',N'İsveç',1927),(20,N'Mazda',N'Japonya',1920),
(21,N'Suzuki',N'Japonya',1909),(22,N'Mitsubishi',N'Japonya',1870),(23,N'Chevrolet',N'ABD',1911),(24,N'Tofaş',N'Türkiye',1968),(25,N'TOGG',N'Türkiye',2018),
(26,N'Tesla',N'ABD',2003),(27,N'MG',N'Çin',1924),(28,N'Chery',N'Çin',1997),(29,N'BYD',N'Çin',1995),(30,N'Cupra',N'İspanya',2018),
(31,N'Jeep',N'ABD',1941),(32,N'Land Rover',N'İngiltere',1948),(33,N'Mini',N'İngiltere',1959),(34,N'Porsche',N'Almanya',1931),(35,N'Lexus',N'Japonya',1989),
(36,N'Alfa Romeo',N'İtalya',1910),(37,N'DS',N'Fransa',2014),(38,N'Subaru',N'Japonya',1953),(39,N'SsangYong',N'Güney Kore',1954),(40,N'Isuzu',N'Japonya',1916);

CREATE TABLE dbo.Modeller (Id INT NOT NULL PRIMARY KEY, MarkaId INT NOT NULL, Ad NVARCHAR(60) NOT NULL, UretimBaslangicYili INT NOT NULL, SegmentKodu CHAR(1) NOT NULL);
INSERT dbo.Modeller (Id, MarkaId, Ad, UretimBaslangicYili, SegmentKodu)
SELECT n, ((n - 1) / 15) + 1,
       N'Model ' + CHAR(65 + ((n - 1) % 15)) + CAST(100 + (n * 37) % 800 AS NVARCHAR(4)),
       1998 + (n * 13) % 26,
       CHAR(65 + (n % 6))                                    -- A..F segment
FROM dbo.__Sayi WHERE n <= 40 * 15;

CREATE TABLE dbo.Renkler (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL, MetalikMi BIT NOT NULL);
INSERT dbo.Renkler VALUES (1,N'Beyaz',0),(2,N'Siyah',0),(3,N'Gri',1),(4,N'Gümüş',1),(5,N'Kırmızı',0),(6,N'Lacivert',1),(7,N'Mavi',1),(8,N'Yeşil',0),
(9,N'Kahverengi',1),(10,N'Bej',0),(11,N'Turuncu',0),(12,N'Sarı',0),(13,N'Bordo',1),(14,N'Füme',1),(15,N'Şampanya',1),(16,N'Antrasit',1);

CREATE TABLE dbo.YakitTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.YakitTipleri VALUES (1,N'Benzin'),(2,N'Dizel'),(3,N'LPG'),(4,N'Hibrit'),(5,N'Elektrik'),(6,N'Şarj Edilebilir Hibrit');

CREATE TABLE dbo.VitesTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.VitesTipleri VALUES (1,N'Manuel'),(2,N'Otomatik'),(3,N'Yarı Otomatik'),(4,N'CVT');

CREATE TABLE dbo.KasaTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.KasaTipleri VALUES (1,N'Sedan'),(2,N'Hatchback'),(3,N'SUV'),(4,N'Station Wagon'),(5,N'Coupe'),(6,N'Cabrio'),(7,N'MPV'),(8,N'Pick-up'),(9,N'Panelvan'),(10,N'Minibüs'),(11,N'Kamyonet'),(12,N'Crossover');

CREATE TABLE dbo.LastikMarkalari (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.LastikMarkalari VALUES (1,N'Michelin'),(2,N'Bridgestone'),(3,N'Goodyear'),(4,N'Continental'),(5,N'Pirelli'),(6,N'Lassa'),(7,N'Petlas'),(8,N'Hankook'),(9,N'Falken'),(10,N'Dunlop');

CREATE TABLE dbo.LastikEbatlari (Id INT NOT NULL PRIMARY KEY, Ebat NVARCHAR(20) NOT NULL, JantInc TINYINT NOT NULL);
INSERT dbo.LastikEbatlari (Id, Ebat, JantInc)
SELECT n, CAST(165 + (n % 12) * 10 AS NVARCHAR(3)) + N'/' + CAST(40 + (n % 6) * 5 AS NVARCHAR(2)) + N' R' + CAST(14 + (n % 7) AS NVARCHAR(2)), 14 + (n % 7)
FROM dbo.__Sayi WHERE n <= 40;

CREATE TABLE dbo.YagTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(20) NOT NULL);
INSERT dbo.YagTipleri VALUES (1,N'0W-20'),(2,N'0W-30'),(3,N'5W-30'),(4,N'5W-40'),(5,N'10W-40'),(6,N'15W-40');

CREATE TABLE dbo.Cinsiyetler (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(10) NOT NULL);
INSERT dbo.Cinsiyetler VALUES (1,N'Erkek'),(2,N'Kadın');

CREATE TABLE dbo.KanGruplari (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(10) NOT NULL);
INSERT dbo.KanGruplari VALUES (1,N'A Rh+'),(2,N'A Rh-'),(3,N'B Rh+'),(4,N'B Rh-'),(5,N'AB Rh+'),(6,N'AB Rh-'),(7,N'0 Rh+'),(8,N'0 Rh-');

CREATE TABLE dbo.MedeniDurumlar (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(20) NOT NULL);
INSERT dbo.MedeniDurumlar VALUES (1,N'Bekar'),(2,N'Evli'),(3,N'Boşanmış'),(4,N'Dul');

CREATE TABLE dbo.EgitimDurumlari (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.EgitimDurumlari VALUES (1,N'İlkokul'),(2,N'Ortaokul'),(3,N'Lise'),(4,N'Ön Lisans'),(5,N'Lisans'),(6,N'Yüksek Lisans'),(7,N'Doktora'),(8,N'Okur Yazar');

CREATE TABLE dbo.MeslekGruplari (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(50) NOT NULL);
INSERT dbo.MeslekGruplari (Id, Ad)
SELECT n, CASE n WHEN 1 THEN N'Eğitim' WHEN 2 THEN N'Sağlık' WHEN 3 THEN N'Mühendislik' WHEN 4 THEN N'Ticaret' WHEN 5 THEN N'İnşaat'
WHEN 6 THEN N'Ulaştırma' WHEN 7 THEN N'Tarım' WHEN 8 THEN N'Finans' WHEN 9 THEN N'Hukuk' WHEN 10 THEN N'Bilişim'
WHEN 11 THEN N'Turizm' WHEN 12 THEN N'Sanat' WHEN 13 THEN N'Güvenlik' WHEN 14 THEN N'Medya' WHEN 15 THEN N'Enerji'
WHEN 16 THEN N'Tekstil' WHEN 17 THEN N'Gıda' WHEN 18 THEN N'Otomotiv' WHEN 19 THEN N'Lojistik' ELSE N'Kamu' END
FROM dbo.__Sayi WHERE n <= 20;

CREATE TABLE dbo.Meslekler (Id INT NOT NULL PRIMARY KEY, MeslekGrubuId INT NOT NULL, Ad NVARCHAR(60) NOT NULL);
INSERT dbo.Meslekler (Id, MeslekGrubuId, Ad)
SELECT n, ((n - 1) / 20) + 1, N'Meslek ' + CAST(n AS NVARCHAR(4))
FROM dbo.__Sayi WHERE n <= 400;

CREATE TABLE dbo.EhliyetSiniflari (Id INT NOT NULL PRIMARY KEY, Kod NVARCHAR(5) NOT NULL, Aciklama NVARCHAR(60) NOT NULL);
INSERT dbo.EhliyetSiniflari VALUES (1,N'B',N'Otomobil'),(2,N'A2',N'Motosiklet'),(3,N'C',N'Kamyon'),(4,N'D',N'Otobüs'),(5,N'BE',N'Römorklu Otomobil'),(6,N'A1',N'Hafif Motosiklet'),(7,N'M',N'Moped'),(8,N'F',N'Traktör');

CREATE TABLE dbo.AdresTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(20) NOT NULL);
INSERT dbo.AdresTipleri VALUES (1,N'Ev'),(2,N'İş'),(3,N'Servis'),(4,N'Depo'),(5,N'Yazlık'),(6,N'Diğer');

CREATE TABLE dbo.EdinmeTurleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.EdinmeTurleri VALUES (1,N'Sıfır Alım'),(2,N'İkinci El'),(3,N'Miras'),(4,N'Hibe'),(5,N'Açık Artırma'),(6,N'Takas');

CREATE TABLE dbo.ServisTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(40) NOT NULL);
INSERT dbo.ServisTipleri VALUES (1,N'Yetkili Servis'),(2,N'Özel Servis'),(3,N'Yetkili Bayi + Servis'),(4,N'Hızlı Bakım Noktası'),(5,N'Ağır Vasıta Servisi'),(6,N'Kaporta/Boya Merkezi');

CREATE TABLE dbo.CalisanPozisyonlari (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(50) NOT NULL, TeknikMi BIT NOT NULL);
INSERT dbo.CalisanPozisyonlari VALUES (1,N'Servis Müdürü',0),(2,N'Servis Danışmanı',0),(3,N'Usta Teknisyen',1),(4,N'Teknisyen',1),(5,N'Çırak',1),
(6,N'Kaporta Ustası',1),(7,N'Boya Ustası',1),(8,N'Elektrik Ustası',1),(9,N'Yedek Parça Sorumlusu',0),(10,N'Muhasebe',0),(11,N'Kalite Kontrol',1),(12,N'Müşteri İlişkileri',0);

CREATE TABLE dbo.GetirmeSebepleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(60) NOT NULL, PlanliMi BIT NOT NULL);
INSERT dbo.GetirmeSebepleri VALUES
(1,N'Periyodik Bakım',1),(2,N'Motor Arızası',0),(3,N'Fren Sistemi Arızası',0),(4,N'Elektrik Arızası',0),(5,N'Klima Arızası',0),
(6,N'Kaza / Hasar Onarımı',0),(7,N'Lastik Değişimi',1),(8,N'Lastik Tamiri',0),(9,N'Boya / Kaporta',0),(10,N'Cam Değişimi',0),
(11,N'Akü Değişimi',0),(12,N'Egzoz Arızası',0),(13,N'Süspansiyon Arızası',0),(14,N'Şanzıman Arızası',0),(15,N'Debriyaj Arızası',0),
(16,N'Yağ Değişimi',1),(17,N'Muayene Öncesi Kontrol',1),(18,N'Ekspertiz',1),(19,N'Geri Çağırma Kampanyası',1),(20,N'Yazılım Güncelleme',1),
(21,N'Alarm / Güvenlik Sistemi',0),(22,N'Multimedya Arızası',0),(23,N'Yakıt Sistemi Arızası',0),(24,N'Soğutma Sistemi Arızası',0),(25,N'Direksiyon Arızası',0),
(26,N'Far / Aydınlatma',0),(27,N'Silecek / Fıskiye',0),(28,N'Döşeme / İç Aksam',0),(29,N'Anahtar / Kumanda',0),(30,N'Garanti Kontrolü',1),
(31,N'Sigorta Hasar Tespiti',0),(32,N'Aksesuar Montajı',1),(33,N'Çekici ile Geliş',0),(34,N'Ses / Titreşim Şikayeti',0),(35,N'Yol Yardım Sonrası',0),(36,N'Diğer',0);

CREATE TABLE dbo.TalepDurumlari (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(40) NOT NULL, SiraNo TINYINT NOT NULL, KapaliMi BIT NOT NULL);
INSERT dbo.TalepDurumlari VALUES
(1,N'Randevu Alındı',1,0),(2,N'Araç Kabul',2,0),(3,N'Ekspertiz / Arıza Tespiti',3,0),(4,N'Parça Bekleniyor',4,0),
(5,N'İşlemde',5,0),(6,N'Test / Kalite Kontrol',6,0),(7,N'Tamamlandı',7,0),(8,N'Teslim Edildi',8,1),(9,N'İptal',9,1);

CREATE TABLE dbo.RandevuDurumlari (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.RandevuDurumlari VALUES (1,N'Planlandı'),(2,N'Onaylandı'),(3,N'Geldi'),(4,N'Gelmedi'),(5,N'İptal'),(6,N'Ertelendi');

CREATE TABLE dbo.RandevuKanallari (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.RandevuKanallari VALUES (1,N'Telefon'),(2,N'Web'),(3,N'Mobil Uygulama'),(4,N'Şubeden'),(5,N'Çağrı Merkezi');

CREATE TABLE dbo.IslemKategorileri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(40) NOT NULL);
INSERT dbo.IslemKategorileri VALUES (1,N'Motor'),(2,N'Fren'),(3,N'Elektrik'),(4,N'Kaporta'),(5,N'Boya'),(6,N'Lastik/Jant'),(7,N'Klima'),(8,N'Şanzıman'),(9,N'Süspansiyon'),(10,N'Genel Bakım'),(11,N'Cam/Ayna'),(12,N'Döşeme');

CREATE TABLE dbo.IslemTipleri (Id INT NOT NULL PRIMARY KEY, IslemKategoriId INT NOT NULL, Ad NVARCHAR(80) NOT NULL, StandartSureDk INT NOT NULL, GarantiKapsaminaGirerMi BIT NOT NULL);
INSERT dbo.IslemTipleri (Id, IslemKategoriId, Ad, StandartSureDk, GarantiKapsaminaGirerMi)
SELECT n, ((n - 1) % 12) + 1,
       N'İşlem ' + CAST(n AS NVARCHAR(3)) + N' - ' + CASE (n % 4) WHEN 0 THEN N'Kontrol' WHEN 1 THEN N'Değişim' WHEN 2 THEN N'Onarım' ELSE N'Ayar' END,
       15 + (n * 7) % 300, CASE WHEN n % 3 = 0 THEN 1 ELSE 0 END
FROM dbo.__Sayi WHERE n <= 120;

CREATE TABLE dbo.ParcaKategorileri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(50) NOT NULL);
INSERT dbo.ParcaKategorileri (Id, Ad)
SELECT n, N'Parça Kategorisi ' + CAST(n AS NVARCHAR(3)) FROM dbo.__Sayi WHERE n <= 60;

CREATE TABLE dbo.Parcalar (Id INT NOT NULL PRIMARY KEY, ParcaKategoriId INT NOT NULL, OemKodu NVARCHAR(20) NOT NULL, Ad NVARCHAR(80) NOT NULL, ListeFiyati DECIMAL(12,2) NOT NULL, OrijinalMi BIT NOT NULL);
INSERT dbo.Parcalar (Id, ParcaKategoriId, OemKodu, Ad, ListeFiyati, OrijinalMi)
SELECT n, ((n - 1) % 60) + 1,
       N'OEM-' + RIGHT('000000' + CAST(n AS VARCHAR(6)), 6),
       N'Parça ' + CAST(n AS NVARCHAR(5)),
       CAST(80 + (n * 7919) % 24000 + ((n * 13) % 100) / 100.0 AS DECIMAL(12,2)),
       CASE WHEN n % 4 = 0 THEN 0 ELSE 1 END
FROM dbo.__Sayi WHERE n <= 5000;

CREATE TABLE dbo.OdemeTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.OdemeTipleri VALUES (1,N'Nakit'),(2,N'Kredi Kartı'),(3,N'Banka Kartı'),(4,N'Havale/EFT'),(5,N'Sigorta Ödemesi'),(6,N'Senet'),(7,N'Çek');

CREATE TABLE dbo.Bankalar (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(50) NOT NULL);
INSERT dbo.Bankalar (Id, Ad) SELECT n, N'Banka ' + CAST(n AS NVARCHAR(2)) FROM dbo.__Sayi WHERE n <= 15;

CREATE TABLE dbo.SigortaSirketleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(60) NOT NULL);
INSERT dbo.SigortaSirketleri (Id, Ad) SELECT n, N'Sigorta Şirketi ' + CAST(n AS NVARCHAR(2)) FROM dbo.__Sayi WHERE n <= 25;

CREATE TABLE dbo.PoliceTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL);
INSERT dbo.PoliceTipleri VALUES (1,N'Trafik Sigortası'),(2,N'Kasko'),(3,N'Mini Kasko'),(4,N'İMM');

CREATE TABLE dbo.HasarTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(40) NOT NULL);
INSERT dbo.HasarTipleri VALUES (1,N'Çarpışma'),(2,N'Park Halinde Hasar'),(3,N'Dolu Hasarı'),(4,N'Sel/Su Baskını'),(5,N'Yangın'),(6,N'Hırsızlık'),(7,N'Cam Kırılması'),(8,N'Vandalizm');

CREATE TABLE dbo.StokHareketTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL, GirisMi BIT NOT NULL);
INSERT dbo.StokHareketTipleri VALUES (1,N'Satın Alma',1),(2,N'Talepte Kullanım',0),(3,N'İade',1),(4,N'Sayım Fazlası',1),(5,N'Sayım Eksiği',0),(6,N'Transfer Giriş',1),(7,N'Transfer Çıkış',0),(8,N'Fire',0);

CREATE TABLE dbo.IzinTipleri (Id INT NOT NULL PRIMARY KEY, Ad NVARCHAR(30) NOT NULL, UcretliMi BIT NOT NULL);
INSERT dbo.IzinTipleri VALUES (1,N'Yıllık İzin',1),(2,N'Hastalık',1),(3,N'Ücretsiz İzin',0),(4,N'Doğum',1),(5,N'Evlilik',1),(6,N'Vefat',1),(7,N'İdari İzin',1);
GO

/* =====================================================================
   2) AD HAVUZLARI (gecici) - kisi/adres uretiminde kullanilir
   ===================================================================== */
CREATE TABLE dbo.__AdE (i INT PRIMARY KEY, Ad NVARCHAR(20));
INSERT dbo.__AdE VALUES (1,N'Ahmet'),(2,N'Mehmet'),(3,N'Mustafa'),(4,N'Ali'),(5,N'Hüseyin'),(6,N'Hasan'),(7,N'İbrahim'),(8,N'Osman'),(9,N'Yusuf'),(10,N'Murat'),
(11,N'Ömer'),(12,N'Ramazan'),(13,N'Halil'),(14,N'Süleyman'),(15,N'Abdullah'),(16,N'Mahmut'),(17,N'Recep'),(18,N'Salih'),(19,N'Kemal'),(20,N'Adem'),
(21,N'Emre'),(22,N'Burak'),(23,N'Serkan'),(24,N'Volkan'),(25,N'Tolga'),(26,N'Çağlar'),(27,N'Barış'),(28,N'Onur'),(29,N'Kerem'),(30,N'Efe'),
(31,N'Arda'),(32,N'Deniz'),(33,N'Cem'),(34,N'Selim'),(35,N'Tarık'),(36,N'Levent'),(37,N'Koray'),(38,N'Doğan'),(39,N'Erhan'),(40,N'Fatih');
CREATE TABLE dbo.__AdK (i INT PRIMARY KEY, Ad NVARCHAR(20));
INSERT dbo.__AdK VALUES (1,N'Fatma'),(2,N'Ayşe'),(3,N'Emine'),(4,N'Hatice'),(5,N'Zeynep'),(6,N'Elif'),(7,N'Meryem'),(8,N'Şerife'),(9,N'Zehra'),(10,N'Sultan'),
(11,N'Hanife'),(12,N'Merve'),(13,N'Esra'),(14,N'Fadime'),(15,N'Özlem'),(16,N'Yasemin'),(17,N'Sevim'),(18,N'Gül'),(19,N'Havva'),(20,N'Melek'),
(21,N'Büşra'),(22,N'Kübra'),(23,N'Tuğba'),(24,N'Derya'),(25,N'Seda'),(26,N'Gamze'),(27,N'Pınar'),(28,N'Burcu'),(29,N'Ece'),(30,N'Selin'),
(31,N'İrem'),(32,N'Ceren'),(33,N'Damla'),(34,N'Sibel'),(35,N'Nur'),(36,N'Aslı'),(37,N'Duygu'),(38,N'Eda'),(39,N'Melis'),(40,N'Naz');
CREATE TABLE dbo.__Soyad (i INT PRIMARY KEY, Ad NVARCHAR(25));
INSERT dbo.__Soyad VALUES (1,N'Yılmaz'),(2,N'Kaya'),(3,N'Demir'),(4,N'Şahin'),(5,N'Çelik'),(6,N'Yıldız'),(7,N'Yıldırım'),(8,N'Öztürk'),(9,N'Aydın'),(10,N'Özdemir'),
(11,N'Arslan'),(12,N'Doğan'),(13,N'Kılıç'),(14,N'Aslan'),(15,N'Çetin'),(16,N'Kara'),(17,N'Koç'),(18,N'Kurt'),(19,N'Özkan'),(20,N'Şimşek'),
(21,N'Polat'),(22,N'Korkmaz'),(23,N'Özcan'),(24,N'Erdoğan'),(25,N'Güneş'),(26,N'Aktaş'),(27,N'Turan'),(28,N'Bulut'),(29,N'Duman'),(30,N'Ateş'),
(31,N'Güler'),(32,N'Avcı'),(33,N'Yavuz'),(34,N'Sarı'),(35,N'Tekin'),(36,N'Ünal'),(37,N'Aksoy'),(38,N'Bozkurt'),(39,N'Keskin'),(40,N'Sezer'),
(41,N'Acar'),(42,N'Erdem'),(43,N'Tan'),(44,N'Uçar'),(45,N'Kaplan'),(46,N'Vural'),(47,N'Işık'),(48,N'Taş'),(49,N'Karaca'),(50,N'Alkan');
CREATE TABLE dbo.__Mahalle (i INT PRIMARY KEY, Ad NVARCHAR(40));
INSERT dbo.__Mahalle (i, Ad)
SELECT n, CASE (n % 20) WHEN 0 THEN N'Cumhuriyet' WHEN 1 THEN N'Atatürk' WHEN 2 THEN N'Yeni' WHEN 3 THEN N'Fatih' WHEN 4 THEN N'Yıldız'
WHEN 5 THEN N'Bahçelievler' WHEN 6 THEN N'Çamlık' WHEN 7 THEN N'Gazi' WHEN 8 THEN N'İstiklal' WHEN 9 THEN N'Zafer'
WHEN 10 THEN N'Barış' WHEN 11 THEN N'Esentepe' WHEN 12 THEN N'Güzelyalı' WHEN 13 THEN N'Karşıyaka' WHEN 14 THEN N'Sanayi'
WHEN 15 THEN N'Yeşiltepe' WHEN 16 THEN N'Kültür' WHEN 17 THEN N'Mimar Sinan' WHEN 18 THEN N'Turgut Özal' ELSE N'Mevlana' END
+ N' Mah. ' + CAST(((n - 1) / 20) + 1 AS NVARCHAR(3))
FROM dbo.__Sayi WHERE n <= 200;
GO

/* =====================================================================
   3) CEKIRDEK TABLOLAR - once tablolar, sonra yuk, en sonda index + FK
   ===================================================================== */
RAISERROR('==> Kisiler (2.000.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.Kisiler (
    Id INT NOT NULL PRIMARY KEY,
    TCKimlikNo CHAR(11) NOT NULL,               -- SAHTE (deterministik uretim)
    Ad NVARCHAR(25) NOT NULL,
    Soyad NVARCHAR(25) NOT NULL,
    CinsiyetId INT NOT NULL,
    DogumTarihi DATE NOT NULL,
    DogumYeriIlId INT NOT NULL,
    AnneAdi NVARCHAR(25) NOT NULL,
    BabaAdi NVARCHAR(25) NOT NULL,
    KanGrubuId INT NOT NULL,
    MedeniDurumId INT NOT NULL,
    EgitimDurumuId INT NOT NULL,
    MeslekId INT NOT NULL,
    CepTelefonu VARCHAR(15) NOT NULL,
    Eposta VARCHAR(60) NOT NULL,
    EhliyetNo VARCHAR(10) NULL,
    EhliyetSinifiId INT NULL,
    EhliyetTarihi DATE NULL,
    VergiNo VARCHAR(10) NULL,
    AktifMi BIT NOT NULL,
    KayitTarihi DATETIME2(0) NOT NULL
);
INSERT dbo.Kisiler
SELECT n,
    CAST(10000000000 + CAST(n AS BIGINT) * 37 AS CHAR(11)),
    CASE WHEN n % 2 = 1 THEN e.Ad ELSE k.Ad END,
    s.Ad,
    CASE WHEN n % 2 = 1 THEN 1 ELSE 2 END,
    DATEADD(DAY, -(6570 + (n * 11) % 16425), '2026-08-06'),          -- 18-63 yas
    ((n * 7) % 81) + 1,
    k2.Ad, e2.Ad,
    (n % 8) + 1, (n % 4) + 1, (n % 8) + 1, ((n * 13) % 400) + 1,
    '05' + RIGHT('000000000' + CAST(300000000 + (CAST(n AS BIGINT) * 17) % 599999999 AS VARCHAR(9)), 9),
    'kisi' + CAST(n AS VARCHAR(7)) + '@ornek.com',
    CASE WHEN n % 10 < 8 THEN RIGHT('0000000000' + CAST(n AS VARCHAR(7)), 6) ELSE NULL END,
    CASE WHEN n % 10 < 8 THEN (n % 8) + 1 ELSE NULL END,
    CASE WHEN n % 10 < 8 THEN DATEADD(DAY, -((n * 3) % 10950), '2026-08-06') ELSE NULL END,
    CASE WHEN n % 5 = 0 THEN RIGHT('0000000000' + CAST(CAST(n AS BIGINT) * 3 AS VARCHAR(10)), 10) ELSE NULL END,
    CASE WHEN n % 50 = 0 THEN 0 ELSE 1 END,
    DATEADD(MINUTE, -(n % 2600000), '2026-08-06')
FROM dbo.__Sayi
JOIN dbo.__AdE  e  ON e.i  = (n % 40) + 1
JOIN dbo.__AdK  k  ON k.i  = ((n * 3) % 40) + 1
JOIN dbo.__AdK  k2 ON k2.i = ((n * 7) % 40) + 1
JOIN dbo.__AdE  e2 ON e2.i = ((n * 11) % 40) + 1
JOIN dbo.__Soyad s ON s.i  = ((n * 13) % 50) + 1
WHERE n <= 2000000;
GO

RAISERROR('==> Araclar (1.500.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.Araclar (
    Id INT NOT NULL PRIMARY KEY,
    Plaka VARCHAR(10) NOT NULL,                 -- SAHTE
    MarkaId INT NOT NULL,
    ModelId INT NOT NULL,
    ModelYili SMALLINT NOT NULL,
    SasiNo CHAR(17) NOT NULL,                   -- SAHTE VIN
    MotorNo VARCHAR(14) NOT NULL,
    RenkId INT NOT NULL,
    YakitTipiId INT NOT NULL,
    VitesTipiId INT NOT NULL,
    KasaTipiId INT NOT NULL,
    MotorHacmiCc SMALLINT NOT NULL,
    MotorGucuHp SMALLINT NOT NULL,
    KoltukSayisi TINYINT NOT NULL,
    AgirlikKg SMALLINT NOT NULL,
    Kilometre INT NOT NULL,
    TescilTarihi DATE NOT NULL,
    TescilIlId INT NOT NULL,
    KullanimAmaci NVARCHAR(20) NOT NULL,        -- Hususi / Ticari / Resmi
    AktifMi BIT NOT NULL
);
INSERT dbo.Araclar
SELECT n,
    RIGHT('0' + CAST(((n * 7) % 81) + 1 AS VARCHAR(2)), 2)
        + CHAR(65 + (n % 23)) + CHAR(65 + ((n * 3) % 23)) + CHAR(65 + ((n * 5) % 23))
        + RIGHT('000' + CAST((n * 17) % 1000 AS VARCHAR(3)), 3),
    ((n * 11) % 40) + 1,
    ((((n * 11) % 40)) * 15) + ((n * 7) % 15) + 1,             -- markayla uyumlu model
    2005 + (n * 13) % 21,
    'VIN' + RIGHT(REPLICATE('0', 14) + CAST(CAST(n AS BIGINT) * 97 AS VARCHAR(14)), 14),
    'MTR' + RIGHT(REPLICATE('0', 11) + CAST(n AS VARCHAR(7)), 11),
    (n % 16) + 1, ((n * 3) % 6) + 1, ((n * 5) % 4) + 1, ((n * 7) % 12) + 1,
    998 + (n % 12) * 200,
    70 + (n * 3) % 330,
    CASE WHEN (n * 7) % 12 IN (7, 8, 9) THEN 8 ELSE 5 END,
    950 + (n * 11) % 1600,
    (n * 37) % 420000,
    DATEADD(DAY, -((n * 5) % 7665), '2026-08-06'),
    ((n * 7) % 81) + 1,
    CASE (n % 10) WHEN 0 THEN N'Ticari' WHEN 1 THEN N'Ticari' WHEN 2 THEN N'Resmi' ELSE N'Hususi' END,
    CASE WHEN n % 60 = 0 THEN 0 ELSE 1 END
FROM dbo.__Sayi WHERE n <= 1500000;
GO

RAISERROR('==> AracDetayBilgileri (1.500.000, 1:1)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.AracDetayBilgileri (
    AracId INT NOT NULL PRIMARY KEY,            -- 1:1
    BoyaKodu VARCHAR(8) NOT NULL,
    BoyaTipi NVARCHAR(20) NOT NULL,
    LastikEbatiId INT NOT NULL,
    LastikMarkasiId INT NOT NULL,
    JantTipi NVARCHAR(20) NOT NULL,
    YagTipiId INT NOT NULL,
    AkuAmper TINYINT NOT NULL,
    UzunlukMm SMALLINT NOT NULL,
    GenislikMm SMALLINT NOT NULL,
    YukseklikMm SMALLINT NOT NULL,
    BagajHacmiLt SMALLINT NOT NULL,
    YakitDepoLt TINYINT NOT NULL,
    AirbagSayisi TINYINT NOT NULL,
    AbsVarMi BIT NOT NULL,
    EspVarMi BIT NOT NULL,
    SunroofVarMi BIT NOT NULL,
    DeriKoltukVarMi BIT NOT NULL,
    NavigasyonVarMi BIT NOT NULL,
    GeriGorusKamerasiVarMi BIT NOT NULL,
    GarantiBitisTarihi DATE NULL,
    SonBakimTarihi DATE NULL,
    SonBakimKm INT NULL
);
INSERT dbo.AracDetayBilgileri
SELECT n,
    'B-' + RIGHT('0000' + CAST((n * 7) % 10000 AS VARCHAR(4)), 4),
    CASE (n % 4) WHEN 0 THEN N'Metalik' WHEN 1 THEN N'Opak' WHEN 2 THEN N'Sedefli' ELSE N'Mat' END,
    (n % 40) + 1, ((n * 3) % 10) + 1,
    CASE (n % 3) WHEN 0 THEN N'Çelik' WHEN 1 THEN N'Alaşım' ELSE N'Alaşım (Özel)' END,
    ((n * 5) % 6) + 1,
    50 + (n % 6) * 10,
    3900 + (n * 7) % 1300, 1650 + (n * 3) % 350, 1400 + (n * 5) % 500,
    280 + (n * 11) % 420, 40 + (n % 4) * 10,
    2 + (n % 8),
    1, CASE WHEN n % 8 = 0 THEN 0 ELSE 1 END,
    CASE WHEN n % 6 = 0 THEN 1 ELSE 0 END, CASE WHEN n % 5 = 0 THEN 1 ELSE 0 END,
    CASE WHEN n % 3 = 0 THEN 1 ELSE 0 END, CASE WHEN n % 4 = 0 THEN 1 ELSE 0 END,
    CASE WHEN n % 3 = 0 THEN DATEADD(DAY, (n % 1460), '2026-08-06') ELSE NULL END,
    DATEADD(DAY, -((n * 3) % 540), '2026-08-06'),
    (n * 37) % 400000
FROM dbo.__Sayi WHERE n <= 1500000;
GO

RAISERROR('==> Adresler (2.500.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.Adresler (
    Id INT NOT NULL PRIMARY KEY,
    AdresTipiId INT NOT NULL,
    IlId INT NOT NULL,
    IlceId INT NOT NULL,
    Mahalle NVARCHAR(50) NOT NULL,
    CaddeSokak NVARCHAR(60) NOT NULL,
    DisKapiNo VARCHAR(6) NOT NULL,
    IcKapiNo VARCHAR(6) NULL,
    PostaKodu CHAR(5) NOT NULL,
    SerbestAdres NVARCHAR(200) NULL,
    Enlem DECIMAL(9,6) NULL,
    Boylam DECIMAL(9,6) NULL,
    OlusturmaTarihi DATETIME2(0) NOT NULL
);
INSERT dbo.Adresler
SELECT n,
    CASE WHEN n <= 2000000 THEN CASE WHEN n % 5 = 0 THEN 2 ELSE 1 END ELSE CASE WHEN n % 7 = 0 THEN 4 ELSE 3 END END,
    ((n * 7) % 81) + 1,
    (((n * 7) % 81) * 12) + ((n * 3) % 12) + 1,               -- ile uyumlu ilce
    m.Ad,
    CASE (n % 3) WHEN 0 THEN N'Cadde ' ELSE N'Sokak ' END + CAST((n * 13) % 250 + 1 AS NVARCHAR(3)),
    CAST((n % 190) + 1 AS VARCHAR(3)),
    CASE WHEN n % 3 = 0 THEN NULL ELSE CAST((n % 40) + 1 AS VARCHAR(3)) END,
    RIGHT('0' + CAST(((n * 7) % 81) + 1 AS VARCHAR(2)), 2) + RIGHT('000' + CAST((n * 11) % 1000 AS VARCHAR(3)), 3),
    CASE WHEN n % 10 = 0 THEN N'Site içi, ' + CAST((n % 20) + 1 AS NVARCHAR(3)) + N'. blok yanı' ELSE NULL END,
    CAST(36.0 + ((n * 17) % 6000) / 1000.0 AS DECIMAL(9,6)),
    CAST(26.0 + ((n * 23) % 19000) / 1000.0 AS DECIMAL(9,6)),
    DATEADD(MINUTE, -(n % 2600000), '2026-08-06')
FROM dbo.__Sayi
JOIN dbo.__Mahalle m ON m.i = ((n * 3) % 200) + 1
WHERE n <= 2500000;
GO

RAISERROR('==> KisiAdresleri (2.600.000; kisi basina TEK aktif)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.KisiAdresleri (
    Id INT NOT NULL PRIMARY KEY,
    KisiId INT NOT NULL,
    AdresId INT NOT NULL,
    AktifMi BIT NOT NULL,
    BaslangicTarihi DATE NOT NULL,
    BitisTarihi DATE NULL,
    BeyanTuru NVARCHAR(20) NOT NULL             -- Ikametgah / Beyan / Is
);
-- kural: her kisinin TAM 1 aktif adresi (n<=2M aktif satirlar); 600k kisiye ek pasif adres
INSERT dbo.KisiAdresleri
SELECT n, n, n, 1,
    DATEADD(DAY, -((n * 3) % 2555), '2026-08-06'), NULL,
    CASE WHEN n % 6 = 0 THEN N'İş' ELSE N'İkametgah' END
FROM dbo.__Sayi WHERE n <= 2000000
UNION ALL
SELECT 2000000 + n, ((n * 7) % 2000000) + 1, 2000000 + ((n * 3) % 500000) + 1, 0,
    DATEADD(DAY, -(2555 + (n * 5) % 2555), '2026-08-06'),
    DATEADD(DAY, -((n * 3) % 2555), '2026-08-06'),
    N'Eski İkametgah'
FROM dbo.__Sayi WHERE n <= 600000;
GO

RAISERROR('==> KisiAraclari (1.800.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.KisiAraclari (
    Id INT NOT NULL PRIMARY KEY,
    KisiId INT NOT NULL,
    AracId INT NOT NULL,
    EdinmeTuruId INT NOT NULL,
    EdinmeTarihi DATE NOT NULL,
    DevirTarihi DATE NULL,
    NoterSatisNo VARCHAR(12) NULL,
    AlisBedeli DECIMAL(14,2) NULL,
    AktifMi BIT NOT NULL
);
-- her aracin 1 aktif sahibi (n<=1.5M); 300k arac icin eski sahiplik kaydi (pasif)
INSERT dbo.KisiAraclari
SELECT n, ((n * 13) % 2000000) + 1, n, (n % 6) + 1,
    DATEADD(DAY, -((n * 5) % 4380), '2026-08-06'), NULL,
    CASE WHEN n % 3 = 0 THEN 'NT' + RIGHT('0000000000' + CAST(n AS VARCHAR(7)), 10) ELSE NULL END,
    CASE WHEN n % 2 = 0 THEN CAST(150000 + (CAST(n AS BIGINT) * 977) % 2850000 AS DECIMAL(14,2)) ELSE NULL END,
    1
FROM dbo.__Sayi WHERE n <= 1500000
UNION ALL
SELECT 1500000 + n, ((n * 17) % 2000000) + 1, ((n * 7) % 1500000) + 1, 2,
    DATEADD(DAY, -(4380 + (n * 7) % 4380), '2026-08-06'),
    DATEADD(DAY, -((n * 5) % 4380), '2026-08-06'),
    'NT' + RIGHT('0000000000' + CAST(1500000 + n AS VARCHAR(7)), 10),
    CAST(80000 + (CAST(n AS BIGINT) * 577) % 1500000 AS DECIMAL(14,2)),
    0
FROM dbo.__Sayi WHERE n <= 300000;
GO

RAISERROR('==> Servisler + unvanlar + calisanlar...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.ServisUnvanlari (
    Id INT NOT NULL PRIMARY KEY,
    ResmiUnvan NVARCHAR(150) NOT NULL,
    KisaUnvan NVARCHAR(60) NOT NULL,
    TicaretSicilNo VARCHAR(10) NOT NULL,
    MersisNo VARCHAR(16) NOT NULL,
    TescilTarihi DATE NOT NULL
);
INSERT dbo.ServisUnvanlari
SELECT n,
    N'Oto Servis ' + CAST(n AS NVARCHAR(5)) + N' Otomotiv Sanayi ve Ticaret ' + CASE WHEN n % 3 = 0 THEN N'A.Ş.' ELSE N'Ltd. Şti.' END,
    N'Oto Servis ' + CAST(n AS NVARCHAR(5)),
    RIGHT('000000000' + CAST(n * 7 AS VARCHAR(9)), 9),
    RIGHT(REPLICATE('0', 16) + CAST(CAST(n AS BIGINT) * 1234567 AS VARCHAR(16)), 16),
    DATEADD(DAY, -((n * 11) % 10950), '2026-08-06')
FROM dbo.__Sayi WHERE n <= 30000;

CREATE TABLE dbo.Servisler (
    Id INT NOT NULL PRIMARY KEY,
    Kod VARCHAR(10) NOT NULL,
    Ad NVARCHAR(80) NOT NULL,
    ServisTipiId INT NOT NULL,
    UnvanId INT NOT NULL,
    AnaBayiMarkaId INT NULL,                    -- yetkili servislerde marka
    VergiNo VARCHAR(10) NOT NULL,
    VergiDairesi NVARCHAR(60) NOT NULL,
    Telefon VARCHAR(15) NOT NULL,
    Eposta VARCHAR(60) NOT NULL,
    WebAdresi VARCHAR(80) NULL,
    KurulusTarihi DATE NOT NULL,
    LiftSayisi TINYINT NOT NULL,
    GunlukAracKapasitesi SMALLINT NOT NULL,
    AktifMi BIT NOT NULL
);
INSERT dbo.Servisler
SELECT n,
    'SRV' + RIGHT('00000' + CAST(n AS VARCHAR(5)), 5),
    N'Oto Servis ' + CAST(n AS NVARCHAR(5)),
    (n % 6) + 1, n,
    CASE WHEN n % 6 IN (0, 2) THEN ((n * 3) % 40) + 1 ELSE NULL END,
    RIGHT('0000000000' + CAST(CAST(n AS BIGINT) * 11 AS VARCHAR(10)), 10),
    i.Ad + N' Vergi Dairesi',
    '02' + RIGHT('000000000' + CAST(120000000 + (n * 53) % 79999999 AS VARCHAR(9)), 9),
    'servis' + CAST(n AS VARCHAR(5)) + '@ornekservis.com',
    CASE WHEN n % 2 = 0 THEN 'www.ornekservis' + CAST(n AS VARCHAR(5)) + '.com' ELSE NULL END,
    DATEADD(DAY, -((n * 11) % 10950), '2026-08-06'),
    2 + (n % 12),
    10 + (n * 3) % 90,
    CASE WHEN n % 40 = 0 THEN 0 ELSE 1 END
FROM dbo.__Sayi
JOIN dbo.Iller i ON i.Id = ((n * 7) % 81) + 1
WHERE n <= 30000;

CREATE TABLE dbo.ServisAdresleri (
    Id INT NOT NULL PRIMARY KEY,
    ServisId INT NOT NULL,
    AdresId INT NOT NULL,
    KullanimTuru NVARCHAR(20) NOT NULL,         -- Merkez / Sube / Depo
    AktifMi BIT NOT NULL,
    BaslangicTarihi DATE NOT NULL
);
-- servis adresleri Adresler'in 2M sonrasindaki (servis tipi) bolgesinden
INSERT dbo.ServisAdresleri
SELECT n, ((n - 1) % 30000) + 1, 2000000 + ((n * 11) % 500000) + 1,
    CASE ((n - 1) / 30000) WHEN 0 THEN N'Merkez' WHEN 1 THEN N'Şube' ELSE N'Depo' END,
    CASE WHEN ((n - 1) / 30000) = 0 THEN 1 ELSE CASE WHEN n % 4 = 0 THEN 0 ELSE 1 END END,
    DATEADD(DAY, -((n * 7) % 7300), '2026-08-06')
FROM dbo.__Sayi WHERE n <= 60000;               -- her servise merkez + yarisina sube/depo

CREATE TABLE dbo.ServisMarkaYetkileri (
    Id INT NOT NULL PRIMARY KEY,
    ServisId INT NOT NULL,
    MarkaId INT NOT NULL,
    YetkiBaslangicTarihi DATE NOT NULL,
    YetkiBitisTarihi DATE NULL
);
INSERT dbo.ServisMarkaYetkileri
SELECT n, ((n - 1) % 30000) + 1, ((n * 13) % 40) + 1,
    DATEADD(DAY, -((n * 9) % 7300), '2026-08-06'),
    CASE WHEN n % 6 = 0 THEN DATEADD(DAY, (n % 720), '2026-08-06') ELSE NULL END
FROM dbo.__Sayi WHERE n <= 55000;

CREATE TABLE dbo.ServisCalisanlari (
    Id INT NOT NULL PRIMARY KEY,
    ServisId INT NOT NULL,
    KisiId INT NOT NULL,
    PozisyonId INT NOT NULL,
    SicilNo VARCHAR(10) NOT NULL,
    IseGirisTarihi DATE NOT NULL,
    IstenCikisTarihi DATE NULL,
    AylikBrutUcret DECIMAL(12,2) NOT NULL,
    AktifMi BIT NOT NULL
);
INSERT dbo.ServisCalisanlari
SELECT n, ((n * 7) % 30000) + 1, 2000000 - ((n * 3) % 1999999),
    ((n * 5) % 12) + 1,
    'P' + RIGHT('000000000' + CAST(n AS VARCHAR(6)), 9),
    DATEADD(DAY, -((n * 13) % 6570), '2026-08-06'),
    CASE WHEN n % 8 = 0 THEN DATEADD(DAY, -((n * 3) % 900), '2026-08-06') ELSE NULL END,
    CAST(26000 + (n * 331) % 74000 AS DECIMAL(12,2)),
    CASE WHEN n % 8 = 0 THEN 0 ELSE 1 END
FROM dbo.__Sayi WHERE n <= 300000;

CREATE TABLE dbo.ServisIslemUcretleri (
    Id INT NOT NULL PRIMARY KEY,
    ServisId INT NOT NULL,
    IslemTipiId INT NOT NULL,
    SaatlikIscilikUcreti DECIMAL(10,2) NOT NULL,
    SabitUcret DECIMAL(10,2) NULL,
    GecerlilikBaslangic DATE NOT NULL,
    GecerlilikBitis DATE NULL
);
INSERT dbo.ServisIslemUcretleri
SELECT n, ((n - 1) / 20) % 30000 + 1, ((n * 7) % 120) + 1,
    CAST(400 + (n * 13) % 1600 AS DECIMAL(10,2)),
    CASE WHEN n % 3 = 0 THEN CAST(250 + (n * 7) % 4750 AS DECIMAL(10,2)) ELSE NULL END,
    DATEADD(DAY, -((n * 3) % 1095), '2026-08-06'),
    CASE WHEN n % 5 = 0 THEN DATEADD(DAY, (n % 365), '2026-08-06') ELSE NULL END
FROM dbo.__Sayi WHERE n <= 600000;

CREATE TABLE dbo.ServisCalismaSaatleri (
    Id INT NOT NULL PRIMARY KEY,
    ServisId INT NOT NULL,
    GunNo TINYINT NOT NULL,                     -- 1=Pzt .. 7=Paz
    AcilisSaati TIME(0) NOT NULL,
    KapanisSaati TIME(0) NOT NULL,
    KapaliMi BIT NOT NULL
);
INSERT dbo.ServisCalismaSaatleri
SELECT n, ((n - 1) / 7) + 1, ((n - 1) % 7) + 1,
    CASE WHEN (n - 1) % 7 = 6 THEN '10:00' ELSE '08:30' END,
    CASE WHEN (n - 1) % 7 = 6 THEN '14:00' ELSE '18:30' END,
    CASE WHEN (n - 1) % 7 = 6 AND n % 3 <> 0 THEN 1 ELSE 0 END
FROM dbo.__Sayi WHERE n <= 30000 * 7;
GO

RAISERROR('==> Servis stoklari + hareketleri...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.ServisStoklari (
    Id INT NOT NULL PRIMARY KEY,
    ServisId INT NOT NULL,
    ParcaId INT NOT NULL,
    MevcutAdet INT NOT NULL,
    KritikAdet INT NOT NULL,
    RafKodu VARCHAR(10) NOT NULL,
    SonSayimTarihi DATE NOT NULL
);
-- NOT: veri yuklemesi TalepParcalari'ndan SONRA yapilir (kullanim-gudumlu: talepte
-- kullanilan her servis+parca ciftinin stok kaydi olur; raporlarda KalanStok dolu gelir).

CREATE TABLE dbo.StokHareketleri (
    Id INT NOT NULL PRIMARY KEY,
    ServisId INT NOT NULL,
    ParcaId INT NOT NULL,
    StokHareketTipiId INT NOT NULL,
    Adet INT NOT NULL,
    BirimMaliyet DECIMAL(12,2) NOT NULL,
    HareketTarihi DATETIME2(0) NOT NULL,
    BelgeNo VARCHAR(14) NULL
);
INSERT dbo.StokHareketleri
SELECT n, ((n * 13) % 30000) + 1, ((n * 29) % 5000) + 1, (n % 8) + 1,
    (n % 12) + 1,
    CAST(60 + (n * 331) % 18000 AS DECIMAL(12,2)),
    DATEADD(MINUTE, -(n % 1576800), '2026-08-06'),      -- son 3 yil
    CASE WHEN n % 4 = 0 THEN 'BLG' + RIGHT(REPLICATE('0', 11) + CAST(n AS VARCHAR(7)), 11) ELSE NULL END
FROM dbo.__Sayi WHERE n <= 1500000;
GO

RAISERROR('==> Randevular (1.000.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.Randevular (
    Id INT NOT NULL PRIMARY KEY,
    ServisId INT NOT NULL,
    AracId INT NOT NULL,
    KisiId INT NOT NULL,
    GetirmeSebebiId INT NOT NULL,
    RandevuDurumuId INT NOT NULL,
    RandevuKanaliId INT NOT NULL,
    RandevuTarihi DATETIME2(0) NOT NULL,
    OlusturmaTarihi DATETIME2(0) NOT NULL,
    NotMetni NVARCHAR(200) NULL
);
INSERT dbo.Randevular
-- KisiId'de dogrusal-olmayan terim (n/37): saf dogrusal mod zinciri, filtreli raporlarda
-- kisileri tek isim sinifina kilitliyordu ("hep Fatih" etkisi).
SELECT n, ((n * 7) % 30000) + 1, ((n * 11) % 1500000) + 1, ((CAST(n AS BIGINT) * 13 + n / 37) % 2000000) + 1,
    ((n * 3) % 36) + 1, (n % 6) + 1, (n % 5) + 1,
    DATEADD(MINUTE, -((n * 17) % 1576800) + 30 * (n % 20), '2026-08-06'),
    DATEADD(MINUTE, -((n * 17) % 1576800) - 1440 * ((n % 14) + 1), '2026-08-06'),
    CASE WHEN n % 6 = 0 THEN N'Müşteri bekleyecek, hızlı işlem rica edildi.' ELSE NULL END
FROM dbo.__Sayi WHERE n <= 1000000;
GO

RAISERROR('==> Talepler (2.000.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.Talepler (
    Id INT NOT NULL PRIMARY KEY,
    TalepNo VARCHAR(14) NOT NULL,
    ServisId INT NOT NULL,
    AracId INT NOT NULL,                        -- getirilen arac
    GetirenKisiId INT NOT NULL,                 -- getiren kisi
    GetirmeSebebiId INT NOT NULL,               -- lookup FK
    TalepDurumuId INT NOT NULL,                 -- lookup FK (randevu/bakimda/sonuclandi...)
    RandevuId INT NULL,
    DanismanCalisanId INT NOT NULL,
    GirisTarihi DATETIME2(0) NOT NULL,
    TahminiTeslimTarihi DATE NULL,
    CikisTarihi DATETIME2(0) NULL,
    GirisKm INT NOT NULL,
    SikayetNotu NVARCHAR(300) NULL,
    IscilikTutari DECIMAL(12,2) NOT NULL,
    ParcaTutari DECIMAL(12,2) NOT NULL,
    KdvTutari DECIMAL(12,2) NOT NULL,
    ToplamTutar DECIMAL(12,2) NOT NULL,
    GarantiKapsamindaMi BIT NOT NULL
);
INSERT dbo.Talepler
-- GetirenKisiId'de dogrusal-olmayan terim (n/41): saf dogrusal mod zinciri, marka/ilce gibi
-- filtreli raporlarda getiren kisileri tek isim sinifina kilitliyordu ("hep Fatih" etkisi).
SELECT n,
    'TLP' + RIGHT(REPLICATE('0', 11) + CAST(n AS VARCHAR(7)), 11),
    ((n * 7) % 30000) + 1,
    ((n * 11) % 1500000) + 1,
    ((CAST(n AS BIGINT) * 13 + n / 41) % 2000000) + 1,
    ((n * 3) % 36) + 1,
    CASE WHEN n % 10 < 6 THEN 8 WHEN n % 10 = 6 THEN 7 WHEN n % 10 = 7 THEN 5 WHEN n % 10 = 8 THEN (n % 4) + 1 ELSE 9 END,
    CASE WHEN n % 2 = 0 THEN ((n * 5) % 1000000) + 1 ELSE NULL END,
    ((n * 17) % 300000) + 1,
    DATEADD(MINUTE, -((n * 19) % 1576800), '2026-08-06'),
    DATEADD(DAY, ((n % 7) + 1), DATEADD(MINUTE, -((n * 19) % 1576800), '2026-08-06')),
    CASE WHEN n % 10 < 7 THEN DATEADD(MINUTE, 2880 + (n * 31) % 12960, DATEADD(MINUTE, -((n * 19) % 1576800), '2026-08-06')) ELSE NULL END,
    (n * 37) % 420000,
    CASE WHEN n % 4 = 0 THEN N'Motorda ses var, rölantide titriyor.' WHEN n % 4 = 1 THEN N'Fren pedalı sert, uyarı lambası yanıyor.' ELSE NULL END,
    CAST(500 + (n * 131) % 14500 AS DECIMAL(12,2)),
    CAST((n * 257) % 42000 AS DECIMAL(12,2)),
    CAST(((500 + (n * 131) % 14500) + ((n * 257) % 42000)) * 0.20 AS DECIMAL(12,2)),
    CAST(((500 + (n * 131) % 14500) + ((n * 257) % 42000)) * 1.20 AS DECIMAL(12,2)),
    CASE WHEN n % 6 = 0 THEN 1 ELSE 0 END
FROM dbo.__Sayi WHERE n <= 2000000;
GO

RAISERROR('==> TalepIslemleri (2.500.000) + TalepParcalari (2.000.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.TalepIslemleri (
    Id INT NOT NULL PRIMARY KEY,
    TalepId INT NOT NULL,
    IslemTipiId INT NOT NULL,
    TeknisyenCalisanId INT NOT NULL,
    IscilikSaat DECIMAL(5,2) NOT NULL,
    SaatlikUcret DECIMAL(10,2) NOT NULL,
    Tutar DECIMAL(12,2) NOT NULL,
    GarantiKapsamindaMi BIT NOT NULL,
    Aciklama NVARCHAR(150) NULL
);
INSERT dbo.TalepIslemleri
SELECT n, ((n * 11) % 2000000) + 1, ((n * 7) % 120) + 1, ((n * 23) % 300000) + 1,
    CAST(0.5 + (n % 12) * 0.5 AS DECIMAL(5,2)),
    CAST(400 + (n * 13) % 1600 AS DECIMAL(10,2)),
    CAST((0.5 + (n % 12) * 0.5) * (400 + (n * 13) % 1600) AS DECIMAL(12,2)),
    CASE WHEN n % 7 = 0 THEN 1 ELSE 0 END,
    CASE WHEN n % 5 = 0 THEN N'Standart prosedür uygulandı.' ELSE NULL END
FROM dbo.__Sayi WHERE n <= 2500000;

CREATE TABLE dbo.TalepParcalari (
    Id INT NOT NULL PRIMARY KEY,
    TalepId INT NOT NULL,
    ParcaId INT NOT NULL,
    Adet SMALLINT NOT NULL,
    BirimFiyat DECIMAL(12,2) NOT NULL,
    IskontoOrani DECIMAL(5,2) NOT NULL,
    Tutar DECIMAL(12,2) NOT NULL,
    GarantiKapsamindaMi BIT NOT NULL
);
INSERT dbo.TalepParcalari
-- ParcaId'de dogrusal-olmayan terim (n/29): 30000 carpani 5000'in kati oldugundan saf
-- dogrusal dagilim her servisi TEK parcaya kilitliyordu (stok eslesmesi 30k'ya dusuyordu).
SELECT n, ((n * 13) % 2000000) + 1, ((CAST(n AS BIGINT) * 31 + n / 29) % 5000) + 1,
    (n % 4) + 1,
    CAST(80 + (n * 331) % 24000 AS DECIMAL(12,2)),
    CAST((n % 4) * 5 AS DECIMAL(5,2)),
    CAST(((n % 4) + 1) * (80 + (n * 331) % 24000) * (1 - (n % 4) * 0.05) AS DECIMAL(12,2)),
    CASE WHEN n % 8 = 0 THEN 1 ELSE 0 END
FROM dbo.__Sayi WHERE n <= 2000000;
GO

RAISERROR('==> ServisStoklari (kullanim-gudumlu)...', 0, 1) WITH NOWAIT;
INSERT dbo.ServisStoklari (Id, ServisId, ParcaId, MevcutAdet, KritikAdet, RafKodu, SonSayimTarihi)
SELECT
    ROW_NUMBER() OVER (ORDER BY x.ServisId, x.ParcaId),
    x.ServisId, x.ParcaId,
    (x.ServisId * 7 + x.ParcaId * 13) % 180,
    5 + (x.ParcaId % 20),
    'RAF-' + CHAR(65 + (x.ParcaId % 20)) + RIGHT('00' + CAST((x.ServisId * 3) % 100 AS VARCHAR(2)), 2),
    DATEADD(DAY, -((x.ServisId + x.ParcaId) % 180), '2026-08-06')
FROM (SELECT DISTINCT t.ServisId, tp.ParcaId
      FROM dbo.TalepParcalari tp
      JOIN dbo.Talepler t ON t.Id = tp.TalepId) x;
GO

RAISERROR('==> TalepDurumGecmisi (2.500.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.TalepDurumGecmisi (
    Id INT NOT NULL PRIMARY KEY,
    TalepId INT NOT NULL,
    TalepDurumuId INT NOT NULL,
    DegisiklikTarihi DATETIME2(0) NOT NULL,
    DegistirenCalisanId INT NOT NULL,
    NotMetni NVARCHAR(100) NULL
);
INSERT dbo.TalepDurumGecmisi
SELECT n, ((n * 7) % 2000000) + 1, ((n * 3) % 9) + 1,
    DATEADD(MINUTE, -((n * 19) % 1576800) + (n % 9) * 240, '2026-08-06'),
    ((n * 29) % 300000) + 1,
    CASE WHEN n % 9 = 0 THEN N'Müşteri arandı, bilgi verildi.' ELSE NULL END
FROM dbo.__Sayi WHERE n <= 2500000;
GO

RAISERROR('==> Odemeler (2.000.000) + Faturalar (1.500.000)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.Odemeler (
    Id INT NOT NULL PRIMARY KEY,
    TalepId INT NOT NULL,
    OdemeTipiId INT NOT NULL,
    BankaId INT NULL,
    Tutar DECIMAL(12,2) NOT NULL,
    TaksitSayisi TINYINT NOT NULL,
    OdemeTarihi DATETIME2(0) NOT NULL,
    MakbuzNo VARCHAR(14) NULL
);
INSERT dbo.Odemeler
SELECT n, ((n * 17) % 2000000) + 1, (n % 7) + 1,
    CASE WHEN n % 7 IN (1, 2, 3) THEN ((n * 5) % 15) + 1 ELSE NULL END,
    CAST(300 + (n * 197) % 49700 AS DECIMAL(12,2)),
    CASE WHEN n % 7 = 1 THEN CAST((n % 4) * 3 + 1 AS TINYINT) ELSE 1 END,
    DATEADD(MINUTE, -((n * 19) % 1576800) + 4320, '2026-08-06'),
    CASE WHEN n % 2 = 0 THEN 'MKB' + RIGHT(REPLICATE('0', 11) + CAST(n AS VARCHAR(7)), 11) ELSE NULL END
FROM dbo.__Sayi WHERE n <= 2000000;

CREATE TABLE dbo.Faturalar (
    Id INT NOT NULL PRIMARY KEY,
    TalepId INT NOT NULL,
    FaturaNo VARCHAR(16) NOT NULL,
    FaturaTarihi DATE NOT NULL,
    MatrahTutar DECIMAL(12,2) NOT NULL,
    KdvTutar DECIMAL(12,2) NOT NULL,
    GenelToplam DECIMAL(12,2) NOT NULL,
    EFaturaMi BIT NOT NULL,
    IptalMi BIT NOT NULL
);
INSERT dbo.Faturalar
SELECT n, ((n * 23) % 2000000) + 1,
    'FTR' + CAST(2023 + n % 4 AS VARCHAR(4)) + RIGHT(REPLICATE('0', 9) + CAST(n AS VARCHAR(7)), 9),
    DATEADD(DAY, -((n * 13) % 1095), '2026-08-06'),
    CAST(500 + (n * 173) % 49500 AS DECIMAL(12,2)),
    CAST((500 + (n * 173) % 49500) * 0.20 AS DECIMAL(12,2)),
    CAST((500 + (n * 173) % 49500) * 1.20 AS DECIMAL(12,2)),
    CASE WHEN n % 5 = 0 THEN 0 ELSE 1 END,
    CASE WHEN n % 100 = 0 THEN 1 ELSE 0 END
FROM dbo.__Sayi WHERE n <= 1500000;
GO

RAISERROR('==> Arac gecmis tablolari (muayene/sigorta/km/hasar)...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.AracMuayeneleri (
    Id INT NOT NULL PRIMARY KEY,
    AracId INT NOT NULL,
    MuayeneTarihi DATE NOT NULL,
    GecerlilikTarihi DATE NOT NULL,
    SonucDurumu NVARCHAR(20) NOT NULL,          -- Gecti / Agir Kusur / Hafif Kusur
    UcretTutari DECIMAL(10,2) NOT NULL
);
INSERT dbo.AracMuayeneleri
SELECT n, ((n * 7) % 1500000) + 1,
    DATEADD(DAY, -((n * 11) % 1460), '2026-08-06'),
    DATEADD(DAY, 730 - ((n * 11) % 1460), '2026-08-06'),
    CASE (n % 10) WHEN 0 THEN N'Ağır Kusur' WHEN 1 THEN N'Hafif Kusur' WHEN 2 THEN N'Hafif Kusur' ELSE N'Geçti' END,
    CAST(1200 + (n % 5) * 150 AS DECIMAL(10,2))
FROM dbo.__Sayi WHERE n <= 1500000;

CREATE TABLE dbo.AracSigortaPoliceleri (
    Id INT NOT NULL PRIMARY KEY,
    AracId INT NOT NULL,
    SigortaSirketiId INT NOT NULL,
    PoliceTipiId INT NOT NULL,
    PoliceNo VARCHAR(14) NOT NULL,
    BaslangicTarihi DATE NOT NULL,
    BitisTarihi DATE NOT NULL,
    PrimTutari DECIMAL(12,2) NOT NULL,
    AktifMi BIT NOT NULL
);
INSERT dbo.AracSigortaPoliceleri
SELECT n, ((n * 13) % 1500000) + 1, ((n * 5) % 25) + 1, (n % 4) + 1,
    'PLC' + RIGHT(REPLICATE('0', 11) + CAST(n AS VARCHAR(7)), 11),
    DATEADD(DAY, -((n * 7) % 730), '2026-08-06'),
    DATEADD(DAY, 365 - ((n * 7) % 730), '2026-08-06'),
    CAST(2500 + (n * 397) % 55000 AS DECIMAL(12,2)),
    CASE WHEN ((n * 7) % 730) < 365 THEN 1 ELSE 0 END
FROM dbo.__Sayi WHERE n <= 1800000;

CREATE TABLE dbo.AracKilometreOkumalari (
    Id INT NOT NULL PRIMARY KEY,
    AracId INT NOT NULL,
    OkumaTarihi DATE NOT NULL,
    Kilometre INT NOT NULL,
    OkumaKaynagi NVARCHAR(20) NOT NULL          -- Servis / Muayene / Beyan
);
INSERT dbo.AracKilometreOkumalari
SELECT n, ((n * 11) % 1500000) + 1,
    DATEADD(DAY, -((n * 5) % 1825), '2026-08-06'),
    (n * 29) % 400000,
    CASE (n % 3) WHEN 0 THEN N'Servis' WHEN 1 THEN N'Muayene' ELSE N'Beyan' END
FROM dbo.__Sayi WHERE n <= 2000000;

CREATE TABLE dbo.AracHasarKayitlari (
    Id INT NOT NULL PRIMARY KEY,
    AracId INT NOT NULL,
    HasarTipiId INT NOT NULL,
    HasarTarihi DATE NOT NULL,
    HasarTutari DECIMAL(12,2) NOT NULL,
    SigortaKarsiladiMi BIT NOT NULL,
    Aciklama NVARCHAR(150) NULL
);
INSERT dbo.AracHasarKayitlari
SELECT n, ((n * 17) % 1500000) + 1, (n % 8) + 1,
    DATEADD(DAY, -((n * 13) % 2555), '2026-08-06'),
    CAST(1500 + (n * 577) % 148500 AS DECIMAL(12,2)),
    CASE WHEN n % 3 = 0 THEN 0 ELSE 1 END,
    CASE WHEN n % 4 = 0 THEN N'Sağ ön çamurluk ve tampon hasarı.' ELSE NULL END
FROM dbo.__Sayi WHERE n <= 800000;
GO

RAISERROR('==> Calisan izinleri...', 0, 1) WITH NOWAIT;
CREATE TABLE dbo.CalisanIzinleri (
    Id INT NOT NULL PRIMARY KEY,
    CalisanId INT NOT NULL,
    IzinTipiId INT NOT NULL,
    BaslangicTarihi DATE NOT NULL,
    BitisTarihi DATE NOT NULL,
    GunSayisi TINYINT NOT NULL,
    OnaylandiMi BIT NOT NULL
);
INSERT dbo.CalisanIzinleri
SELECT n, ((n * 7) % 300000) + 1, (n % 7) + 1,
    DATEADD(DAY, -((n * 11) % 1095), '2026-08-06'),
    DATEADD(DAY, -((n * 11) % 1095) + (n % 14) + 1, '2026-08-06'),
    (n % 14) + 1,
    CASE WHEN n % 12 = 0 THEN 0 ELSE 1 END
FROM dbo.__Sayi WHERE n <= 900000;
GO

/* =====================================================================
   4) INDEXLER (yuk sonrasi — cok daha hizli)
   ===================================================================== */
RAISERROR('==> Indexler...', 0, 1) WITH NOWAIT;
CREATE UNIQUE INDEX UX_Kisiler_TC ON dbo.Kisiler(TCKimlikNo);
CREATE UNIQUE INDEX UX_Araclar_Sasi ON dbo.Araclar(SasiNo);
CREATE INDEX IX_Araclar_Plaka ON dbo.Araclar(Plaka);
CREATE INDEX IX_Araclar_Marka ON dbo.Araclar(MarkaId, ModelId);
CREATE INDEX IX_KisiAdresleri_Kisi ON dbo.KisiAdresleri(KisiId, AktifMi);
CREATE INDEX IX_KisiAraclari_Kisi ON dbo.KisiAraclari(KisiId, AktifMi);
CREATE INDEX IX_KisiAraclari_Arac ON dbo.KisiAraclari(AracId, AktifMi);
CREATE INDEX IX_Talepler_Servis ON dbo.Talepler(ServisId, GirisTarihi);
CREATE INDEX IX_Talepler_Arac ON dbo.Talepler(AracId);
CREATE INDEX IX_Talepler_Durum ON dbo.Talepler(TalepDurumuId);
CREATE INDEX IX_TalepIslemleri_Talep ON dbo.TalepIslemleri(TalepId);
CREATE INDEX IX_TalepParcalari_Talep ON dbo.TalepParcalari(TalepId);
CREATE INDEX IX_TalepDurumGecmisi_Talep ON dbo.TalepDurumGecmisi(TalepId);
CREATE INDEX IX_Odemeler_Talep ON dbo.Odemeler(TalepId);
CREATE INDEX IX_Faturalar_Talep ON dbo.Faturalar(TalepId);
CREATE INDEX IX_Randevular_Servis ON dbo.Randevular(ServisId, RandevuTarihi);
CREATE INDEX IX_StokHareketleri_Servis ON dbo.StokHareketleri(ServisId, HareketTarihi);
CREATE INDEX IX_AracKmOkumalari_Arac ON dbo.AracKilometreOkumalari(AracId, OkumaTarihi);
-- Rapor destek indexleri (ilce->adres->kisi->talep zinciri uygulama timeout'una sigsin)
CREATE INDEX IX_Adresler_Ilce ON dbo.Adresler(IlceId);
CREATE INDEX IX_KisiAdresleri_Adres ON dbo.KisiAdresleri(AdresId);
CREATE INDEX IX_Talepler_GetirenKisi ON dbo.Talepler(GetirenKisiId);
-- COLUMNSTORE (BI cekirdegi — KRITIK): LocalDB/Express bellek tavani ~1.6 GB, DB ~7.7 GB.
-- GROUP BY + SUM/COUNT + ORDER BY(aggregate) desenli rapor sorgulari buyuk tablolari TAM tarar;
-- satir-deposunda soguk onbellekte bu GB'larca disk okumasi = uygulama zaman asimi. Olculdu:
-- Talepler ciro raporu 30.000+ ms -> 309 ms; Markalar raporu (2M Talepler x 1.5M Araclar join)
-- 39.170 ms -> 923 ms SOGUK (42x). Bu yuzden rapor sorgularinin dokundugu TUM buyuk tablolara
-- sayisal/tarih kolonlarini kapsayan NCCI kurulur (sikistirilmis segmentler tavana rahat sigar).
-- Metin kolonlari bilerek disarida: kisi-bazli detay sorgulari rowstore seek ile zaten hizli.
-- D4 BAKIM NOTU: raporlara yeni bir sayisal/tarih kolonu girecekse ILGILI NCCI'ye eklemeyi
-- unutma - kolon listede yoksa optimizer o sorguda columnstore'u KULLANAMAZ (sessiz yavaslama).
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Talepler_Rapor
ON dbo.Talepler (Id, ServisId, AracId, GetirenKisiId, GetirmeSebebiId, TalepDurumuId,
                 GirisTarihi, IscilikTutari, ParcaTutari, KdvTutari, ToplamTutar);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Araclar_Rapor ON dbo.Araclar
  (Id, MarkaId, ModelId, ModelYili, RenkId, YakitTipiId, VitesTipiId, KasaTipiId,
   MotorHacmiCc, MotorGucuHp, Kilometre, TescilTarihi, TescilIlId, AktifMi);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_AracDetay_Rapor ON dbo.AracDetayBilgileri
  (AracId, LastikEbatiId, LastikMarkasiId, YagTipiId, AkuAmper, BagajHacmiLt, YakitDepoLt,
   AirbagSayisi, GarantiBitisTarihi, SonBakimTarihi, SonBakimKm);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Kisiler_Rapor ON dbo.Kisiler
  (Id, CinsiyetId, DogumTarihi, DogumYeriIlId, MedeniDurumId, EgitimDurumuId, MeslekId, AktifMi, KayitTarihi);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Adresler_Rapor ON dbo.Adresler
  (Id, AdresTipiId, IlId, IlceId, OlusturmaTarihi);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_KisiAdresleri_Rapor ON dbo.KisiAdresleri
  (Id, KisiId, AdresId, AktifMi, BaslangicTarihi, BitisTarihi);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_TalepIslemleri_Rapor ON dbo.TalepIslemleri
  (Id, TalepId, IslemTipiId, TeknisyenCalisanId, IscilikSaat, SaatlikUcret, Tutar, GarantiKapsamindaMi);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_TalepParcalari_Rapor ON dbo.TalepParcalari
  (Id, TalepId, ParcaId, Adet, BirimFiyat, IskontoOrani, Tutar, GarantiKapsamindaMi);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Faturalar_Rapor ON dbo.Faturalar
  (Id, TalepId, FaturaTarihi, MatrahTutar, KdvTutar, GenelToplam, EFaturaMi, IptalMi);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Odemeler_Rapor ON dbo.Odemeler
  (Id, TalepId, OdemeTipiId, BankaId, Tutar, TaksitSayisi, OdemeTarihi);
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_AracSigorta_Rapor ON dbo.AracSigortaPoliceleri
  (Id, AracId, SigortaSirketiId, PoliceTipiId, BaslangicTarihi, BitisTarihi, PrimTutari, AktifMi);
GO

/* =====================================================================
   5) FOREIGN KEY'LER (yuk sonrasi; buyuk tablolarda NOCHECK - kesif icin
      sys.foreign_keys'te gorunmesi yeterli, Veri Haritasi FK onerisi calisir)
   ===================================================================== */
RAISERROR('==> Foreign key''ler...', 0, 1) WITH NOWAIT;
ALTER TABLE dbo.Ilceler   ADD CONSTRAINT FK_Ilceler_Il FOREIGN KEY (IlId) REFERENCES dbo.Iller(Id);
ALTER TABLE dbo.Modeller  ADD CONSTRAINT FK_Modeller_Marka FOREIGN KEY (MarkaId) REFERENCES dbo.Markalar(Id);
ALTER TABLE dbo.Meslekler ADD CONSTRAINT FK_Meslekler_Grup FOREIGN KEY (MeslekGrubuId) REFERENCES dbo.MeslekGruplari(Id);
ALTER TABLE dbo.IslemTipleri ADD CONSTRAINT FK_IslemTipleri_Kategori FOREIGN KEY (IslemKategoriId) REFERENCES dbo.IslemKategorileri(Id);
ALTER TABLE dbo.Parcalar  ADD CONSTRAINT FK_Parcalar_Kategori FOREIGN KEY (ParcaKategoriId) REFERENCES dbo.ParcaKategorileri(Id);

ALTER TABLE dbo.Kisiler WITH NOCHECK ADD
    CONSTRAINT FK_Kisiler_Cinsiyet FOREIGN KEY (CinsiyetId) REFERENCES dbo.Cinsiyetler(Id),
    CONSTRAINT FK_Kisiler_DogumIl FOREIGN KEY (DogumYeriIlId) REFERENCES dbo.Iller(Id),
    CONSTRAINT FK_Kisiler_KanGrubu FOREIGN KEY (KanGrubuId) REFERENCES dbo.KanGruplari(Id),
    CONSTRAINT FK_Kisiler_MedeniDurum FOREIGN KEY (MedeniDurumId) REFERENCES dbo.MedeniDurumlar(Id),
    CONSTRAINT FK_Kisiler_Egitim FOREIGN KEY (EgitimDurumuId) REFERENCES dbo.EgitimDurumlari(Id),
    CONSTRAINT FK_Kisiler_Meslek FOREIGN KEY (MeslekId) REFERENCES dbo.Meslekler(Id),
    CONSTRAINT FK_Kisiler_Ehliyet FOREIGN KEY (EhliyetSinifiId) REFERENCES dbo.EhliyetSiniflari(Id);

ALTER TABLE dbo.Araclar WITH NOCHECK ADD
    CONSTRAINT FK_Araclar_Marka FOREIGN KEY (MarkaId) REFERENCES dbo.Markalar(Id),
    CONSTRAINT FK_Araclar_Model FOREIGN KEY (ModelId) REFERENCES dbo.Modeller(Id),
    CONSTRAINT FK_Araclar_Renk FOREIGN KEY (RenkId) REFERENCES dbo.Renkler(Id),
    CONSTRAINT FK_Araclar_Yakit FOREIGN KEY (YakitTipiId) REFERENCES dbo.YakitTipleri(Id),
    CONSTRAINT FK_Araclar_Vites FOREIGN KEY (VitesTipiId) REFERENCES dbo.VitesTipleri(Id),
    CONSTRAINT FK_Araclar_Kasa FOREIGN KEY (KasaTipiId) REFERENCES dbo.KasaTipleri(Id),
    CONSTRAINT FK_Araclar_TescilIl FOREIGN KEY (TescilIlId) REFERENCES dbo.Iller(Id);

ALTER TABLE dbo.AracDetayBilgileri WITH NOCHECK ADD
    CONSTRAINT FK_AracDetay_Arac FOREIGN KEY (AracId) REFERENCES dbo.Araclar(Id),
    CONSTRAINT FK_AracDetay_LastikEbati FOREIGN KEY (LastikEbatiId) REFERENCES dbo.LastikEbatlari(Id),
    CONSTRAINT FK_AracDetay_LastikMarkasi FOREIGN KEY (LastikMarkasiId) REFERENCES dbo.LastikMarkalari(Id),
    CONSTRAINT FK_AracDetay_Yag FOREIGN KEY (YagTipiId) REFERENCES dbo.YagTipleri(Id);

ALTER TABLE dbo.Adresler WITH NOCHECK ADD
    CONSTRAINT FK_Adresler_Tip FOREIGN KEY (AdresTipiId) REFERENCES dbo.AdresTipleri(Id),
    CONSTRAINT FK_Adresler_Il FOREIGN KEY (IlId) REFERENCES dbo.Iller(Id),
    CONSTRAINT FK_Adresler_Ilce FOREIGN KEY (IlceId) REFERENCES dbo.Ilceler(Id);

ALTER TABLE dbo.KisiAdresleri WITH NOCHECK ADD
    CONSTRAINT FK_KisiAdresleri_Kisi FOREIGN KEY (KisiId) REFERENCES dbo.Kisiler(Id),
    CONSTRAINT FK_KisiAdresleri_Adres FOREIGN KEY (AdresId) REFERENCES dbo.Adresler(Id);

ALTER TABLE dbo.KisiAraclari WITH NOCHECK ADD
    CONSTRAINT FK_KisiAraclari_Kisi FOREIGN KEY (KisiId) REFERENCES dbo.Kisiler(Id),
    CONSTRAINT FK_KisiAraclari_Arac FOREIGN KEY (AracId) REFERENCES dbo.Araclar(Id),
    CONSTRAINT FK_KisiAraclari_EdinmeTuru FOREIGN KEY (EdinmeTuruId) REFERENCES dbo.EdinmeTurleri(Id);

ALTER TABLE dbo.Servisler WITH NOCHECK ADD
    CONSTRAINT FK_Servisler_Tip FOREIGN KEY (ServisTipiId) REFERENCES dbo.ServisTipleri(Id),
    CONSTRAINT FK_Servisler_Unvan FOREIGN KEY (UnvanId) REFERENCES dbo.ServisUnvanlari(Id),
    CONSTRAINT FK_Servisler_AnaMarka FOREIGN KEY (AnaBayiMarkaId) REFERENCES dbo.Markalar(Id);

ALTER TABLE dbo.ServisAdresleri WITH NOCHECK ADD
    CONSTRAINT FK_ServisAdresleri_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id),
    CONSTRAINT FK_ServisAdresleri_Adres FOREIGN KEY (AdresId) REFERENCES dbo.Adresler(Id);

ALTER TABLE dbo.ServisMarkaYetkileri WITH NOCHECK ADD
    CONSTRAINT FK_ServisMarkaYetki_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id),
    CONSTRAINT FK_ServisMarkaYetki_Marka FOREIGN KEY (MarkaId) REFERENCES dbo.Markalar(Id);

ALTER TABLE dbo.ServisCalisanlari WITH NOCHECK ADD
    CONSTRAINT FK_ServisCalisanlari_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id),
    CONSTRAINT FK_ServisCalisanlari_Kisi FOREIGN KEY (KisiId) REFERENCES dbo.Kisiler(Id),
    CONSTRAINT FK_ServisCalisanlari_Pozisyon FOREIGN KEY (PozisyonId) REFERENCES dbo.CalisanPozisyonlari(Id);

ALTER TABLE dbo.ServisIslemUcretleri WITH NOCHECK ADD
    CONSTRAINT FK_ServisIslemUcret_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id),
    CONSTRAINT FK_ServisIslemUcret_Islem FOREIGN KEY (IslemTipiId) REFERENCES dbo.IslemTipleri(Id);

ALTER TABLE dbo.ServisCalismaSaatleri WITH NOCHECK ADD
    CONSTRAINT FK_ServisCalismaSaat_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id);

ALTER TABLE dbo.ServisStoklari WITH NOCHECK ADD
    CONSTRAINT FK_ServisStok_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id),
    CONSTRAINT FK_ServisStok_Parca FOREIGN KEY (ParcaId) REFERENCES dbo.Parcalar(Id);

ALTER TABLE dbo.StokHareketleri WITH NOCHECK ADD
    CONSTRAINT FK_StokHareket_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id),
    CONSTRAINT FK_StokHareket_Parca FOREIGN KEY (ParcaId) REFERENCES dbo.Parcalar(Id),
    CONSTRAINT FK_StokHareket_Tip FOREIGN KEY (StokHareketTipiId) REFERENCES dbo.StokHareketTipleri(Id);

ALTER TABLE dbo.Randevular WITH NOCHECK ADD
    CONSTRAINT FK_Randevular_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id),
    CONSTRAINT FK_Randevular_Arac FOREIGN KEY (AracId) REFERENCES dbo.Araclar(Id),
    CONSTRAINT FK_Randevular_Kisi FOREIGN KEY (KisiId) REFERENCES dbo.Kisiler(Id),
    CONSTRAINT FK_Randevular_Sebep FOREIGN KEY (GetirmeSebebiId) REFERENCES dbo.GetirmeSebepleri(Id),
    CONSTRAINT FK_Randevular_Durum FOREIGN KEY (RandevuDurumuId) REFERENCES dbo.RandevuDurumlari(Id),
    CONSTRAINT FK_Randevular_Kanal FOREIGN KEY (RandevuKanaliId) REFERENCES dbo.RandevuKanallari(Id);

ALTER TABLE dbo.Talepler WITH NOCHECK ADD
    CONSTRAINT FK_Talepler_Servis FOREIGN KEY (ServisId) REFERENCES dbo.Servisler(Id),
    CONSTRAINT FK_Talepler_Arac FOREIGN KEY (AracId) REFERENCES dbo.Araclar(Id),
    CONSTRAINT FK_Talepler_GetirenKisi FOREIGN KEY (GetirenKisiId) REFERENCES dbo.Kisiler(Id),
    CONSTRAINT FK_Talepler_Sebep FOREIGN KEY (GetirmeSebebiId) REFERENCES dbo.GetirmeSebepleri(Id),
    CONSTRAINT FK_Talepler_Durum FOREIGN KEY (TalepDurumuId) REFERENCES dbo.TalepDurumlari(Id),
    CONSTRAINT FK_Talepler_Randevu FOREIGN KEY (RandevuId) REFERENCES dbo.Randevular(Id),
    CONSTRAINT FK_Talepler_Danisman FOREIGN KEY (DanismanCalisanId) REFERENCES dbo.ServisCalisanlari(Id);

ALTER TABLE dbo.TalepIslemleri WITH NOCHECK ADD
    CONSTRAINT FK_TalepIslemleri_Talep FOREIGN KEY (TalepId) REFERENCES dbo.Talepler(Id),
    CONSTRAINT FK_TalepIslemleri_Islem FOREIGN KEY (IslemTipiId) REFERENCES dbo.IslemTipleri(Id),
    CONSTRAINT FK_TalepIslemleri_Teknisyen FOREIGN KEY (TeknisyenCalisanId) REFERENCES dbo.ServisCalisanlari(Id);

ALTER TABLE dbo.TalepParcalari WITH NOCHECK ADD
    CONSTRAINT FK_TalepParcalari_Talep FOREIGN KEY (TalepId) REFERENCES dbo.Talepler(Id),
    CONSTRAINT FK_TalepParcalari_Parca FOREIGN KEY (ParcaId) REFERENCES dbo.Parcalar(Id);

ALTER TABLE dbo.TalepDurumGecmisi WITH NOCHECK ADD
    CONSTRAINT FK_TalepDurumGecmisi_Talep FOREIGN KEY (TalepId) REFERENCES dbo.Talepler(Id),
    CONSTRAINT FK_TalepDurumGecmisi_Durum FOREIGN KEY (TalepDurumuId) REFERENCES dbo.TalepDurumlari(Id),
    CONSTRAINT FK_TalepDurumGecmisi_Calisan FOREIGN KEY (DegistirenCalisanId) REFERENCES dbo.ServisCalisanlari(Id);

ALTER TABLE dbo.Odemeler WITH NOCHECK ADD
    CONSTRAINT FK_Odemeler_Talep FOREIGN KEY (TalepId) REFERENCES dbo.Talepler(Id),
    CONSTRAINT FK_Odemeler_Tip FOREIGN KEY (OdemeTipiId) REFERENCES dbo.OdemeTipleri(Id),
    CONSTRAINT FK_Odemeler_Banka FOREIGN KEY (BankaId) REFERENCES dbo.Bankalar(Id);

ALTER TABLE dbo.Faturalar WITH NOCHECK ADD
    CONSTRAINT FK_Faturalar_Talep FOREIGN KEY (TalepId) REFERENCES dbo.Talepler(Id);

ALTER TABLE dbo.AracMuayeneleri WITH NOCHECK ADD
    CONSTRAINT FK_AracMuayene_Arac FOREIGN KEY (AracId) REFERENCES dbo.Araclar(Id);

ALTER TABLE dbo.AracSigortaPoliceleri WITH NOCHECK ADD
    CONSTRAINT FK_AracSigorta_Arac FOREIGN KEY (AracId) REFERENCES dbo.Araclar(Id),
    CONSTRAINT FK_AracSigorta_Sirket FOREIGN KEY (SigortaSirketiId) REFERENCES dbo.SigortaSirketleri(Id),
    CONSTRAINT FK_AracSigorta_PoliceTipi FOREIGN KEY (PoliceTipiId) REFERENCES dbo.PoliceTipleri(Id);

ALTER TABLE dbo.AracKilometreOkumalari WITH NOCHECK ADD
    CONSTRAINT FK_AracKm_Arac FOREIGN KEY (AracId) REFERENCES dbo.Araclar(Id);

ALTER TABLE dbo.AracHasarKayitlari WITH NOCHECK ADD
    CONSTRAINT FK_AracHasar_Arac FOREIGN KEY (AracId) REFERENCES dbo.Araclar(Id),
    CONSTRAINT FK_AracHasar_Tip FOREIGN KEY (HasarTipiId) REFERENCES dbo.HasarTipleri(Id);

ALTER TABLE dbo.CalisanIzinleri WITH NOCHECK ADD
    CONSTRAINT FK_CalisanIzin_Calisan FOREIGN KEY (CalisanId) REFERENCES dbo.ServisCalisanlari(Id),
    CONSTRAINT FK_CalisanIzin_Tip FOREIGN KEY (IzinTipiId) REFERENCES dbo.IzinTipleri(Id);
GO

/* =====================================================================
   6) ARSIV TABLOLARI (gercek hayattaki yillik arsiv deseni)
      12 konu x 10 yil = 120 tablo; her biri ~10.000 satirlik dilim.
      Tablo SAYISI olcegi icin: Veri Haritasi'nin 600-tablo arama senaryosu.
   ===================================================================== */
RAISERROR('==> Arsiv tablolari (12 konu x 10 yil = 120 tablo)...', 0, 1) WITH NOWAIT;
DECLARE @yil INT = 2015;
WHILE @yil <= 2024
BEGIN
    DECLARE @y VARCHAR(4) = CAST(@yil AS VARCHAR(4));
    DECLARE @kaydir INT = (@yil - 2015) * 173;
    EXEC('SELECT TOP (10000) * INTO dbo.TalepArsiv_' + @y + ' FROM dbo.Talepler WHERE Id % 200 = ' + @kaydir + ' % 200 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.TalepIslemArsiv_' + @y + ' FROM dbo.TalepIslemleri WHERE Id % 250 = ' + @kaydir + ' % 250 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.TalepParcaArsiv_' + @y + ' FROM dbo.TalepParcalari WHERE Id % 200 = ' + @kaydir + ' % 200 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.OdemeArsiv_' + @y + ' FROM dbo.Odemeler WHERE Id % 200 = ' + @kaydir + ' % 200 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.FaturaArsiv_' + @y + ' FROM dbo.Faturalar WHERE Id % 150 = ' + @kaydir + ' % 150 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.RandevuArsiv_' + @y + ' FROM dbo.Randevular WHERE Id % 100 = ' + @kaydir + ' % 100 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.StokHareketArsiv_' + @y + ' FROM dbo.StokHareketleri WHERE Id % 150 = ' + @kaydir + ' % 150 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.KmOkumaArsiv_' + @y + ' FROM dbo.AracKilometreOkumalari WHERE Id % 200 = ' + @kaydir + ' % 200 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.MuayeneArsiv_' + @y + ' FROM dbo.AracMuayeneleri WHERE Id % 150 = ' + @kaydir + ' % 150 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.SigortaArsiv_' + @y + ' FROM dbo.AracSigortaPoliceleri WHERE Id % 180 = ' + @kaydir + ' % 180 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.HasarArsiv_' + @y + ' FROM dbo.AracHasarKayitlari WHERE Id % 80 = ' + @kaydir + ' % 80 ORDER BY Id');
    EXEC('SELECT TOP (10000) * INTO dbo.DurumGecmisArsiv_' + @y + ' FROM dbo.TalepDurumGecmisi WHERE Id % 250 = ' + @kaydir + ' % 250 ORDER BY Id');
    RAISERROR('    arsiv yili tamam: %d', 0, 1, @yil) WITH NOWAIT;
    SET @yil = @yil + 1;
END;
GO

/* =====================================================================
   7) KONU SEMALARI (kullanici istegi: kisiye ait olanlar kisi.,
      araca ait olanlar arac. ... ) - metadata tasima, veri kopyalanmaz.
      Ayrica KDS'in sema destegini (S6 Sema alani) gercekci test eder.
   ===================================================================== */
RAISERROR('==> Konu semalari (ortak/kisi/arac/adres/servis/talep/stok/finans/arsiv)...', 0, 1) WITH NOWAIT;
IF SCHEMA_ID('ortak')  IS NULL EXEC('CREATE SCHEMA ortak');   -- tum lookup (tip/kod) tablolari
IF SCHEMA_ID('kisi')   IS NULL EXEC('CREATE SCHEMA kisi');
IF SCHEMA_ID('arac')   IS NULL EXEC('CREATE SCHEMA arac');
IF SCHEMA_ID('adres')  IS NULL EXEC('CREATE SCHEMA adres');
IF SCHEMA_ID('servis') IS NULL EXEC('CREATE SCHEMA servis');
IF SCHEMA_ID('talep')  IS NULL EXEC('CREATE SCHEMA talep');
IF SCHEMA_ID('stok')   IS NULL EXEC('CREATE SCHEMA stok');
IF SCHEMA_ID('finans') IS NULL EXEC('CREATE SCHEMA finans');
IF SCHEMA_ID('arsiv')  IS NULL EXEC('CREATE SCHEMA arsiv');
GO
-- ortak: TUM lookup (tip/kod/siniflandirma) tablolari tek semada (kullanici istegi)
ALTER SCHEMA ortak TRANSFER dbo.Cinsiyetler;
ALTER SCHEMA ortak TRANSFER dbo.KanGruplari;
ALTER SCHEMA ortak TRANSFER dbo.MedeniDurumlar;
ALTER SCHEMA ortak TRANSFER dbo.EgitimDurumlari;
ALTER SCHEMA ortak TRANSFER dbo.MeslekGruplari;
ALTER SCHEMA ortak TRANSFER dbo.Meslekler;
ALTER SCHEMA ortak TRANSFER dbo.EhliyetSiniflari;
ALTER SCHEMA ortak TRANSFER dbo.Markalar;
ALTER SCHEMA ortak TRANSFER dbo.Modeller;
ALTER SCHEMA ortak TRANSFER dbo.Renkler;
ALTER SCHEMA ortak TRANSFER dbo.YakitTipleri;
ALTER SCHEMA ortak TRANSFER dbo.VitesTipleri;
ALTER SCHEMA ortak TRANSFER dbo.KasaTipleri;
ALTER SCHEMA ortak TRANSFER dbo.LastikMarkalari;
ALTER SCHEMA ortak TRANSFER dbo.LastikEbatlari;
ALTER SCHEMA ortak TRANSFER dbo.YagTipleri;
ALTER SCHEMA ortak TRANSFER dbo.EdinmeTurleri;
ALTER SCHEMA ortak TRANSFER dbo.SigortaSirketleri;
ALTER SCHEMA ortak TRANSFER dbo.PoliceTipleri;
ALTER SCHEMA ortak TRANSFER dbo.HasarTipleri;
ALTER SCHEMA ortak TRANSFER dbo.Iller;
ALTER SCHEMA ortak TRANSFER dbo.Ilceler;
ALTER SCHEMA ortak TRANSFER dbo.AdresTipleri;
ALTER SCHEMA ortak TRANSFER dbo.ServisTipleri;
ALTER SCHEMA ortak TRANSFER dbo.CalisanPozisyonlari;
ALTER SCHEMA ortak TRANSFER dbo.IzinTipleri;
ALTER SCHEMA ortak TRANSFER dbo.GetirmeSebepleri;
ALTER SCHEMA ortak TRANSFER dbo.TalepDurumlari;
ALTER SCHEMA ortak TRANSFER dbo.RandevuDurumlari;
ALTER SCHEMA ortak TRANSFER dbo.RandevuKanallari;
ALTER SCHEMA ortak TRANSFER dbo.IslemTipleri;
ALTER SCHEMA ortak TRANSFER dbo.IslemKategorileri;
ALTER SCHEMA ortak TRANSFER dbo.StokHareketTipleri;
ALTER SCHEMA ortak TRANSFER dbo.ParcaKategorileri;
ALTER SCHEMA ortak TRANSFER dbo.OdemeTipleri;
ALTER SCHEMA ortak TRANSFER dbo.Bankalar;
-- domain semalari: yalniz VERI tablolari
ALTER SCHEMA kisi TRANSFER dbo.Kisiler;
ALTER SCHEMA kisi TRANSFER dbo.KisiAdresleri;
ALTER SCHEMA kisi TRANSFER dbo.KisiAraclari;
ALTER SCHEMA arac TRANSFER dbo.Araclar;
ALTER SCHEMA arac TRANSFER dbo.AracDetayBilgileri;
ALTER SCHEMA arac TRANSFER dbo.AracMuayeneleri;
ALTER SCHEMA arac TRANSFER dbo.AracSigortaPoliceleri;
ALTER SCHEMA arac TRANSFER dbo.AracKilometreOkumalari;
ALTER SCHEMA arac TRANSFER dbo.AracHasarKayitlari;
ALTER SCHEMA adres TRANSFER dbo.Adresler;
ALTER SCHEMA servis TRANSFER dbo.Servisler;
ALTER SCHEMA servis TRANSFER dbo.ServisUnvanlari;
ALTER SCHEMA servis TRANSFER dbo.ServisAdresleri;
ALTER SCHEMA servis TRANSFER dbo.ServisMarkaYetkileri;
ALTER SCHEMA servis TRANSFER dbo.ServisCalisanlari;
ALTER SCHEMA servis TRANSFER dbo.ServisIslemUcretleri;
ALTER SCHEMA servis TRANSFER dbo.ServisCalismaSaatleri;
ALTER SCHEMA servis TRANSFER dbo.CalisanIzinleri;
ALTER SCHEMA talep TRANSFER dbo.Talepler;
ALTER SCHEMA talep TRANSFER dbo.TalepIslemleri;
ALTER SCHEMA talep TRANSFER dbo.TalepParcalari;
ALTER SCHEMA talep TRANSFER dbo.TalepDurumGecmisi;
ALTER SCHEMA talep TRANSFER dbo.Randevular;
ALTER SCHEMA stok TRANSFER dbo.Parcalar;
ALTER SCHEMA stok TRANSFER dbo.ServisStoklari;
ALTER SCHEMA stok TRANSFER dbo.StokHareketleri;
ALTER SCHEMA finans TRANSFER dbo.Odemeler;
ALTER SCHEMA finans TRANSFER dbo.Faturalar;
GO
DECLARE @tasima NVARCHAR(MAX) = N'';
SELECT @tasima = @tasima + N'ALTER SCHEMA arsiv TRANSFER dbo.' + QUOTENAME(name) + N';'
FROM sys.tables
WHERE schema_id = SCHEMA_ID('dbo') AND name LIKE '%Arsiv[_]%';
EXEC sp_executesql @tasima;
GO

/* =====================================================================
   8) TEMIZLIK + OZET
   ===================================================================== */
DROP TABLE dbo.__AdE; DROP TABLE dbo.__AdK; DROP TABLE dbo.__Soyad; DROP TABLE dbo.__Mahalle;
DROP TABLE dbo.__Sayi;
GO

RAISERROR('==> Ozet...', 0, 1) WITH NOWAIT;
SELECT COUNT(*) AS ToplamTablo FROM sys.tables;
SELECT s.name AS Sema, COUNT(*) AS TabloSayisi
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
GROUP BY s.name ORDER BY TabloSayisi DESC, s.name;
SELECT s.name + '.' + t.name AS Tablo, SUM(p.rows) AS Satir
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE s.name <> 'arsiv'
GROUP BY s.name, t.name HAVING SUM(p.rows) >= 500000
ORDER BY Satir DESC;
SELECT CAST(SUM(size) * 8 / 1024.0 AS DECIMAL(9,1)) AS ToplamMb FROM sys.database_files;
GO
