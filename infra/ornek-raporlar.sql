/* =============================================================================
   KDS - KdsDemo ornek raporlari (uctan uca, cok-semali)
   -----------------------------------------------------------------------------
   Bu sorgular KDS'in SQL Editoru ekranina (S9) YAPISTIRILIP calistirilabilir:
   hepsi TEK SELECT'tir (SELECT-only bekcisinden gecer), satir siniri uygulanir.
   Ayni desenler Veri Haritasi + Sorgu Tasarimcisi ile gorsel olarak da kurulabilir.
   Kaynak: (localdb)\MSSQLLocalDB / KdsDemo  (infra/ornek-veritabani.sql uretir)
   ============================================================================= */

/* ---------------------------------------------------------------------------
   RAPOR 1 - "Etimesgut'ta oturan Audi sahiplerinin buyuk odemeli servis kayitlari"
   Zincir: finans.Odemeler(>5000) -> talep.Talepler -> arac.Araclar(Audi)
           -> kisi (getiren + AKTIF adresi Etimesgut) -> servis.Servisler
           -> talep.TalepParcalari -> stok (parca + serviste kalan stok)
           -> finans.Faturalar
   --------------------------------------------------------------------------- */
SELECT
    k.Ad + N' ' + k.Soyad            AS KisiAdSoyad,
    mk.Ad                            AS Marka,
    md.Ad                            AS Model,
    a.ModelYili                      AS Yil,
    a.Plaka                          AS Plaka,
    sv.Ad                            AS Servis,
    t.TalepNo                        AS TalepNo,
    gs.Ad                            AS GetirmeSebebi,
    o.Tutar                          AS OdemeTutari,
    p.Ad                             AS Parca,
    tp.Adet                          AS KullanilanAdet,
    tp.Tutar                         AS ParcaTutari,
    ss.MevcutAdet                    AS ServisteKalanStok,
    f.GenelToplam                    AS FaturaTutari
FROM talep.Talepler t
JOIN finans.Odemeler o           ON o.TalepId = t.Id AND o.Tutar > 5000
JOIN arac.Araclar a              ON a.Id = t.AracId
JOIN ortak.Markalar mk           ON mk.Id = a.MarkaId AND mk.Ad = N'Audi'
JOIN ortak.Modeller md           ON md.Id = a.ModelId
JOIN kisi.Kisiler k              ON k.Id = t.GetirenKisiId
JOIN kisi.KisiAdresleri ka       ON ka.KisiId = k.Id AND ka.AktifMi = 1
JOIN adres.Adresler ad           ON ad.Id = ka.AdresId
JOIN ortak.Ilceler il            ON il.Id = ad.IlceId AND il.Ad = N'Etimesgut'
JOIN ortak.GetirmeSebepleri gs   ON gs.Id = t.GetirmeSebebiId
JOIN servis.Servisler sv         ON sv.Id = t.ServisId
JOIN talep.TalepParcalari tp     ON tp.TalepId = t.Id
JOIN stok.Parcalar p             ON p.Id = tp.ParcaId
LEFT JOIN stok.ServisStoklari ss ON ss.ServisId = t.ServisId AND ss.ParcaId = tp.ParcaId
JOIN finans.Faturalar f          ON f.TalepId = t.Id
ORDER BY o.Tutar DESC;

/* ---------------------------------------------------------------------------
   RAPOR 2 - "Marka bazinda servis cirosu" (2M talep uzerinde ozet)
   Tasarimciyla birebir kurulabilir: kirilim=Marka, sayisal=Toplam/adet.
   --------------------------------------------------------------------------- */
SELECT
    mk.Ad                            AS Marka,
    COUNT(*)                         AS TalepSayisi,
    SUM(t.ToplamTutar)               AS ToplamCiro,
    AVG(t.ToplamTutar)               AS OrtalamaTutar,
    SUM(CASE WHEN t.GarantiKapsamindaMi = 1 THEN 1 ELSE 0 END) AS GarantiliTalep
FROM talep.Talepler t
JOIN arac.Araclar a    ON a.Id = t.AracId
JOIN ortak.Markalar mk ON mk.Id = a.MarkaId
GROUP BY mk.Ad
ORDER BY ToplamCiro DESC;

/* ---------------------------------------------------------------------------
   RAPOR 3 - "Il bazinda kisi / arac / talep yogunlugu" (cok kaynakli ozet)
   --------------------------------------------------------------------------- */
SELECT
    i.Ad                             AS Il,
    i.BolgeAdi                       AS Bolge,
    COUNT(DISTINCT k.Id)             AS KisiSayisi,
    COUNT(DISTINCT t.Id)             AS TalepSayisi,
    SUM(t.ToplamTutar)               AS ToplamTutar
FROM kisi.Kisiler k
JOIN kisi.KisiAdresleri ka ON ka.KisiId = k.Id AND ka.AktifMi = 1
JOIN adres.Adresler ad     ON ad.Id = ka.AdresId
JOIN ortak.Iller i         ON i.Id = ad.IlId
LEFT JOIN talep.Talepler t ON t.GetirenKisiId = k.Id
GROUP BY i.Ad, i.BolgeAdi
ORDER BY ToplamTutar DESC;

/* ---------------------------------------------------------------------------
   RAPOR 4 - "Servis performansi: en cok is yapan 20 servis"
   Iscilik + parca + odeme + calisan sayisiyla birlikte.
   --------------------------------------------------------------------------- */
SELECT TOP (20)
    sv.Ad                            AS Servis,
    st.Ad                            AS ServisTipi,
    COUNT(DISTINCT t.Id)             AS TalepSayisi,
    SUM(t.IscilikTutari)             AS ToplamIscilik,
    SUM(t.ParcaTutari)               AS ToplamParca,
    SUM(t.ToplamTutar)               AS GenelCiro,
    (SELECT COUNT(*) FROM servis.ServisCalisanlari c WHERE c.ServisId = sv.Id AND c.AktifMi = 1) AS AktifCalisan
FROM servis.Servisler sv
JOIN ortak.ServisTipleri st ON st.Id = sv.ServisTipiId
JOIN talep.Talepler t       ON t.ServisId = sv.Id
GROUP BY sv.Id, sv.Ad, st.Ad
ORDER BY GenelCiro DESC;

/* ---------------------------------------------------------------------------
   RAPOR 5 - "Getirme sebebi x durum matrisi" (talep akisinin fotografi)
   --------------------------------------------------------------------------- */
SELECT
    gs.Ad                            AS GetirmeSebebi,
    td.Ad                            AS Durum,
    COUNT(*)                         AS TalepSayisi,
    AVG(t.ToplamTutar)               AS OrtalamaTutar
FROM talep.Talepler t
JOIN ortak.GetirmeSebepleri gs ON gs.Id = t.GetirmeSebebiId
JOIN ortak.TalepDurumlari td   ON td.Id = t.TalepDurumuId
GROUP BY gs.Ad, td.Ad
ORDER BY TalepSayisi DESC;

/* =============================================================================
   M32 - ILERI SEVIYE ORNEKLER (RAPOR 6-15)
   -----------------------------------------------------------------------------
   Her biri TEK SELECT'tir (bekciden gecer): CTE, pencere fonksiyonlari
   (RANK / SUM OVER / LAG), CROSS APPLY, korele NOT EXISTS, PIVOT, HAVING,
   GROUPING SETS, cok kosullu detay ve mutabakat desenleri.
   NOT: Demo verisi '2026-08-06' capasina gore uretildigi icin tarih filtreleri
   GETDATE() yerine bu capayi kullanir (deterministik sonuc).
   ============================================================================= */

/* ---------------------------------------------------------------------------
   RAPOR 6 - "Marka ciro payi ve ulke ici siralama" (CTE + pencere: RANK, pay %)
   Kosullar: yalnizca teslim edilmis (CikisTarihi dolu) ve garanti DISI talepler.
   --------------------------------------------------------------------------- */
WITH marka_ozet AS (
    SELECT
        mk.Ad                        AS Marka,
        mk.UlkeAdi                   AS Ulke,
        COUNT_BIG(*)                 AS TalepSayisi,
        SUM(t.ToplamTutar)           AS Ciro
    FROM talep.Talepler t
    JOIN arac.Araclar a    ON a.Id = t.AracId
    JOIN ortak.Markalar mk ON mk.Id = a.MarkaId
    WHERE t.CikisTarihi IS NOT NULL
      AND t.GarantiKapsamindaMi = 0
    GROUP BY mk.Ad, mk.UlkeAdi
)
SELECT
    Marka, Ulke, TalepSayisi, Ciro,
    CAST(Ciro * 100.0 / SUM(Ciro) OVER () AS DECIMAL(5,2))  AS CiroPayiYuzde,
    RANK() OVER (ORDER BY Ciro DESC)                        AS GenelSira,
    RANK() OVER (PARTITION BY Ulke ORDER BY Ciro DESC)      AS UlkeIciSira
FROM marka_ozet
ORDER BY Ciro DESC;

/* ---------------------------------------------------------------------------
   RAPOR 7 - "Son 12 ayin ciro trendi + onceki aya gore degisim %" (CTE + LAG)
   --------------------------------------------------------------------------- */
WITH aylik AS (
    SELECT
        DATEFROMPARTS(YEAR(t.GirisTarihi), MONTH(t.GirisTarihi), 1) AS Ay,
        COUNT_BIG(*)                                                AS TalepSayisi,
        SUM(t.ToplamTutar)                                          AS Ciro,
        SUM(CASE WHEN t.GarantiKapsamindaMi = 1 THEN t.ToplamTutar ELSE 0 END) AS GarantiCiro
    FROM talep.Talepler t
    WHERE t.GirisTarihi >= DATEADD(MONTH, -12, '2026-08-06')
    GROUP BY DATEFROMPARTS(YEAR(t.GirisTarihi), MONTH(t.GirisTarihi), 1)
)
SELECT
    Ay, TalepSayisi, Ciro, GarantiCiro,
    LAG(Ciro) OVER (ORDER BY Ay)                            AS OncekiAyCiro,
    CAST((Ciro - LAG(Ciro) OVER (ORDER BY Ay)) * 100.0
         / NULLIF(LAG(Ciro) OVER (ORDER BY Ay), 0) AS DECIMAL(6,2)) AS DegisimYuzde
FROM aylik
ORDER BY Ay;

/* ---------------------------------------------------------------------------
   RAPOR 8 - "En yuksek cirolu 10 servisin en pahali 3 talebi" (CROSS APPLY)
   Servis basina TOP 3: siralama servis icinde ayri ayri isler.
   --------------------------------------------------------------------------- */
SELECT
    sv.Ad                            AS Servis,
    e.Ciro                           AS ServisCirosu,
    tt.TalepNo, tt.GirisTarihi, tt.ToplamTutar,
    mk.Ad                            AS Marka
FROM (SELECT TOP (10) t.ServisId, SUM(t.ToplamTutar) AS Ciro
      FROM talep.Talepler t
      GROUP BY t.ServisId
      ORDER BY SUM(t.ToplamTutar) DESC) e
JOIN servis.Servisler sv ON sv.Id = e.ServisId
CROSS APPLY (SELECT TOP (3) t2.TalepNo, t2.GirisTarihi, t2.ToplamTutar, t2.AracId
             FROM talep.Talepler t2
             WHERE t2.ServisId = e.ServisId
             ORDER BY t2.ToplamTutar DESC) tt
JOIN arac.Araclar a    ON a.Id = tt.AracId
JOIN ortak.Markalar mk ON mk.Id = a.MarkaId
ORDER BY e.Ciro DESC, tt.ToplamTutar DESC;

/* ---------------------------------------------------------------------------
   RAPOR 9 - "Sigortasi olmayan yuksek hasarli aktif araclar" (korele NOT EXISTS)
   Kosullar: hasar > 100.000, sigorta karsilamamis, son 2 yil, arac aktif,
   ve aracin AKTIF policesi hic yok.
   --------------------------------------------------------------------------- */
SELECT TOP (100)
    a.Plaka, mk.Ad AS Marka, md.Ad AS Model, a.ModelYili,
    h.HasarTarihi, ht.Ad AS HasarTipi, h.HasarTutari
FROM arac.AracHasarKayitlari h
JOIN arac.Araclar a      ON a.Id = h.AracId AND a.AktifMi = 1
JOIN ortak.Markalar mk   ON mk.Id = a.MarkaId
JOIN ortak.Modeller md   ON md.Id = a.ModelId
JOIN ortak.HasarTipleri ht ON ht.Id = h.HasarTipiId
WHERE h.HasarTutari > 100000
  AND h.SigortaKarsiladiMi = 0
  AND h.HasarTarihi >= DATEADD(YEAR, -2, '2026-08-06')
  AND NOT EXISTS (SELECT 1 FROM arac.AracSigortaPoliceleri p
                  WHERE p.AracId = a.Id AND p.AktifMi = 1)
ORDER BY h.HasarTutari DESC;

/* ---------------------------------------------------------------------------
   RAPOR 10 - "Bolge x yil ciro matrisi" (PIVOT)
   Servisin MERKEZ adresinin bulundugu ile gore bolge kirilimi.
   --------------------------------------------------------------------------- */
SELECT Bolge, [2023], [2024], [2025], [2026]
FROM (SELECT
          i.BolgeAdi               AS Bolge,
          YEAR(t.GirisTarihi)      AS Yil,
          t.ToplamTutar
      FROM talep.Talepler t
      JOIN servis.Servisler sv       ON sv.Id = t.ServisId
      JOIN servis.ServisAdresleri sa ON sa.ServisId = sv.Id AND sa.KullanimTuru = N'Merkez' AND sa.AktifMi = 1
      JOIN adres.Adresler ad         ON ad.Id = sa.AdresId
      JOIN ortak.Iller i             ON i.Id = ad.IlId) kaynak
PIVOT (SUM(ToplamTutar) FOR Yil IN ([2023], [2024], [2025], [2026])) p
ORDER BY Bolge;

/* ---------------------------------------------------------------------------
   RAPOR 11 - "3+ markaya yetkili aktif servisler" (HAVING + DISTINCT sayim)
   Suresi bitmis yetkiler sayilmaz; aktif calisan sayisi korele alt sorgudan.
   --------------------------------------------------------------------------- */
SELECT
    sv.Ad                            AS Servis,
    st.Ad                            AS ServisTipi,
    COUNT(DISTINCT y.MarkaId)        AS YetkiliMarkaSayisi,
    MIN(y.YetkiBaslangicTarihi)      AS IlkYetkiTarihi,
    (SELECT COUNT(*) FROM servis.ServisCalisanlari c
     WHERE c.ServisId = sv.Id AND c.AktifMi = 1) AS AktifCalisan
FROM servis.Servisler sv
JOIN ortak.ServisTipleri st          ON st.Id = sv.ServisTipiId
JOIN servis.ServisMarkaYetkileri y   ON y.ServisId = sv.Id
WHERE sv.AktifMi = 1
  AND (y.YetkiBitisTarihi IS NULL OR y.YetkiBitisTarihi > '2026-08-06')
GROUP BY sv.Id, sv.Ad, st.Ad
HAVING COUNT(DISTINCT y.MarkaId) >= 3
ORDER BY YetkiliMarkaSayisi DESC, Servis;

/* ---------------------------------------------------------------------------
   RAPOR 12 - "Garantili + orijinal parcali + iskontolu satis detayi"
   (cok kosullu detay: bit + aralik + tarih + lookup filtreleri bir arada)
   --------------------------------------------------------------------------- */
SELECT TOP (200)
    t.TalepNo, t.GirisTarihi,
    sv.Ad                            AS Servis,
    mk.Ad                            AS Marka,
    pk.Ad                            AS ParcaKategorisi,
    p.Ad                             AS Parca,
    p.OemKodu,
    tp.Adet, tp.BirimFiyat, tp.IskontoOrani, tp.Tutar
FROM talep.TalepParcalari tp
JOIN talep.Talepler t            ON t.Id = tp.TalepId
JOIN servis.Servisler sv         ON sv.Id = t.ServisId
JOIN arac.Araclar a              ON a.Id = t.AracId
JOIN ortak.Markalar mk           ON mk.Id = a.MarkaId
JOIN stok.Parcalar p             ON p.Id = tp.ParcaId AND p.OrijinalMi = 1
JOIN ortak.ParcaKategorileri pk  ON pk.Id = p.ParcaKategoriId
WHERE tp.GarantiKapsamindaMi = 1
  AND tp.IskontoOrani > 0
  AND tp.BirimFiyat BETWEEN 5000 AND 20000
  AND t.GirisTarihi >= DATEADD(MONTH, -6, '2026-08-06')
ORDER BY tp.Tutar DESC;

/* ---------------------------------------------------------------------------
   RAPOR 13 - "Geri alinmis km sayaci suphesi" (LAG ile ardisik okuma denetimi)
   Ayni aracin ardisik iki okumasinda 1.000+ km DUSUS varsa listeler.
   --------------------------------------------------------------------------- */
WITH okumalar AS (
    SELECT
        o.AracId, o.OkumaTarihi, o.Kilometre, o.OkumaKaynagi,
        LAG(o.Kilometre)   OVER (PARTITION BY o.AracId ORDER BY o.OkumaTarihi, o.Id) AS OncekiKm,
        LAG(o.OkumaTarihi) OVER (PARTITION BY o.AracId ORDER BY o.OkumaTarihi, o.Id) AS OncekiTarih
    FROM arac.AracKilometreOkumalari o
    WHERE o.OkumaTarihi >= DATEADD(YEAR, -1, '2026-08-06')
)
SELECT TOP (100)
    a.Plaka, mk.Ad AS Marka,
    ok.OncekiTarih, ok.OncekiKm,
    ok.OkumaTarihi, ok.Kilometre,
    ok.OncekiKm - ok.Kilometre       AS GeriAlinanKm,
    ok.OkumaKaynagi
FROM okumalar ok
JOIN arac.Araclar a    ON a.Id = ok.AracId
JOIN ortak.Markalar mk ON mk.Id = a.MarkaId
WHERE ok.OncekiKm IS NOT NULL
  AND ok.Kilometre < ok.OncekiKm - 1000
ORDER BY GeriAlinanKm DESC;

/* ---------------------------------------------------------------------------
   RAPOR 14 - "Randevu kanali bazinda talebe donusum orani" (GROUPING SETS ile
   kanal satirlari + genel TOPLAM satiri tek taramada)
   --------------------------------------------------------------------------- */
SELECT
    ISNULL(rk.Ad, N'— TOPLAM —')     AS Kanal,
    COUNT_BIG(*)                     AS RandevuSayisi,
    SUM(CASE WHEN t.Id IS NOT NULL THEN 1 ELSE 0 END) AS TalebeDonusen,
    CAST(SUM(CASE WHEN t.Id IS NOT NULL THEN 1 ELSE 0 END) * 100.0
         / COUNT_BIG(*) AS DECIMAL(5,2))              AS DonusumYuzde,
    AVG(CASE WHEN t.Id IS NOT NULL THEN t.ToplamTutar END) AS DonusenOrtalamaTutar
FROM talep.Randevular r
JOIN ortak.RandevuKanallari rk ON rk.Id = r.RandevuKanaliId
LEFT JOIN talep.Talepler t     ON t.RandevuId = r.Id
GROUP BY GROUPING SETS ((rk.Ad), ())
ORDER BY CASE WHEN rk.Ad IS NULL THEN 1 ELSE 0 END, DonusumYuzde DESC;

/* ---------------------------------------------------------------------------
   RAPOR 15 - "Odeme / fatura mutabakati" (turetilmis tablolar + fark denetimi)
   Teslim edilmis taleplerde odenen toplam ile (iptal olmayan) fatura toplami
   arasinda 25.000+ fark olanlar. AGIR ornek: iki buyuk tabloda ozet + birlestirme.
   --------------------------------------------------------------------------- */
SELECT TOP (100)
    t.TalepNo,
    sv.Ad                            AS Servis,
    t.ToplamTutar                    AS TalepTutari,
    ISNULL(o.OdenenToplam, 0)        AS OdenenToplam,
    ISNULL(f.FaturaToplam, 0)        AS FaturaToplam,
    ISNULL(o.OdenenToplam, 0) - ISNULL(f.FaturaToplam, 0) AS Fark,
    ISNULL(o.OdemeSayisi, 0)         AS OdemeSayisi,
    ISNULL(f.FaturaSayisi, 0)        AS FaturaSayisi
FROM talep.Talepler t
JOIN servis.Servisler sv ON sv.Id = t.ServisId
LEFT JOIN (SELECT TalepId, SUM(Tutar) AS OdenenToplam, COUNT_BIG(*) AS OdemeSayisi
           FROM finans.Odemeler GROUP BY TalepId) o ON o.TalepId = t.Id
LEFT JOIN (SELECT TalepId, SUM(GenelToplam) AS FaturaToplam, COUNT_BIG(*) AS FaturaSayisi
           FROM finans.Faturalar WHERE IptalMi = 0 GROUP BY TalepId) f ON f.TalepId = t.Id
WHERE t.CikisTarihi IS NOT NULL
  AND ABS(ISNULL(o.OdenenToplam, 0) - ISNULL(f.FaturaToplam, 0)) > 25000
ORDER BY ABS(ISNULL(o.OdenenToplam, 0) - ISNULL(f.FaturaToplam, 0)) DESC;
