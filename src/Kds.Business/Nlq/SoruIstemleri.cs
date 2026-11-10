using System.Text;
using Kds.Contracts.DataMaps;
using Kds.Data.Entities;

namespace Kds.Business.Nlq;

/// <summary>Soru Sor İSTEM kurucusu — SAF: UI/IO yok, tümü test edilir. SQLST `AsistanIstemleri`
/// deseninin KDS eşi ve aynı iki kural birebir taşınır:
/// <list type="bullet">
/// <item><b>Gizlilik:</b> prompta yalnız tablo/kolon ADLARI ve TİPLERİ + metrik sözlüğü girer —
/// satır verisi, bağlantı bilgisi, sunucu adı ASLA girmez.</item>
/// <item><b>Show Prompt:</b> bu metot "gönderilecek içerik"in TEK kaynağıdır; ekranda gösterilen
/// metin modele gidenin ta kendisidir (gölge kopya yok — güven bunu gerektirir).</item>
/// </list>
/// İstem, gerçek qwen2.5-coder:7b ile kalibre edildi (20 Ağu 2026 fizibilite): metrik tercihi
/// örnekli vurgulanır (örneksiz yazınca model metriği görmezden gelip SUM'ı kendisi kuruyordu).</summary>
public static class SoruIstemleri
{
    /// <param name="oncekiModelOzeti">Çok turlu düzeltme (S30): mevcut sorgunun MODELİN KENDİ ara
    /// biçimindeki özeti (<see cref="ModelKurtarici.AraJsonaCevir"/>). İç model JSON'u DEĞİL —
    /// gerekçe orada yazılı.</param>
    /// <param name="filtreGizli">Özette filtreler temsil edilemedi (VEYA/iç içe) — istem modelden
    /// filtre alanına dokunmamasını ister.</param>
    public static string Kur(string soru, DataMapDetail harita, IReadOnlyList<Metric> metrikler,
        string? oncekiModelOzeti = null, bool filtreGizli = false)
    {
        var sb = new StringBuilder(OnekKur(harita, metrikler));
        if (!string.IsNullOrWhiteSpace(oncekiModelOzeti))
        {
            /* S30 çok turlu düzeltme: model sıfırdan kurmaz — mevcut modeli isteğe göre günceller.
             * S30c: buraya İÇ model değil, modelin KENDİ ara biçimi konur (ModelKurtarici
             * .AraJsonaCevir). İç model (GUID'li tabloId + null alanlar) gösterildiğinde model onu
             * taklit ediyor, çıktı 3.7 katına çıkıp süre 24 sn → 129 sn oluyor ve kurtarıcının
             * beklediği şema tutmadığı için limit gibi alanlar kayboluyordu (ölçüm 23 Ağu 2026). */
            sb.AppendLine("## Mevcut sorgu (kullanıcı bunun DÜZELTİLMESİNİ istiyor)");
            sb.AppendLine(oncekiModelOzeti.Trim());
            sb.AppendLine("- Cevabın YUKARIDAKİ ŞEMANIN aynısı olsun; mevcut sorguyu isteğe göre güncelle, " +
                "isteğin dokunmadığı alanları AYNEN koru.");
            // Canlı gözlem (23 Ağu 2026): 7b model "ilk 2yi göster" düzeltmesinde limit'i 2 yapmak
            // yerine SİLDİ (1000 satır döndü) — tek-alan kuralı örnekle sabitlenir.
            sb.AppendLine("- Düzeltme çoğu zaman TEK alanı değiştirir: \"ilk 2yi göster\" → yalnız \"limit\": 2 olur; " +
                "\"azalan sırala\" → yalnız \"siralama\" değişir; kırılım/hesap/filtre olduğu gibi kalır.");
            if (filtreGizli)
            {
                // Ara biçim VEYA'lı/iç içe filtreyi temsil edemez: uydurmasındansa dokunmamasını iste.
                sb.AppendLine("- Mevcut sorgunun filtreleri karmaşık olduğu için burada gösterilmedi; " +
                    "\"filtreler\" alanını BOŞ bırak — sistem eski filtreleri korur.");
            }

            sb.AppendLine();
            sb.AppendLine("## Düzeltme isteği");
            sb.Append(soru.Trim());
            return sb.ToString();
        }

        sb.AppendLine("## Soru");
        sb.Append(soru.Trim());
        return sb.ToString();
    }

    /// <summary>İstemin SORUDAN ÖNCEKİ, harita başına DEĞİŞMEYEN kısmı (şema + kurallar + metrik
    /// kataloğu + tablo listesi).
    ///
    /// <para>S34 — bu ayrımın sebebi ölçüm: 928 token'lık istemin İŞLENMESİ 49.7 sn sürüyor
    /// (19 tok/s), üretim ise 27 sn; yani sürenin %64'ü burada. Ollama aynı öneki ikinci kez
    /// işlemiyor (49.7 sn → 0.2 sn), ama bunun için önekin bayt bayt AYNI olması ve sorunun
    /// SONDA durması gerekir. Isıtma ucu (<c>NlqIsitHandler</c>) tam bu metni gönderip önbelleği
    /// kullanıcı soruyu yazmadan doldurur.</para>
    ///
    /// <para><b>Kural:</b> buraya soruya/oturuma göre değişen hiçbir şey yazılmaz — yazılırsa
    /// önbellek her soruda ıskalar ve 50 sn geri gelir.</para></summary>
    public static string OnekKur(DataMapDetail harita, IReadOnlyList<Metric> metrikler)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sen KDS raporlama ürününün sorgu kurucususun. Kullanıcının Türkçe sorusunu " +
                      "aşağıdaki JSON sorgu modeline çevir. YALNIZ geçerli JSON döndür — açıklama, markdown, kod bloğu YOK.");
        sb.AppendLine();
        /* ŞEMA KATALOĞA GÖRE KURULUR (24 Ağu 2026 ölçümü): katalog boşken "metrikler" satırını
         * şemada göstermek, ne kadar sert yasaklanırsa yasaklansın modeli kolon adını oraya
         * yazmaya itiyordu ('ToplamTutar' metrik sanıldı — iki ayrı istem sertleştirmesi de
         * yetmedi). Görmediği alanı yazamaz: alan şemadan TAMAMEN çıkarılır. */
        sb.AppendLine("## JSON şeması (yalnız KULLANDIĞIN alanları yaz)");
        sb.AppendLine("{");
        sb.AppendLine("""  "alanlar": [ { "tablo": "TabloAdi", "kolon": "KolonAdi", "toplama": "Yok|Topla|Ortalama|EnAz|EnCok|Say|BenzersizSay", "kova": "Yil|Ceyrek|Ay|Hafta|Gun" } ],""");
        if (metrikler.Count > 0)
        {
            sb.AppendLine("""  "metrikler": [ "metrik adı" ],""");
        }

        sb.AppendLine("""  "filtreler": [ { "tablo": "TabloAdi", "kolon": "KolonAdi", "op": "Esit|EsitDegil|Buyuk|BuyukEsit|Kucuk|KucukEsit|Icerir|Arasinda", "degerler": ["..."] } ],""");
        sb.AppendLine("""  "siralama": [ { "hedef": "kolon ya da metrik ÇIKIŞ ADI", "azalan": true } ],""");
        sb.AppendLine("""  "limit": 10,""");
        sb.AppendLine("""  "gorsellestirme": "Tablo|Cubuk|Cizgi|Pasta|SayiKarti" """);
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("## Kurallar");
        /* KISA ÇIKTI (24 Ağu 2026 ölçümü): üretim CPU'da ~5.8 tok/s ve SABİT — süre doğrudan
         * yazılan token sayısına bağlı. Model boş listeleri ("metrikler":[], "filtreler":[]) ve
         * null alanları ("kova":null, "limit":null) dolduruyordu; bunlar kurtarıcıda zaten
         * iyimser null olarak karşılanıyor, yani hiçbir bilgi kaybı olmadan atlanabilir. */
        sb.AppendLine("- KISA YAZ: kullanmadığın alanı HİÇ YAZMA. Boş liste ([]) ve null DEĞER YAZMA — " +
                      "o alanı tamamen atla. Kısa cevap hızlı cevaptır.");
        // Metrik kuralları yalnız katalog DOLUYKEN anlamlı; boş katalogda bu satırlar modeli
        // "metrikler" alanına kolon adı yazmaya kışkırtıyordu (24 Ağu ölçümü) + boşuna token.
        if (metrikler.Count > 0)
        {
            sb.AppendLine("- ÖNCE metrik kataloğuna bak: soru katalogdaki bir metrikle cevaplanabiliyorsa o metriğin " +
                          "ADINI \"metrikler\" listesine yaz ve AYNI hesabı alanlarla TEKRAR KURMA " +
                          "(yanlış: kolona toplama koymak; doğru: metrikler:[\"Metrik Adı\"]).");
            sb.AppendLine("- Metrik kullanınca sıralama hedefi metriğin TAM ADIdır.");
        }
        sb.AppendLine("- Kırılım (gruplama) istenen kolonları \"alanlar\"a toplama:\"Yok\" ile koy; sistem gruplamayı kendisi kurar.");
        sb.AppendLine("- Tablo/kolon adlarını SADECE aşağıdaki listeden kullan. Listede olmayan adı UYDURMA.");
        /* ZAMAN KIRILIMI (24 Ağu 2026 ölçümü): "2026'da aylık talep sayısı nasıl değişmiş?"
         * sorusuna model kova KURMADAN 1000 ham satır döndürüyordu — cevap işe yaramıyordu.
         * S23 kalibrasyon dersi: bu modelde ÖRNEKSİZ kural görmezden geliniyor; örnek şart. */
        sb.AppendLine("- ZAMAN KIRILIMI: soruda \"aylık/yıllık/haftalık/günlük/çeyreklik\", \"aya göre\", " +
                      "\"yıllara göre\", \"nasıl değişmiş\", \"trend\" geçiyorsa TARİH kolonunu kova ile " +
                      "kırılım yap, ölçüyü AYRI alan olarak koy. Örnek — \"2026'da aylık talep sayısı\":");
        sb.AppendLine("  \"alanlar\": [ {\"tablo\":\"Talepler\",\"kolon\":\"GirisTarihi\",\"toplama\":\"Yok\",\"kova\":\"Ay\"}, " +
                      "{\"tablo\":\"Talepler\",\"kolon\":\"Id\",\"toplama\":\"Say\"} ]");
        sb.AppendLine("  Kova koymazsan binlerce ham satır döner ve cevap işe yaramaz.");
        /* KOVA KURALI İSTEMDEN ÇIKARILDI (25 Ağu 2026 ölçümü). "Yıl sayı kolonundaysa kova KOYMA
         * — ör. ModelYili" diye yazınca model tam tersini yaptı: iki ardışık soruda 'ModelYili' ve
         * hatta 'Ad' kolonlarına kova uygulayıp sorguyu düşürdü. S32'de 'metrikler' alanında
         * yaşanan tuzağın aynısı: yasak cümlesi, yasakladığı şeyi ÖĞRETİYOR. Garanti artık kodda:
         * ModelKurtarici tarih olmayan kolondaki kovayı sessizce düşürüp notunu yazıyor. */
        sb.AppendLine("- Soru cevaplanamıyorsa {\"hata\":\"kısa Türkçe sebep\"} döndür.");
        sb.AppendLine("- \"gorsellestirme\": zaman serisi → Cizgi; pay/oran dili → Pasta; kategorik kırılım → Cubuk; tek değer → SayiKarti; emin değilsen Tablo.");
        sb.AppendLine();

        sb.AppendLine("## Metrik kataloğu (ad · açıklama)");
        if (metrikler.Count == 0)
        {
            /* Küçük model boş katalogda bile kolon adını "metrik" diye yazabiliyor (kullanıcı
             * bulgusu 23 Ağu: ToplamTutar). 24 Ağu: "KISA YAZ" kuralı eklenince yasak yeniden
             * delindi — "boş bırak" ifadesi artık "boş liste yazma" kuralıyla çelişiyordu.
             * Yasak tek biçimde söylenir ve DOĞRU yol örnekle gösterilir. */
            sb.AppendLine("(katalogda metrik YOK — şemada \"metrikler\" alanı zaten yok, KULLANMA. " +
                "Kolon adı metrik DEĞİLDİR: toplam/ortalama gerekiyorsa kolonu \"alanlar\"a toplama ile koy, " +
                "ör. {\"tablo\":\"Talepler\",\"kolon\":\"ToplamTutar\",\"toplama\":\"Topla\"})");
        }

        foreach (var m in metrikler)
        {
            sb.Append("- ").Append(m.Ad);
            if (!string.IsNullOrWhiteSpace(m.Aciklama))
            {
                sb.Append(" · ").Append(m.Aciklama);
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("## Tablolar (ad ve tip — satır verisi yok)");
        foreach (var t in harita.Tablolar)
        {
            // Model DOSTÇA adları görür/kullanır — kullanıcı da ekranda onları görüyor; kurtarıcı
            // hem dostça hem ham adı eşler. Şema öneki verilmez (bağlam israfı; adlar benzersiz).
            sb.Append(t.DostcaAd).Append(" (");
            sb.Append(string.Join(", ", t.Kolonlar.Select(k =>
                $"{k.DostcaAd ?? k.HamAd} {k.VeriTipi ?? "?"}")));
            sb.AppendLine(")");
        }

        /* İLİŞKİLER (S39 — kullanıcı bulgusu 25 Ağu 2026). Model bunları görmeyince çok tablolu
         * soruları kuramıyordu: "AUDİ marka" için Markalar'a bağlanıp Ad'a filtre koyması gerekirken
         * marka ADINI doğrudan MarkaId (int) kolonuna yazıyor ve sorgu "tipine çevrilemedi" diye
         * düşüyordu.
         *
         * DİKKAT — model burada JOIN YAZMAZ: üretici (MssqlQueryGenerator) kullanılan tablolar
         * arasındaki yolu haritadaki ilişkilerden BFS ile KENDİSİ kurar. Bu liste modele yalnız
         * "hangi tablo hangisine bağlı" bilgisini verir; işi doğru KOLONU seçmektir. */
        var adlar = harita.Tablolar.ToDictionary(t => t.Id, t => t.DostcaAd);
        var iliskiler = harita.Iliskiler
            .Where(i => adlar.ContainsKey(i.KaynakTabloId) && adlar.ContainsKey(i.HedefTabloId))
            .ToList();
        if (iliskiler.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Tablo ilişkileri (birleştirmeyi SİSTEM yapar — sen yazma)");
            foreach (var i in iliskiler)
            {
                sb.Append(adlar[i.KaynakTabloId]).Append('.').Append(i.KaynakKolon)
                  .Append(" → ").Append(adlar[i.HedefTabloId]).Append('.').AppendLine(i.HedefKolon);
            }

            sb.AppendLine("- Farklı tablolardaki alanları serbestçe birlikte kullanabilirsin; " +
                          "sistem yukarıdaki ilişkilerden gerekli birleştirmeyi kurar.");
            /* Bu kural örnekle veriliyor çünkü S23 kalibrasyon dersi: bu modelde ÖRNEKSİZ kural
             * görmezden geliniyor. Yaşanan tam hata örnek olarak konuluyor. */
            sb.AppendLine("- ADLA süzme kuralı: kullanıcı bir şeyin ADINI söylüyorsa (marka, şehir, " +
                          "durum, servis...) o adı \"...Id\" kolonuna YAZMA — Id sayıdır. İlgili tablonun " +
                          "AD kolonuna filtre koy. Örnek — \"AUDİ marka araçlar\":");
            sb.AppendLine("  YANLIŞ: {\"tablo\":\"Araclar\",\"kolon\":\"MarkaId\",\"op\":\"Esit\",\"degerler\":[\"AUDİ\"]}");
            sb.AppendLine("  DOĞRU : {\"tablo\":\"Markalar\",\"kolon\":\"Ad\",\"op\":\"Esit\",\"degerler\":[\"AUDİ\"]}");
            sb.AppendLine("- Aynı ada sahip kolon birden çok tabloda olabilir (ör. Tutar); hangi tabloyu " +
                          "kastettiğini \"tablo\" alanında MUTLAKA yaz.");
            /* Kullanıcı tam yazımı bilmez ("AUDİ" ↔ veride "Audi"): Esit sessizce 0 satır döndürür.
             * Icerir tarafı Türkçe harf duyarsız çalışır (üreticide COLLATE Turkish_CI_AI). */
            sb.AppendLine("- METİN süzerken \"op\":\"Icerir\" kullan, \"Esit\" değil — kullanıcı adı " +
                          "büyük/küçük harf ya da noktalama farkıyla yazmış olabilir.");
        }

        sb.AppendLine();
        return sb.ToString();
    }
}
