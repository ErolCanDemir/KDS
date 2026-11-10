using System.Text.Json.Serialization;

namespace Kds.Contracts.QueryModel;

/// <summary>Tasarlanan sorgunun JSON modeli (AST) — SQL DEĞİL (00-analiz §5). Backend bunu
/// Veri Haritası'na göre doğrular ve motor lehçesine göre PARAMETRELİ SQL'e çevirir. UI'dan
/// bağımsız, versiyonlanabilir/denetlenebilir. v1 kapsam: kolon seçimi · join (haritadan) ·
/// filtre ağacı (AND/OR) · GROUP BY + aggregate · sıralama · limit.</summary>
public sealed record QueryModel(
    Guid HaritaId,
    IReadOnlyList<SelectedField> Alanlar,
    FilterNode? Filtre,
    IReadOnlyList<SortItem> Siralama,
    int? SatirLimiti,
    IReadOnlyList<JoinSelection>? Joinler = null,
    IReadOnlyList<GroupingItem>? Gruplamalar = null,
    IReadOnlyList<SummaryField>? OzetAlanlar = null,
    IReadOnlyList<CompositeField>? BilesikAlanlar = null,
    string? ElleKosul = null,
    Guid? KokTabloId = null,
    IReadOnlyList<JoinSetting>? JoinAyarlari = null,
    MantikBaglaci ElleKosulBaglaci = MantikBaglaci.Ve,
    IReadOnlyList<ConditionalMeasure>? KosulluOlculer = null,
    IReadOnlyList<ReportParameter>? Parametreler = null,
    // M17: alansız eklenen tablolar — SELECT'e girmez, yalnız JOIN'e katılır (kanvasta köprü/kapsam
    // tablosu; kullanıcı alan seçmeden tabloyu sorguda tutabilir).
    IReadOnlyList<Guid>? EkTablolar = null,
    // M18: kullanıcının Join Yolları'ndaki tablo SIRASI — otomatik ebeveyn seçiminde işleme
    // önceliğidir (null = alan ekleme sırası); önyüz ağacıyla determinizmi korur.
    IReadOnlyList<Guid>? TabloSirasi = null,
    // M29: satır-düzeyi CASE kolonları (aggregate'sız CASE WHEN … THEN … ELSE … END).
    IReadOnlyList<CaseColumn>? CaseAlanlar = null,
    // Y3: özet (gruplama SONRASI) koşulları → HAVING. Filtre WHERE'dir: satır bazında, gruplamadan
    // ÖNCE çalışır; buradakiler grubun ÖZETİNE bakar ("3'ten çok markası olan servisler").
    IReadOnlyList<SummaryCondition>? OzetKosullar = null,
    // Y1: hesaplanan (formül) kolonlar — SELECT'e serbest HESAP ifadesi.
    IReadOnlyList<ComputedColumn>? HesaplananAlanlar = null,
    // S22: katalog metrikleri — modele formülün KOPYASI değil metriğin KİMLİĞİ girer; formül koşum
    // anında katalogdan çözülür (RunQueryModelHandler). Böylece metrik güncellenince onu kullanan
    // her rapor bir sonraki koşumda yeni tanımı kullanır — tek doğruluk kaynağı ancak böyle tutar.
    IReadOnlyList<MetricField>? MetrikAlanlar = null,
    // Y2: formül koşulları — sol taraf bir HESAP (ya da [Çıkış Kolonu] atfı); toplama içeriyorsa
    // HAVING'e, içermiyorsa WHERE'e gider (kullanıcı bunu bilmek zorunda değil).
    IReadOnlyList<ExpressionCondition>? IfadeKosullari = null,
    // Y6: pencere kolonları — satırı KOMŞULARINA göre değerlendirir (sıra no, önceki/sonraki dönem,
    // koşan toplam, bölüm toplamı, hareketli ortalama). SQL'de OVER (...) üretir.
    IReadOnlyList<WindowColumn>? PencereKolonlari = null,
    // Y7: "her X için ilk N satır" — sonucu bölüm bölüm kırpar (SQL'de sarmalayıcı alt sorgu).
    TopNPerGroup? GrupIciIlkN = null,
    // Y8: ARA KÜME (ikinci kat) — sorgunun sonucu bir alt sorgu olur, üstünde YENİDEN gruplama/
    // özetleme yapılır ("özetin özeti": müşteri başına toplamların ORTALAMASI).
    IReadOnlyList<string>? AraKumeGruplari = null,
    IReadOnlyList<OuterAggregate>? AraKumeOzeti = null,
    // Y9: çapraz tablo — bir kolonun DEĞERLERİ başlığa çıkar ("aylar sütun olsun").
    PivotSpec? Capraz = null,
    // Y10: ara toplam / genel toplam satırları (GROUPING SETS / ROLLUP).
    SubtotalSpec? AraToplam = null,
    // Y11: "kaydı VAR / YOK" koşulları — ilişkili tabloda eşleşme arar (EXISTS / NOT EXISTS).
    IReadOnlyList<ExistenceCondition>? VarlikKosullari = null,
    // Y12: yinelenen satırları ele (SELECT DISTINCT). Gruplama/toplama varken zaten tekildir;
    // asıl işi "join yüzünden çoğalmış" satır listelerinde görür.
    bool Tekille = false,
    // Y16: sayfalama — kaçıncı satırdan başlansın (OFFSET). Satır limiti sayfa BOYUdur.
    int? Atla = null,
    // Y14: aynı tabloyu sorguda İKİNCİ kez kullanma (self-join) — her örnek kendi kimliğiyle
    // yaşar ve modelin geri kalanı (alan/filtre/join) o kimliği normal bir tablo gibi kullanır.
    IReadOnlyList<TableInstance>? TabloOrnekleri = null,
    // Y15: bu sorgunun sonucuna ALT ALTA eklenecek sorgular (UNION). Sıralama/limit/sayfalama
    // ANA modelden gelir ve birleşimin TAMAMINA uygulanır.
    IReadOnlyList<QueryModel>? Birlesim = null,
    // Y15: yinelenen satırlar elensin mi (UNION) yoksa hepsi kalsın mı (UNION ALL, varsayılan).
    bool BirlesimTekille = false);

/// <summary>Y14 — tablo örneği: haritadaki BİR tablonun sorgu içindeki ikinci (üçüncü…) kopyası.
/// "Getiren kişi" ile "danışman" aynı Kisiler tablosundan gelir ama AYRI satırlardır; tek kimlikle
/// ikisi ayırt edilemezdi. <see cref="OrnekId"/> modelin geri kalanında normal bir tablo kimliği
/// gibi kullanılır (alan seçimi, filtre, join ayarı); <see cref="Etiket"/> ekranda ve
/// <c>{Etiket.Kolon}</c> yer tutucusunda görünen addır.
/// <para>Örneğin haritadaki TÜM ilişkileri kopyalanır — yani kaynak tablo nereye bağlanabiliyorsa
/// örnek de oraya bağlanabilir.</para></summary>
public sealed record TableInstance(Guid OrnekId, Guid KaynakTabloId, string? Etiket = null);

/// <summary>Y11 — varlık koşulu: "şu tabloda eşleşen kaydı OLANLAR / OLMAYANLAR".
/// Join ile yapılmaya çalışılırsa iki tuzak vardır: eşleşme birden çoksa ana satır ÇOĞALIR
/// (toplamlar şişer) ve "hiç yok" sorusu join'le doğrudan sorulamaz. Bu koşul SQL'de
/// <c>EXISTS/NOT EXISTS</c> üretir: satır ne çoğalır ne de kolon ekler.
/// <para><see cref="TabloId"/> aranan tablo; <see cref="Baglar"/> o tabloyu sorgudaki bir tabloya
/// bağlayan kolon çiftleri (boşsa Veri Haritası'ndaki ilişkiden türetilir);
/// <see cref="Filtre"/> aranan kayda ek koşul ("iptal edilmemiş siparişi olanlar").</para></summary>
public sealed record ExistenceCondition(
    Guid TabloId,
    bool Yok = false,
    IReadOnlyList<ExistenceLink>? Baglar = null,
    FilterNode? Filtre = null,
    MantikBaglaci Baglac = MantikBaglaci.Ve);

/// <summary>Y11 — varlık koşulunun bağ kolonları: aranan tablonun kolonu = sorgudaki tablonun kolonu.</summary>
public sealed record ExistenceLink(string Kolon, Guid AnaTabloId, string AnaKolon);

/// <summary>Y10 — ara toplam satırları. Normalde her grup bir satırdır ve "TOPLAM" satırı yoktur;
/// bu, sonuca ek ÖZET SATIRLARI ekler: <see cref="AraToplamTuru.GenelToplam"/> yalnız en sona bir
/// genel toplam, <see cref="AraToplamTuru.Kademeli"/> (ROLLUP) her kırılım düzeyi için ara toplam.
/// <para><see cref="Etiketle"/> açıkken özet satırlarındaki boş kırılım hücreleri
/// <see cref="Etiket"/> ile doldurulur (SQL'in <c>GROUPING()</c> işleviyle GERÇEK boş değerden
/// ayrılır); bu, kırılım kolonlarını METNE çevirir — kapatılırsa hücreler boş (NULL) kalır.</para></summary>
public sealed record SubtotalSpec(
    AraToplamTuru Tur = AraToplamTuru.GenelToplam,
    string Etiket = "(Tümü)",
    bool Etiketle = true);

public enum AraToplamTuru { GenelToplam = 0, Kademeli }

/// <summary>Y9 — çapraz tablo (PIVOT): bir kolonun DEĞERLERİ sütun başlığı olur.
/// "Şehir satır, ay sütun, ciro hücre" gibi. <see cref="BaslikKolonu"/> başlığa çıkacak çıkış
/// kolonu, <see cref="DegerKolonu"/> hücredeki (toplanacak) çıkış kolonu, <see cref="Basliklar"/>
/// sütuna dönecek DEĞERLER (SQL'de PIVOT sütun listesi SABİT olmak zorundadır — dinamik liste
/// çalışma anında SQL üretmek demektir, bu üründe bilerek yapılmaz: liste kullanıcıdan alınır ya da
/// "başlıkları getir" düğmesiyle önceden okunur). Satır kırılımı, çapraza girmeyen diğer çıkış
/// kolonlarıdır.</summary>
public sealed record PivotSpec(
    string BaslikKolonu,
    string DegerKolonu,
    IReadOnlyList<string> Basliklar,
    ToplamaTuru Toplama = ToplamaTuru.Topla);

/// <summary>Y8 — ara küme özeti: DIŞ katmanda, iç sorgunun ÇIKIŞ KOLONU üzerinde toplama.
/// "Her müşterinin toplamını bul (iç kat), sonra bunların ortalamasını al (dış kat)" gibi
/// tek katta yazılamayan soruların karşılığıdır (SQL'de toplama içinde toplama yazılamaz).</summary>
public sealed record OuterAggregate(ToplamaTuru Toplama, string CikisAdi, string? TakmaAd = null);

/// <summary>Y7 — grup içi ilk N: "her marka için en çok satan 3 ürün", "her ilde en yeni 5 kayıt".
/// Sıradan satır limiti sonucun TAMAMINI kırpar; bu ise HER BÖLÜMDEN N satır bırakır.
/// <para>SQL'de sıra numarası bir pencere işleviyle hesaplanır ve pencere işlevine WHERE'de atıf
/// yapılamadığı için sorgu bir alt sorguyla SARMALANIR (dış katman sıra ≤ N filtresini uygular).
/// <see cref="Bolum"/> boşsa kırpma tüm sonuca uygulanır (sıradan "ilk N").</para>
/// <see cref="EsitleriDeAl"/>: N. sıradaki değerde beraberlik varsa hepsi kalsın mı (RANK) yoksa
/// tam N satır mı dönsün (ROW_NUMBER).</summary>
public sealed record TopNPerGroup(
    IReadOnlyList<WindowSort> Sira,
    int Adet,
    IReadOnlyList<string>? Bolum = null,
    bool EsitleriDeAl = false);

/// <summary>Y6 — pencere (window) kolonu: değeri satırın KENDİsinden değil, aynı pencerede yer alan
/// KOMŞU satırlardan üretir. <see cref="Deger"/>, <see cref="Bolum"/> ve <see cref="Sira"/> Y1/Y2
/// formül dilini kullanır: <c>{Tablo.Kolon}</c> yer tutucusu ya da <c>[Çıkış Kolonu]</c> atfı
/// (<c>[Ciro]</c> gibi) — böylece gruplanmış sorguda özet kolonlara doğrudan atıf yapılabilir.
/// <para><b>Sıra:</b> pencere kolonları SELECT'te ÖZET kolonlardan SONRA, hesaplanan (Y1)
/// kolonlardan ÖNCE üretilir; yani hesaplanan kolon bir pencere kolonuna atıf yapabilir
/// ("bölüm toplamına oran"), tersi olmaz (SQL'de döngüsel olurdu).</para></summary>
public sealed record WindowColumn(
    PencereIslevi Islev,
    string? Deger = null,
    IReadOnlyList<string>? Bolum = null,
    IReadOnlyList<WindowSort>? Sira = null,
    int? Sayi = null,
    string? TakmaAd = null);

/// <summary>Pencere sırası: ifade + yön. "Neye göre önceki/kümülatif?" sorusunun cevabı.</summary>
public sealed record WindowSort(string Ifade, bool Azalan = false);

/// <summary>Y6 — pencere işlevi.
/// <list type="bullet">
/// <item><see cref="SiraNo"/>/<see cref="Sira"/>/<see cref="YogunSira"/>: ROW_NUMBER/RANK/DENSE_RANK —
/// değer istemez, sıra ZORUNLUdur.</item>
/// <item><see cref="Dilim"/>: NTILE(n) — çeyreklik/dilim segmentasyonu.</item>
/// <item><see cref="Onceki"/>/<see cref="Sonraki"/>: LAG/LEAD — n satır önceki/sonraki değer.</item>
/// <item><see cref="KosanToplam"/>: sıraya göre kümülatif toplam.</item>
/// <item><see cref="BolumToplami"/>: bölümün TAMAMININ toplamı (paya oran hesabı için) — sıra almaz.</item>
/// <item><see cref="HareketliOrtalama"/>: son n satırın ortalaması.</item>
/// </list></summary>
public enum PencereIslevi
{
    SiraNo = 0, Sira, YogunSira, Dilim, Onceki, Sonraki, KosanToplam, BolumToplami, HareketliOrtalama,
}

/// <summary>Y2 — formül koşulu: sol taraf serbest bir HESAP (<c>{T.K} * 1.18</c>,
/// <c>SUM({T.Tutar}) / NULLIF(COUNT({T.Id}), 0)</c>) ya da bir ÇIKIŞ KOLONU atfı
/// (<c>[Kâr Marjı]</c> — SELECT'teki takma ada göre); sağ taraf her filtre gibi
/// operatör + değer(ler)dir (parametre/dinamik tarih dahil).
/// <para>Nereye gideceğini KULLANICI değil üretici seçer: ifade bir toplama içeriyorsa
/// gruplama SONRASI (HAVING), içermiyorsa satır bazında (WHERE) uygulanır.</para>
/// <see cref="Baglac"/> bir önceki formül koşuluyla ilişkidir (ilkinde yok sayılır).</summary>
public sealed record ExpressionCondition(
    string Ifade,
    KarsilastirmaOp Op,
    IReadOnlyList<object?> Degerler,
    MantikBaglaci Baglac = MantikBaglaci.Ve);

/// <summary>Y1 — hesaplanan kolon: SELECT'e formül. Kolonlar <c>{Tablo.Kolon}</c> yer tutucusuyla
/// yazılır (elle SQL koşuluyla aynı dil), gerisi hesaptır:
/// <c>{Odemeler.Tutar} - {Faturalar.GenelToplam}</c> ·
/// <c>SUM({T.Donen}) * 100.0 / NULLIF(COUNT({T.Id}), 0)</c>.
/// <para>İfade bir TOPLAMA içeriyorsa grup düzeyindedir (GROUP BY'a girmez); içermiyorsa satır
/// düzeyi bir boyuttur ve aggregate'li sorguda GROUP BY'a eklenir — bileşik/CASE kolonlarıyla
/// aynı kural. Kullanılabilir fonksiyonlar <see cref="Kds.Execution"/> tarafında beyaz listedir.</para></summary>
public sealed record ComputedColumn(string Ifade, string? TakmaAd = null);

/// <summary>S22 — katalog metriği REFERANSI. <see cref="TakmaAd"/> boşsa çıktı başlığı metriğin
/// katalog adıdır. Metrik silinmiş/pasifse koşum AÇIK hata verir (sessiz kırılma yok) ve mesaj
/// hangi metriğin eksik olduğunu söyler.</summary>
public sealed record MetricField(Guid MetrikId, string? TakmaAd = null);

/// <summary>Y3 — özet koşulu (HAVING): <c>AGG(tablo.kolon) OP değer</c>. Gruplama sonrası
/// uygulanır; WHERE'e yazılamaz çünkü WHERE satırları gruplamadan ÖNCE eler.
/// <see cref="Baglac"/> bir ÖNCEKİ koşulla ilişkidir (ilk koşulda yok sayılır); soldan sağa
/// uygulanır — filtre satırlarındaki VE/VEYA ile aynı kural.</summary>
public sealed record SummaryCondition(
    ToplamaTuru Toplama,
    Guid TabloId,
    string Kolon,
    KarsilastirmaOp Op,
    IReadOnlyList<object?> Degerler,
    MantikBaglaci Baglac = MantikBaglaci.Ve);

/// <summary>Çalıştırma-anı parametresi (parametreli rapor): kullanıcı raporu çalıştırırken değer
/// girer (ör. tarih aralığı). Bir filtre koşulunun değeri sabit yerine <c>{ "$param": "anahtar" }</c>
/// ile buna bağlanır; üretici çalışma değerini (yoksa <see cref="Varsayilan"/>) kolon tipine çevirip
/// yine PARAMETRE olarak bağlar (enjeksiyon güvenliği değişmez). <see cref="Tip"/> giriş kontrolünü
/// belirler; <see cref="ParametreTipi.TarihAralik"/> tek değer {baslangic,bitis} taşır → filtreye
/// <c>parca</c> ile bağlanır. <see cref="Secenekler"/> yalnız <see cref="ParametreTipi.Liste"/>'de.</summary>
public sealed record ReportParameter(
    string Anahtar,
    string Etiket,
    ParametreTipi Tip,
    bool Zorunlu = false,
    object? Varsayilan = null,
    IReadOnlyList<ParametreSecenek>? Secenekler = null);

/// <summary>Liste parametresinin bir seçeneği (açılır menü): saklanan değer + gösterilen etiket.</summary>
public sealed record ParametreSecenek(object? Deger, string Etiket);

/// <summary>Parametre giriş tipi. Metin/Sayi/Tarih/EvetHayir tek skaler; TarihAralik tek kontrolde
/// başlangıç+bitiş (filtreye BETWEEN olarak); Liste sabit seçeneklerden biri.</summary>
public enum ParametreTipi { Metin = 0, Sayi, Tarih, TarihAralik, EvetHayir, Liste }

/// <summary>Koşullu ölçü: ana sorgunun SELECT'inde <c>AGG(CASE WHEN koşul THEN değer …)</c> üretir —
/// ör. garanti kapsamındaki talep sayısı: <c>SUM(CASE WHEN GarantiKapsamindaMi=1 THEN 1 ELSE 0 END)</c>.
/// <see cref="Kolon"/> null ise değer sabit 1'dir (adet sayımı). Topla/Say'da eşleşmeyen satır 0
/// katkı yapar; Ortalama/EnAz/EnCok'ta eşleşmeyen satır hesaba KATILMAZ (ELSE NULL). Koşul
/// kolonlarının tabloları gerekirse otomatik join'lenir.</summary>
public sealed record ConditionalMeasure(
    ToplamaTuru Toplama,
    FilterNode Filtre,
    Guid? TabloId = null,
    string? Kolon = null,
    string? TakmaAd = null);

/// <summary>M29 — satır-düzeyi CASE kolonu: SELECT'te <c>CASE WHEN k1 THEN s1 [WHEN k2 THEN s2 …]
/// [ELSE v] END</c> üretir (aggregate YOK; koşullu ölçünün satır-düzeyi karşılığı) — ör. durum
/// etiketi: <c>CASE WHEN AktifMi=1 THEN 'Aktif' ELSE 'Pasif' END</c>. Dallar sırayla denenir;
/// varsayılan (ELSE) kolon ya da sabit olabilir, ikisi de boşsa ELSE NULL. Sabitler PARAMETRE olur.
/// Aggregate'li sorguda ifade GROUP BY'a da girer (boyut ifadesidir). Koşul/sonuç kolonlarının
/// tabloları gerekirse otomatik join'lenir.</summary>
public sealed record CaseColumn(
    IReadOnlyList<CaseBranch> Dallar,
    string? TakmaAd = null,
    Guid? VarsayilanTabloId = null,
    string? VarsayilanKolon = null,
    string? VarsayilanSabit = null);

/// <summary>CASE kolonunun tek dalı: koşul (filtre ağacı) + sonuç. Sonuç ya kolon
/// (<see cref="TabloId"/>+<see cref="Kolon"/>) ya sabit (<see cref="Sabit"/>) — tam biri dolu.</summary>
public sealed record CaseBranch(
    FilterNode Filtre,
    Guid? TabloId = null,
    string? Kolon = null,
    string? Sabit = null);

/// <summary>Join türü (Join Yolları adımı): Inner = yalnız eşleşen satırlar; Left = ebeveynin
/// tüm satırları (eşleşmeyende çocuk kolonları NULL).</summary>
public enum JoinType { Inner = 0, Left }

/// <summary>Tek join koşulu: çocuk tablonun kolonu = <see cref="EbeveynTabloId"/> tablosunun kolonu.
/// Birden çoğu AND ile bağlanır — bileşik join. <see cref="EbeveynTabloId"/> null ise ayarın ANA
/// ebeveyni kullanılır; dolu ise koşul SORGUDAKİ BAŞKA bir tabloya başvurabilir (test turu M5 —
/// ör. <c>ss.ServisId = t.ServisId AND ss.ParcaId = tp.ParcaId</c>).</summary>
public sealed record JoinCondition(string TabloKolon, string EbeveynKolon, Guid? EbeveynTabloId = null);

/// <summary>Tablo başına join tanımı (Join Yolları adımı): tablo hangi EBEVEYN tabloya, hangi
/// türle, hangi koşullarla bağlanır. <see cref="Kosullar"/> boş/null ise haritadaki ilişki (varsa
/// kenar diyaloğundaki kolon seçimi) kullanılır. Ayar verilmeyen tablolar otomatik (BFS) bağlanır;
/// FROM kökü <see cref="QueryModel.KokTabloId"/> ile seçilir (null = ilk alanın tablosu).</summary>
public sealed record JoinSetting(
    Guid TabloId,
    Guid EbeveynTabloId,
    JoinType Tur = JoinType.Inner,
    IReadOnlyList<JoinCondition>? Kosullar = null,
    // Y13: JOIN'in ON'una eklenen SABİT koşullar ("yalnız aktif adresi getir"). LEFT join'de
    // ON'daki filtre ile WHERE'deki filtre FARKLI şeydir: ON'daki eşleşmeyi daraltır (eşleşmeyen
    // ana satır YİNE gelir, kolonlar NULL), WHERE'deki ana satırı ELER. INNER'da sonuç aynıdır.
    IReadOnlyList<FilterCondition>? EkKosullar = null);

/// <summary>Birleşik kolon: birden çok kolon + sabit metin ayraçları TEK çıktı kolonunda
/// (ör. Ad + ' ' + Soyad → KisiAdSoyad). Üretici <c>CONCAT(...)</c> üretir (NULL parça tümünü
/// NULL'a düşürmez); sabit metinler PARAMETRE olur (asla SQL'e gömülmez). Kolon parçalarının
/// tabloları gerekirse otomatik join'lenir.</summary>
public sealed record CompositeField(IReadOnlyList<CompositePart> Parcalar, string? TakmaAd = null);

/// <summary>Birleşik kolonun tek parçası: ya bir kolon (<see cref="TabloId"/>+<see cref="Kolon"/>)
/// ya da sabit metin (<see cref="Sabit"/>) — ikisinden tam biri dolu olmalı.</summary>
public sealed record CompositePart(Guid? TabloId = null, string? Kolon = null, string? Sabit = null);

/// <summary>İlişkili özet alan: ana sorgudan FARKLI granülaritede, kendi filtreli bir özet kolonu
/// (ör. servis başına aktif çalışan sayısı). Özet tablosu ana sorguya JOIN'lenmez (fan-trap: satır
/// çoğalması SUM'ları şişirir); üretici bunu türetilmiş tabloya çevirir:
/// <c>LEFT JOIN (SELECT bagKolon, AGG(kolon) FROM tablo WHERE filtre GROUP BY bagKolon) ON ...</c>.
/// <see cref="Filtre"/> yalnız özet tablosunun kolonlarını kullanabilir. Bağ kolonları, haritada
/// <see cref="TabloId"/> ile <see cref="BagTabloId"/> arasındaki ilişkiden gelir (yoksa hata;
/// kullanıcı join seçimi yaptıysa o geçerlidir).</summary>
public sealed record SummaryField(
    Guid TabloId,
    string Kolon,
    ToplamaTuru Toplama,
    Guid BagTabloId,
    string? TakmaAd = null,
    FilterNode? Filtre = null);

/// <summary>Kullanıcının seçtiği join: aynı tablo çifti arasında birden çok ilişki varsa (ya da
/// yanlış kolonlu bir ilişki) hangisinin kullanılacağını belirtir. Haritada TANIMLI bir ilişkiyle
/// eşleşmeli (güvenlik: keyfi join yasak). <c>null</c> = otomatik (BFS ilk bulduğu ilişki).</summary>
public sealed record JoinSelection(Guid KaynakTabloId, string KaynakKolon, Guid HedefTabloId, string HedefKolon);

/// <summary>Elle GROUP BY öğesi. Model bu listeyi taşıyorsa GROUP BY otomatik türetilmez; tam bu
/// liste kullanılır (seçili olmayan bir boyuta göre de gruplanabilir — o tablo otomatik join'lenir).
/// <c>null</c> = otomatik (aggregate varsa, aggregate-olmayan seçili kolonlar).</summary>
/// <summary>Elle GROUP BY öğesi. Y4: <see cref="Kova"/> doluysa gruplama kolonun kendisine değil
/// tarih kovasına (yıl/ay/…) yapılır — SELECT'teki indirgenmiş ifadeyle birebir aynı olmalıdır.</summary>
public sealed record GroupingItem(Guid TabloId, string Kolon, TarihKovasi? Kova = null);

/// <summary>Toplama (aggregate) türü. <see cref="Yok"/> = ham kolon (boyut). §5: SUM/AVG/MIN/MAX/COUNT.</summary>
public enum ToplamaTuru { Yok = 0, Topla, Ortalama, EnAz, EnCok, Say, BenzersizSay }

/// <summary>Seçilen çıktı alanı: haritadaki bir tablonun ham kolonu + (varsa) toplama + takma ad.
/// <para>Y4: <see cref="Kova"/> doluysa TARİH kolonu o çözünürlüğe indirgenir (yıl/çeyrek/ay/hafta/
/// gün ya da mevsimsellik parçaları) ve sorgu gruplanıyorsa GROUP BY'a indirgenmiş HÂLİ girer —
/// "aylık kırılım" için ayrı bir kolon üretmeye gerek kalmaz.</para></summary>
public sealed record SelectedField(
    Guid TabloId,
    string Kolon,
    ToplamaTuru Toplama = ToplamaTuru.Yok,
    string? TakmaAd = null,
    TarihKovasi? Kova = null);

/// <summary>Y4 — tarih kovası (çözünürlük). İki aile vardır:
/// <list type="bullet">
/// <item><b>Dönem başlangıcı</b> (<see cref="Yil"/>…<see cref="Gun"/>): değer o dönemin İLK GÜNÜdür
/// (tarih tipi) — kronolojik sıralanır, grafikte eksen olur. Hafta PAZARTESİ başlar
/// (<c>DinamikTarih</c> ile aynı kural).</item>
/// <item><b>Dönem parçası</b> (<see cref="YilNo"/>…<see cref="SaatNo"/>): yıldan bağımsız sayıdır
/// (ay 1-12, çeyrek 1-4, saat 0-23) — mevsimsellik/saatlik desen içindir.</item>
/// </list></summary>
public enum TarihKovasi
{
    Yil = 0, Ceyrek, Ay, Hafta, Gun,
    YilNo, CeyrekNo, AyNo, SaatNo,
}

/// <summary>Filtre ağacı düğümü (§5: AND/OR ağacı). Grup ya da yaprak koşul. JSON'da <c>tur</c>
/// ayrımcısıyla polimorfik serileşir.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tur")]
[JsonDerivedType(typeof(FilterGroup), "grup")]
[JsonDerivedType(typeof(FilterCondition), "kosul")]
public abstract record FilterNode;

/// <summary>AND/OR ile bağlanmış alt düğümler.</summary>
public sealed record FilterGroup(MantikBaglaci Baglac, IReadOnlyList<FilterNode> Cocuklar) : FilterNode;

/// <summary>Yaprak koşul: tablo.kolon OP değer(ler). Değerler ASLA birleştirilmez → parametre olur.
/// Değersiz operatörlerde (Bos/DoluDegil) <see cref="Degerler"/> boş liste geçilir.
/// TEK parametreli constructor: System.Text.Json'un polimorfik deserialize'ı bunu kullanabilsin
/// (ikinci bir constructor eklenirse `[JsonConstructor]` şart olur, yoksa deserialize patlar).</summary>
public sealed record FilterCondition(
    Guid TabloId,
    string Kolon,
    KarsilastirmaOp Op,
    IReadOnlyList<object?> Degerler) : FilterNode;

public enum MantikBaglaci { Ve = 0, Veya }

/// <summary>Karşılaştırma operatörü. Bos/DoluDegil değersiz; Arasinda iki değer; Icinde N değer;
/// Icerir tek değer (LIKE %değer%); kalanlar tek değer.</summary>
public enum KarsilastirmaOp { Esit = 0, EsitDegil, Buyuk, BuyukEsit, Kucuk, KucukEsit, Icinde, Arasinda, Icerir, Bos, DoluDegil }

/// <summary>Sıralama öğesi — seçili bir alanı işaret eder (tablo.kolon + toplama), yön.
/// <para>Y2: <see cref="CikisAdi"/> doluysa öğe bir ÇIKIŞ KOLONUNU işaret eder (SELECT'teki takma
/// ad) ve tablo/kolon yok sayılır — böylece hesaplanan, özet, koşullu ölçü, CASE ve birleşik
/// kolonlar da sıralanabilir (tabloya bağlı olmayan çıkışlar).</para></summary>
public sealed record SortItem(Guid TabloId, string Kolon, ToplamaTuru Toplama = ToplamaTuru.Yok, bool Azalan = false,
    string? CikisAdi = null, TarihKovasi? Kova = null);
