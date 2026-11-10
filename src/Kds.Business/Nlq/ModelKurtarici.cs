using System.Text.Json;
using System.Text.Json.Serialization;
using Kds.Contracts.DataMaps;
using Kds.Contracts.Nlq;
using Kds.Contracts.QueryModel;
using Kds.Data.Entities;
using Kds.Execution;

namespace Kds.Business.Nlq;

/// <summary>Kurtarma sonucu: kurulan model + "Nasıl hesaplandı" insan dili özeti (ekran bunu
/// madde madde gösterir — AI kara kutu değil).</summary>
public sealed record KurtarilanSorgu(QueryModel Model, IReadOnlyList<string> NasilHesaplandi, string Gorsellestirme,
    SorguDurumu? Durum = null);

/// <summary>Model çıktısı KURTARICISI — SAF: ham metin → ara JSON → doğrulanmış <see cref="QueryModel"/>.
/// AI çıktısı asla doğrudan koşulmaz; buradaki DETERMİNİSTİK kurallar 20 Ağu 2026 fizibilite
/// koşumlarıyla kalibre edildi (prompt inceltmek yerine kodda normalize etmek seçildi — model
/// değişse de davranış öngörülebilir kalır):
/// <list type="bullet">
/// <item>```json çiti format:'json' kipine RAĞMEN gelebiliyor → çit ayıklanır.</item>
/// <item>Model metrik seçip AYNI hesabı alan olarak da kurabiliyor → metrik ifadesinde geçen
/// kolona toplama koyan alan DÜŞÜRÜLÜR (çift kolon önlenir).</item>
/// <item>"Aylık ... sayısı" tek alana kova+toplama sıkıştırıyor (Y4 üreticide red) → alan
/// kırılım (kova) + ölçü (toplama) olarak İKİYE AYRILIR.</item>
/// <item>Ad eşleme toleranslı (dostça/ham ad, büyük-küçük duyarsız) ama LİSTEDE OLMAYAN ad
/// UYDURMA sayılır ve AÇIK hatayla reddedilir (fail-closed — S22 metrik diliyle aynı).</item>
/// </list></summary>
public static class ModelKurtarici
{
    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static KurtarilanSorgu Kurtar(string modelCiktisi, DataMapDetail harita, IReadOnlyList<Metric> katalog)
    {
        var taslak = Ayikla(modelCiktisi);
        if (!string.IsNullOrWhiteSpace(taslak.Hata))
        {
            throw new QueryModelException($"Model soruyu cevaplayamadı: {taslak.Hata}");
        }

        var notlar = new List<string>();

        // 1) Metrikler: ad → katalog kaydı. Katalogda olmayan ad = uydurma → açık red (yanlış
        // rakam üretmekten iyidir; kullanıcı soruyu netleştirir).
        var metrikler = new List<Metric>();
        foreach (var ad in taslak.Metrikler ?? [])
        {
            if (string.IsNullOrWhiteSpace(ad)) { continue; }

            var metrik = katalog.FirstOrDefault(m => AdEsit(m.Ad, ad))
                ?? throw new QueryModelException($"Model '{ad}' adında bir metrik kullandı; katalogda böyle bir metrik yok.");
            if (metrikler.All(m => m.Id != metrik.Id)) { metrikler.Add(metrik); }
        }

        /* BAĞLAM ÖN GEÇİŞİ (S39): "Tutar" gibi birden çok tabloda geçen kolon adları tek başına
         * belirsizdir. Model cevabın BAŞKA bir yerinde tabloyu açıkça yazdıysa (ör. filtrede
         * "TalepParcalari") kastettiği tablo odur. Bu küme önce toplanır ki alanlar hangi sırada
         * gelirse gelsin ipucu hazır olsun — tek geçişte ilk alan belirsizse bağlam henüz boş
         * kalırdı. */
        var baglam = new HashSet<Guid>();
        foreach (var ad in (taslak.Alanlar ?? []).Select(a => a.Tablo)
                     .Concat((taslak.Filtreler ?? []).Select(f => f.Tablo))
                     .Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var t = harita.Tablolar.FirstOrDefault(x => AdEsit(x.DostcaAd, ad!) || AdEsit(x.HamAd, ad!));
            if (t is not null) { baglam.Add(t.Id); }
        }

        // 2) Alanlar: ad → tablo/kolon kimliği; toplama+kova toleranslı çözümlenir.
        var alanlar = new List<SelectedField>();
        foreach (var a in taslak.Alanlar ?? [])
        {
            var (tablo, kolon) = KolonBul(harita, a.Tablo, a.Kolon, baglam);
            var toplama = ToplamaCoz(a.Toplama);
            var kova = KovaCoz(a.Kova);

            /* KOVA YALNIZ TARİH KOLONUNDA (S39, 25 Ağu 2026). Model 'ModelYili' (smallint) ve hatta
             * 'Ad' (nvarchar) kolonlarına tarih kovası uygulayıp sorguyu düşürüyordu. İstem kuralı
             * denendi ve TERS TEPTİ (yasak cümlesi yasakladığını öğretiyor — S32'deki 'metrikler'
             * tuzağının aynısı), o yüzden garanti burada: kova sessizce DÜŞER, not yazılır.
             * Sorguyu hata ile öldürmek yerine kırılımsız ama çalışan bir cevap vermek, kurtarıcının
             * "iyimser onarım" çizgisiyle aynı. */
            if (kova is not null && !TarihKolonuMu(kolon))
            {
                notlar.Add($"'{KolonAdi(tablo, kolon)}' bir tarih kolonu olmadığı için " +
                           $"{KovaAdi(kova.Value).ToLowerInvariant()} kırılımı uygulanmadı.");
                kova = null;
            }

            // Kalibrasyon: "aylık talep sayısı" → model tek alana kova:Ay + toplama:Say sıkıştırdı.
            // Y4 gereği kova bir BOYUTTUR; ikiye ayır: kırılım (kova) + ölçü (toplama).
            if (kova is not null && toplama != ToplamaTuru.Yok)
            {
                alanlar.Add(new SelectedField(tablo.Id, kolon.HamAd, ToplamaTuru.Yok, TakmaAd: null, kova));
                alanlar.Add(new SelectedField(tablo.Id, kolon.HamAd, toplama));
                continue;
            }

            alanlar.Add(new SelectedField(tablo.Id, kolon.HamAd, toplama, TakmaAd: null, kova));
        }

        // 3) Metrik seçildiyse aynı hesabı alanlarla TEKRAR kuran satırları düşür: metriğin
        // formülünde geçen kolona toplama koymuş alan çift hesaptır (kalibrasyon koşumu 2'nin
        // birebir gözlemi). Toplamasız (kırılım) alanlar dokunulmaz.
        if (metrikler.Count > 0)
        {
            alanlar.RemoveAll(a =>
            {
                if (a.Toplama == ToplamaTuru.Yok) { return false; }

                var t = harita.Tablolar.First(x => x.Id == a.TabloId);
                var k = t.Kolonlar.First(x => AdEsit(x.HamAd, a.Kolon));
                var cift = metrikler.Any(m => IfadeKolonaDokunur(m.Ifade, t, k));
                if (cift)
                {
                    notlar.Add($"'{KolonAdi(t, k)}' üzerindeki hesap metrikle çakıştığı için atlandı (metrik esas alındı).");
                }

                return cift;
            });
        }

        // Aynı alan iki kez geldiyse tekille (model bazen filtre kolonunu da yineliyor).
        alanlar = alanlar
            .GroupBy(a => (a.TabloId, Kolon: a.Kolon.ToUpperInvariant(), a.Toplama, a.Kova))
            .Select(g => g.First())
            .ToList();

        if (alanlar.Count == 0 && metrikler.Count == 0)
        {
            throw new QueryModelException("Sorudan ne bir alan ne bir metrik çıkarılabildi — soruyu biraz daha somut sorar mısın?");
        }

        // 4) Filtreler → VE ile bağlı düz grup (v1: model iç içe VE/VEYA üretmiyor).
        var kosullar = new List<FilterNode>();
        foreach (var f in taslak.Filtreler ?? [])
        {
            var (tablo, kolon) = KolonBul(harita, f.Tablo, f.Kolon, baglam);
            var op = OpCoz(f.Op);
            var degerler = (f.Degerler ?? []).Select(DegerCoz).ToList();

            // Değer sayısı operatörle tutarlı hale getirilir (toleranslı ama deterministik).
            if (op == KarsilastirmaOp.Esit && degerler.Count > 1) { op = KarsilastirmaOp.Icinde; }
            if (op == KarsilastirmaOp.Arasinda && degerler.Count != 2)
            {
                throw new QueryModelException($"'{KolonAdi(tablo, kolon)}' için aralık filtresi 2 değer ister; model {degerler.Count} değer verdi.");
            }

            if (op is KarsilastirmaOp.Bos or KarsilastirmaOp.DoluDegil) { degerler = []; }
            else if (degerler.Count == 0)
            {
                throw new QueryModelException($"'{KolonAdi(tablo, kolon)}' filtresi değersiz geldi; soru netleştirilmeli.");
            }

            /* METİN EŞİTLİĞİ → İÇERİR (S39, 25 Ağu 2026). Model, isteme yazılan kurala rağmen metin
             * kolonlarında "Esit" kullanmaya devam ediyor; veride "Audi" yazılıyken kullanıcı "AUDİ"
             * dediğinde eşitlik SESSİZCE 0 satır döndürüyor (ölçüldü: sorgu 200, toplam null).
             * 'İçerir' üreticide Türkçe harf duyarsız çalışır ve yazım farkını yutar.
             *
             * YALNIZ NLQ yolunda: bu dönüşüm kurtarıcıdadır, tasarımcıda ELLE kurulan filtreye
             * dokunmaz — orada kullanıcı tam eşitliği bilerek seçmiştir. */
            if (op == KarsilastirmaOp.Esit && degerler is [string s] && !string.IsNullOrWhiteSpace(s)
                && (kolon.VeriTipi ?? "").Contains("char", StringComparison.OrdinalIgnoreCase))
            {
                op = KarsilastirmaOp.Icerir;
                notlar.Add($"'{KolonAdi(tablo, kolon)}' metin olduğu için eşitlik yerine " +
                           "\"içerir\" ile süzüldü (büyük/küçük harf ve Türkçe karakter farkı yutulur).");
            }

            /* AD → Id ONARIMI (S39, 25 Ağu 2026). Modelin en sık ve en yıkıcı hatası: kullanıcı
             * "AUDİ marka" der, model 'MarkaId = "AUDİ"' kurar; sorgu "int'e çevrilemedi" diye
             * ölür. İstem kuralı yazıldı ama model tutarsız — bazen doğru, bazen yine Id'ye metin.
             * Onarım burada deterministik: metin, sayısal Id kolonuna yazılmışsa filtre İLİŞKİLİ
             * TABLONUN AD kolonuna taşınır. Üretici join'i zaten kendi kurar, o yüzden tablo
             * değiştirmek yeterli — model bunu bilmek zorunda değil. */
            var (hedefTablo, adKolonu) = AdKolonunaTasi(harita, tablo, kolon, degerler);
            if (hedefTablo is not null && adKolonu is not null)
            {
                notlar.Add($"'{degerler[0]}' bir sayı değil; '{KolonAdi(tablo, kolon)}' yerine " +
                           $"{hedefTablo.DostcaAd} · {KolonAdi(hedefTablo, adKolonu)} üzerinden süzüldü.");
                // 'İçerir': kullanıcı adı büyük/küçük harf ya da Türkçe 'İ' farkıyla yazmış olabilir
                // (üretici bu operatörde Türkçe harf duyarsız karşılaştırır).
                kosullar.Add(new FilterCondition(hedefTablo.Id, adKolonu.HamAd, KarsilastirmaOp.Icerir,
                    [degerler[0]]));
                continue;
            }

            kosullar.Add(new FilterCondition(tablo.Id, kolon.HamAd, op, degerler));
        }

        FilterNode? filtre = kosullar.Count switch
        {
            0 => null,
            1 => kosullar[0],
            _ => new FilterGroup(MantikBaglaci.Ve, kosullar),
        };

        // 5) Sıralama: hedef önce metrik adına (ÇIKIŞ ADI), sonra seçili alanlara eşlenir.
        // Eşlenemeyen hedef soruyu düşürmez — sıralama süs, rakam değil: atla + notla.
        var siralama = new List<SortItem>();
        foreach (var s in taslak.Siralama ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Hedef)) { continue; }

            var metrik = metrikler.FirstOrDefault(m => AdEsit(m.Ad, s.Hedef));
            if (metrik is not null)
            {
                siralama.Add(new SortItem(Guid.Empty, string.Empty, ToplamaTuru.Yok, s.Azalan, CikisAdi: metrik.Ad));
                continue;
            }

            var alan = alanlar.FirstOrDefault(a =>
            {
                var t = harita.Tablolar.First(x => x.Id == a.TabloId);
                var k = t.Kolonlar.First(x => AdEsit(x.HamAd, a.Kolon));
                return AdEsit(k.HamAd, s.Hedef) || AdEsit(k.DostcaAd ?? "", s.Hedef)
                    || AdEsit($"{t.DostcaAd}.{k.DostcaAd ?? k.HamAd}", s.Hedef);
            });
            if (alan is not null)
            {
                siralama.Add(new SortItem(alan.TabloId, alan.Kolon, alan.Toplama, s.Azalan, CikisAdi: null, alan.Kova));
                continue;
            }

            notlar.Add($"Sıralama hedefi '{s.Hedef}' eşlenemedi; sıralama atlandı.");
        }

        var model = new QueryModel(
            harita.Id,
            alanlar,
            filtre,
            siralama,
            SatirLimiti: taslak.Limit is > 0 ? taslak.Limit : null,
            MetrikAlanlar: metrikler.Count > 0 ? metrikler.Select(m => new MetricField(m.Id)).ToList() : null);
        // Gruplamalar bilerek null: toplama/metrik varken üretici toplamasız alanları kendisi
        // gruplar — modelden gruplama İSTEMEMEK, kalibrasyonun en sağlam kararıydı.

        // S30 — görünüm önerisi: model önerdiyse doğrula, yoksa DETERMİNİSTİK tahmin.
        // Kural sırası kalibre: kova (zaman) → Çizgi · kırılımsız tek ölçü → Sayı Kartı ·
        // kırılım + ölçü → Çubuk · gerisi Tablo. Pasta yalnız model isterse (pay/oran dili).
        var gecerliGorunumler = new[] { "Tablo", "Cubuk", "Cizgi", "Pasta", "SayiKarti" };
        var oneri = gecerliGorunumler.FirstOrDefault(
            g => string.Equals(g, taslak.Gorsellestirme?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (oneri is null)
        {
            var kovaVar = alanlar.Any(a => a.Kova is not null);
            var olcuVar = metrikler.Count > 0 || alanlar.Any(a => a.Toplama != ToplamaTuru.Yok);
            var kirilimVar = alanlar.Any(a => a.Toplama == ToplamaTuru.Yok);
            oneri = kovaVar ? "Cizgi"
                : olcuVar && !kirilimVar ? "SayiKarti"
                : olcuVar ? "Cubuk"
                : "Tablo";
        }
        notlar.Add($"Önerilen görünüm: {oneri}");
        IdTahminiUyar(model.Filtre, harita, notlar);

        return new KurtarilanSorgu(model, Ozetle(model, harita, metrikler, notlar), oneri,
            DurumCikar(model, harita, metrikler));
    }

    /// <summary>S39 — SESSİZ YANLIŞ UYARISI. Model bir <c>...Id</c> kolonuna doğrudan SAYI yazdıysa
    /// bu neredeyse her zaman bir TAHMİNDİR: kullanıcı "AUDİ marka" der, model 'MarkaId = 1' kurar
    /// ve sorgu hatasız çalışıp YANLIŞ markanın rakamını döndürür.
    ///
    /// <para>Bu, çevrilemeyen sorudan daha tehlikelidir — hata mesajı yoktur, sayı doğru görünür.
    /// Sorgu engellenmez (kullanıcı bilerek Id ile süzüyor olabilir) ama "Nasıl hesaplandı"da
    /// açıkça işaretlenir; ekran bu satırları zaten kanıt şeridinde basar.</para>
    ///
    /// <para>Yalnız hedef tabloda AD niteliğinde bir metin kolonu varsa uyarılır: ad ile süzmenin
    /// mümkün olduğu durumda Id yazmak şüphelidir. Kod tablosu olmayan bir Id'de (ör. serbest
    /// numara alanı) uyarı gürültü olurdu.</para></summary>
    private static void IdTahminiUyar(FilterNode? filtre, DataMapDetail harita, List<string> notlar)
    {
        if (filtre is null)
        {
            return;
        }

        foreach (var k in Kosullar(filtre))
        {
            if (!k.Kolon.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                || k.Op is not (KarsilastirmaOp.Esit or KarsilastirmaOp.Icinde)
                || k.Degerler is not { Count: > 0 } d
                || !d.All(v => v is not null && int.TryParse(v.ToString(), out _)))
            {
                continue;
            }

            // Id hangi tabloyu gösteriyor? Haritadaki ilişkiden bulunur; yoksa uyarma.
            var iliski = harita.Iliskiler.FirstOrDefault(i =>
                (i.KaynakTabloId == k.TabloId && i.KaynakKolon.Equals(k.Kolon, StringComparison.OrdinalIgnoreCase))
                || (i.HedefTabloId == k.TabloId && i.HedefKolon.Equals(k.Kolon, StringComparison.OrdinalIgnoreCase)));
            if (iliski is null)
            {
                continue;
            }

            var hedefId = iliski.KaynakTabloId == k.TabloId ? iliski.HedefTabloId : iliski.KaynakTabloId;
            var hedef = harita.Tablolar.FirstOrDefault(t => t.Id == hedefId);
            var adKolon = hedef?.Kolonlar.FirstOrDefault(c =>
                (c.DostcaAd ?? c.HamAd).Contains("Ad", StringComparison.OrdinalIgnoreCase)
                && (c.VeriTipi ?? "").Contains("char", StringComparison.OrdinalIgnoreCase));
            if (hedef is null || adKolon is null)
            {
                continue;
            }

            var kaynakTablo = harita.Tablolar.FirstOrDefault(t => t.Id == k.TabloId);
            var kaynakKolon = kaynakTablo?.Kolonlar.FirstOrDefault(c =>
                c.HamAd.Equals(k.Kolon, StringComparison.OrdinalIgnoreCase));
            var kolonAd = kaynakTablo is not null && kaynakKolon is not null
                ? KolonAdi(kaynakTablo, kaynakKolon) : k.Kolon;
            notlar.Add($"⚠ '{kolonAd}' için {string.Join(", ", d)} değeri MODELİN TAHMİNİDİR — " +
                       $"adıyla süzmek istiyorsanız soruyu \"{hedef.DostcaAd} adı ...\" diye yazın; " +
                       "rakamı kullanmadan önce doğrulayın.");
        }
    }

    /// <summary>S31 — sorgunun YAPILANDIRILMIŞ hâli (durum rayı + atıf satırı için).
    /// <see cref="Ozetle"/> ile aynı bilgiyi taşır; fark, ekranın metin ayrıştırmak zorunda kalmaması.
    /// Kaldırılabilirlik: kırılım/filtre/sıralama/limit tek tıkla atılabilir, metrik ve hesap ise
    /// sorgunun kendisidir — atılırsa geriye cevap kalmaz, o yüzden kilitli.</summary>
    private static SorguDurumu DurumCikar(QueryModel model, DataMapDetail harita, IReadOnlyList<Metric> metrikler)
    {
        var cipler = new List<DurumCipi>();
        var tablolar = new List<string>();

        void TabloYaz(Guid id)
        {
            var t = harita.Tablolar.FirstOrDefault(x => x.Id == id);
            var ad = t?.DostcaAd ?? t?.HamAd;
            if (ad is not null && !tablolar.Contains(ad)) { tablolar.Add(ad); }
        }

        foreach (var m in metrikler)
        {
            cipler.Add(new DurumCipi("metrik", m.Ad, Kaldirilabilir: false));
        }

        foreach (var a in model.Alanlar)
        {
            TabloYaz(a.TabloId);
            var t = harita.Tablolar.First(x => x.Id == a.TabloId);
            var ad = KolonAdi(t, t.Kolonlar.First(x => AdEsit(x.HamAd, a.Kolon)));
            cipler.Add(a.Toplama == ToplamaTuru.Yok
                ? new DurumCipi("kirilim", a.Kova is null ? ad : $"{ad} ({KovaAdi(a.Kova.Value)})")
                : new DurumCipi("hesap", $"{ToplamaAdi(a.Toplama)} — {ad}", Kaldirilabilir: false));
        }

        foreach (var k in Kosullar(model.Filtre))
        {
            TabloYaz(k.TabloId);
            var t = harita.Tablolar.First(x => x.Id == k.TabloId);
            var kol = t.Kolonlar.First(x => AdEsit(x.HamAd, k.Kolon));
            var degerler = string.Join(" – ", k.Degerler.Select(d => d?.ToString() ?? "boş"));
            cipler.Add(new DurumCipi("filtre",
                $"{KolonAdi(t, kol)} {OpAdi(k.Op)}{(k.Degerler.Count > 0 ? " " + degerler : "")}"));
        }

        foreach (var s in model.Siralama)
        {
            string hedef;
            if (s.CikisAdi is not null) { hedef = s.CikisAdi; }
            else
            {
                var t = harita.Tablolar.First(x => x.Id == s.TabloId);
                hedef = KolonAdi(t, t.Kolonlar.First(x => AdEsit(x.HamAd, s.Kolon)));
            }

            cipler.Add(new DurumCipi("siralama", $"{hedef} {(s.Azalan ? "↓" : "↑")}"));
        }

        if (model.SatirLimiti is not null)
        {
            cipler.Add(new DurumCipi("limit", $"ilk {model.SatirLimiti} satır"));
        }

        return new SorguDurumu(
            harita.Ad,
            tablolar,
            metrikler.Select(m => new MetrikAtfi(m.Id, m.Ad, m.GuncellemeTarihi ?? m.OlusturmaTarihi)).ToList(),
            cipler);
    }

    /// <summary>Kurtarmanın TERSİ (S30c): iç modeli, modelin KENDİ ürettiği ara JSON biçimine
    /// çevirir — çok turlu düzeltmede isteme bu konur.
    ///
    /// <para><b>Neden gerekli (ölçümle bulundu, 23 Ağu 2026):</b> düzeltme isteminde iç
    /// <see cref="QueryModel"/> ham hâliyle (GUID'li tabloId'ler + onlarca null alan)
    /// gösteriliyordu. Model bu yabancı biçimi TAKLİT etmeye başlıyor: üretilen token 145 → 538
    /// (3.7 kat, CPU'da ~5.5 tok/s ile 24 sn → 129 sn) ve çıktı kurtarıcının beklediği şemada
    /// olmadığı için alanlar (limit dahil) kayboluyordu. Yani yavaşlık ve kalite kaybı TEK kök
    /// nedenden geliyordu. Model kendi diliyle konuşturulunca ikisi de düzelir.</para>
    ///
    /// <para>Yalnız NLQ'nun ürettiği dar alt küme çevrilir (alan/metrik/filtre/sıralama/limit);
    /// tasarımcıda kurulmuş zengin modeller bu yoldan geçmez. VEYA'lı ya da iç içe filtre ağacı
    /// ara biçimde temsil edilemez — o durumda filtreler <c>null</c> bırakılır ve çağıran, modele
    /// "filtreye dokunma" diye söyler (uydurma filtre üretmesindense eksik göstermek yeğdir).</para></summary>
    public static string AraJsonaCevir(QueryModel model, DataMapDetail harita, IReadOnlyList<Metric> katalog)
    {
        string? TabloAd(Guid id) => harita.Tablolar.FirstOrDefault(t => t.Id == id) is { } t
            ? t.DostcaAd ?? t.HamAd : null;
        string KolonAd(Guid tabloId, string hamAd) =>
            harita.Tablolar.FirstOrDefault(t => t.Id == tabloId)?.Kolonlar
                .FirstOrDefault(k => string.Equals(k.HamAd, hamAd, StringComparison.OrdinalIgnoreCase)) is { } k
                ? k.DostcaAd ?? k.HamAd : hamAd;

        var alanlar = model.Alanlar.Select(a => new NlqTaslakAlan(
            TabloAd(a.TabloId), KolonAd(a.TabloId, a.Kolon),
            a.Toplama.ToString(), a.Kova?.ToString())).ToList();

        var metrikler = (model.MetrikAlanlar ?? [])
            .Select(m => katalog.FirstOrDefault(k => k.Id == m.MetrikId)?.Ad)
            .Where(ad => ad is not null).Select(ad => ad!).ToList();

        var siralama = model.Siralama.Select(s => new NlqTaslakSiralama(
            s.CikisAdi ?? KolonAd(s.TabloId, s.Kolon), s.Azalan)).ToList();

        // Yalnız düz VE-listesi (ya da tek yaprak) temsil edilebilir; gerisi null.
        List<NlqTaslakFiltre>? filtreler = model.Filtre switch
        {
            FilterCondition tek => [FiltreyeCevir(tek, TabloAd, KolonAd)],
            FilterGroup { Baglac: MantikBaglaci.Ve } g when g.Cocuklar.All(c => c is FilterCondition) =>
                g.Cocuklar.Cast<FilterCondition>().Select(c => FiltreyeCevir(c, TabloAd, KolonAd)).ToList(),
            null => [],
            _ => null,
        };

        return JsonSerializer.Serialize(new NlqTaslak(
            alanlar, metrikler, filtreler, siralama, model.SatirLimiti, Gorsellestirme: null, Hata: null),
            AraJsonAyar);
    }

    /// <summary>Ara biçimde filtre temsil edilemiyor mu? (VEYA / iç içe grup) — istem bunu
    /// bilerek "filtreye dokunma" der; modelden temsil edemediğimiz bir şeyi korumasını istemek
    /// sessiz veri kaybı olurdu.</summary>
    public static bool FiltreTemsilEdilemez(QueryModel model) => model.Filtre switch
    {
        null or FilterCondition => false,
        FilterGroup { Baglac: MantikBaglaci.Ve } g => !g.Cocuklar.All(c => c is FilterCondition),
        _ => true,
    };

    private static NlqTaslakFiltre FiltreyeCevir(
        FilterCondition k, Func<Guid, string?> tabloAd, Func<Guid, string, string> kolonAd) =>
        new(tabloAd(k.TabloId), kolonAd(k.TabloId, k.Kolon), k.Op.ToString(),
            k.Degerler.Select(d => JsonSerializer.SerializeToElement(d)).ToList());

    private static readonly JsonSerializerOptions AraJsonAyar = new()
    {
        // camelCase: istemdeki şema örneği de küçük harfli — model iki farklı yazım görmesin.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>```json ... ``` çitini ayıklar, ilk '{' ile son '}' arasını çözer. format:'json'
    /// kipine rağmen çit gelebildiği fizibilitede görüldü — parser çite dayanıklı olmak zorunda.</summary>
    private static NlqTaslak Ayikla(string ham)
    {
        var bas = ham.IndexOf('{');
        var son = ham.LastIndexOf('}');
        if (bas < 0 || son <= bas)
        {
            throw new QueryModelException("Modelin cevabında çözümlenebilir JSON bulunamadı — yerel model beklenmedik bir çıktı verdi.");
        }

        try
        {
            return JsonSerializer.Deserialize<NlqTaslak>(ham[bas..(son + 1)], JsonAyar)
                ?? throw new QueryModelException("Modelin cevabı boş geldi.");
        }
        catch (JsonException)
        {
            throw new QueryModelException("Modelin cevabı geçerli JSON değil — soruyu yeniden sormayı dene.");
        }
    }

    /// <param name="baglam">S39 — modelin AYNI cevapta tablosunu AÇIKÇA belirttiği tablolar.
    /// "Tutar" gibi birden çok tabloda geçen bir kolon adı tek başına belirsizdir; ama model
    /// cevabın başka bir yerinde "TalepParcalari" dediyse kastettiği tablo odur. Bu ipucu
    /// olmadan kurtarıcı hata veriyordu ve kullanıcı soruyu yeniden yazmak zorunda kalıyordu.</param>
    private static (DataMapTableDto Tablo, DataMapColumnDto Kolon) KolonBul(
        DataMapDetail harita, string? tabloAd, string? kolonAd, IReadOnlySet<Guid>? baglam = null)
    {
        if (string.IsNullOrWhiteSpace(kolonAd))
        {
            throw new QueryModelException("Model kolonsuz bir alan üretti; soru netleştirilmeli.");
        }

        // Tablo adı verilmişse ondan; verilmemiş/eşleşmemişse TÜM tablolarda tekil kolon aranır.
        var tablo = string.IsNullOrWhiteSpace(tabloAd)
            ? null
            : harita.Tablolar.FirstOrDefault(t => AdEsit(t.DostcaAd, tabloAd!) || AdEsit(t.HamAd, tabloAd!));

        if (tablo is not null)
        {
            var kolon = KolonAra(tablo, kolonAd!);
            if (kolon is not null) { return (tablo, kolon); }
        }

        var adaylar = harita.Tablolar
            .Select(t => (Tablo: t, Kolon: KolonAra(t, kolonAd!)))
            .Where(x => x.Kolon is not null)
            .ToList();

        /* Belirsizliği BAĞLAMLA çöz — ama yalnız TEK aday kalıyorsa. Bağlamda iki tablo da varsa
         * seçim yine tahmindir ve yanlış tablodan gelen rakam sessiz yanlıştır: o durumda hata
         * vermek doğrudur. */
        if (adaylar.Count > 1 && baglam is { Count: > 0 })
        {
            var baglamli = adaylar.Where(a => baglam.Contains(a.Tablo.Id)).ToList();
            if (baglamli.Count == 1)
            {
                return (baglamli[0].Tablo, baglamli[0].Kolon!);
            }
        }

        return adaylar.Count switch
        {
            1 => (adaylar[0].Tablo, adaylar[0].Kolon!),
            0 => throw new QueryModelException($"Model '{tabloAd}.{kolonAd}' diye bir kolon kullandı; haritada böyle bir kolon yok."),
            _ => throw new QueryModelException($"'{kolonAd}' kolonu haritada birden çok tabloda var; model tabloyu belirtmedi — soruda tabloyu da söyler misin?"),
        };
    }

    private static DataMapColumnDto? KolonAra(DataMapTableDto tablo, string ad)
        => tablo.Kolonlar.FirstOrDefault(k => AdEsit(k.HamAd, ad) || AdEsit(k.DostcaAd ?? "", ad));

    private static bool AdEsit(string a, string b)
        => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Metrik formülü bu kolona dokunuyor mu? Formül {Tablo.Kolon} atıflı Y1 ifadesidir;
    /// dostça ya da ham adla yazılmış olabilir — ikisi de aranır.</summary>
    private static bool IfadeKolonaDokunur(string ifade, DataMapTableDto tablo, DataMapColumnDto kolon)
        => ifade.Contains($"{tablo.DostcaAd}.{kolon.DostcaAd ?? kolon.HamAd}", StringComparison.OrdinalIgnoreCase)
        || ifade.Contains($"{tablo.HamAd}.{kolon.HamAd}", StringComparison.OrdinalIgnoreCase)
        || ifade.Contains($"{tablo.DostcaAd}.{kolon.HamAd}", StringComparison.OrdinalIgnoreCase);

    private static ToplamaTuru ToplamaCoz(string? ham) => (ham ?? "").Trim().ToLowerInvariant() switch
    {
        "" or "yok" or "none" => ToplamaTuru.Yok,
        "topla" or "toplam" or "sum" => ToplamaTuru.Topla,
        "ortalama" or "avg" or "average" => ToplamaTuru.Ortalama,
        "enaz" or "min" => ToplamaTuru.EnAz,
        "encok" or "max" => ToplamaTuru.EnCok,
        "say" or "sayi" or "count" => ToplamaTuru.Say,
        "benzersizsay" or "distinctcount" or "countdistinct" => ToplamaTuru.BenzersizSay,
        var b => throw new QueryModelException($"Model bilinmeyen bir toplama türü kullandı: '{b}'."),
    };

    private static TarihKovasi? KovaCoz(string? ham) => (ham ?? "").Trim().ToLowerInvariant() switch
    {
        "" or "null" or "yok" => null,
        "yil" or "year" => TarihKovasi.Yil,
        "ceyrek" or "quarter" => TarihKovasi.Ceyrek,
        "ay" or "month" => TarihKovasi.Ay,
        "hafta" or "week" => TarihKovasi.Hafta,
        "gun" or "day" => TarihKovasi.Gun,
        var b => throw new QueryModelException($"Model bilinmeyen bir tarih kovası kullandı: '{b}'."),
    };

    private static KarsilastirmaOp OpCoz(string? ham) => (ham ?? "").Trim().ToLowerInvariant() switch
    {
        "esit" or "=" or "==" or "eq" => KarsilastirmaOp.Esit,
        "esitdegil" or "!=" or "<>" or "ne" => KarsilastirmaOp.EsitDegil,
        "buyuk" or ">" or "gt" => KarsilastirmaOp.Buyuk,
        "buyukesit" or ">=" or "gte" => KarsilastirmaOp.BuyukEsit,
        "kucuk" or "<" or "lt" => KarsilastirmaOp.Kucuk,
        "kucukesit" or "<=" or "lte" => KarsilastirmaOp.KucukEsit,
        "icinde" or "in" => KarsilastirmaOp.Icinde,
        "arasinda" or "between" => KarsilastirmaOp.Arasinda,
        "icerir" or "like" or "contains" => KarsilastirmaOp.Icerir,
        "bos" or "null" or "isnull" => KarsilastirmaOp.Bos,
        "doludegil" or "notnull" or "dolu" => KarsilastirmaOp.DoluDegil,
        var b => throw new QueryModelException($"Model bilinmeyen bir karşılaştırma kullandı: '{b}'."),
    };

    /// <summary>Filtre değeri: sayı/bool JSON'dan METİN olarak alınır — tasarımcı ekranı da
    /// değerleri metin gönderir; kanıtlı yol odur (tip dönüşümü üretici/parametre katmanında).</summary>
    private static object? DegerCoz(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => e.GetString(),
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        _ => e.GetRawText(),
    };

    private static string KolonAdi(DataMapTableDto tablo, DataMapColumnDto kolon)
        => kolon.DostcaAd ?? $"{tablo.DostcaAd}.{kolon.HamAd}";

    /// <summary>"Nasıl hesaplandı" — dostça adlarla madde madde; kurtarma notları en sona.</summary>
    private static List<string> Ozetle(QueryModel model, DataMapDetail harita, IReadOnlyList<Metric> metrikler, List<string> notlar)
    {
        var ozet = new List<string>();
        foreach (var m in metrikler)
        {
            ozet.Add($"Metrik: {m.Ad} (katalogdan; formül koşum anında çözüldü)");
        }

        foreach (var a in model.Alanlar)
        {
            var t = harita.Tablolar.First(x => x.Id == a.TabloId);
            var k = t.Kolonlar.First(x => AdEsit(x.HamAd, a.Kolon));
            var ad = KolonAdi(t, k);
            if (a.Toplama == ToplamaTuru.Yok)
            {
                ozet.Add(a.Kova is null ? $"Kırılım: {ad}" : $"Kırılım: {ad} ({KovaAdi(a.Kova.Value)})");
            }
            else
            {
                ozet.Add($"Hesap: {ToplamaAdi(a.Toplama)} — {ad}");
            }
        }

        foreach (var k in Kosullar(model.Filtre))
        {
            var t = harita.Tablolar.First(x => x.Id == k.TabloId);
            var kol = t.Kolonlar.First(x => AdEsit(x.HamAd, k.Kolon));
            var degerler = string.Join(" – ", k.Degerler.Select(d => d?.ToString() ?? "boş"));
            ozet.Add($"Filtre: {KolonAdi(t, kol)} {OpAdi(k.Op)}{(k.Degerler.Count > 0 ? " " + degerler : "")}");
        }

        foreach (var s in model.Siralama)
        {
            string hedef;
            if (s.CikisAdi is not null) { hedef = s.CikisAdi; }
            else
            {
                var t = harita.Tablolar.First(x => x.Id == s.TabloId);
                hedef = KolonAdi(t, t.Kolonlar.First(x => AdEsit(x.HamAd, s.Kolon)));
            }

            ozet.Add($"Sıralama: {hedef} ({(s.Azalan ? "azalan" : "artan")})");
        }

        if (model.SatirLimiti is not null)
        {
            ozet.Add($"Limit: ilk {model.SatirLimiti} satır");
        }

        ozet.AddRange(notlar);
        return ozet;
    }

    private static IEnumerable<FilterCondition> Kosullar(FilterNode? dugum) => dugum switch
    {
        null => [],
        FilterCondition k => [k],
        FilterGroup g => g.Cocuklar.SelectMany(Kosullar),
        _ => [],
    };

    private static string ToplamaAdi(ToplamaTuru t) => t switch
    {
        ToplamaTuru.Topla => "toplam",
        ToplamaTuru.Ortalama => "ortalama",
        ToplamaTuru.EnAz => "en az",
        ToplamaTuru.EnCok => "en çok",
        ToplamaTuru.Say => "sayım",
        ToplamaTuru.BenzersizSay => "benzersiz sayım",
        _ => t.ToString(),
    };

    /* JOIN YOLU BELİRSİZLİĞİ UYARISI — DENENDİ VE GERİ ALINDI (25 Ağu 2026).
     *
     * Gerçek kusur şudur: üretici, sorgudaki tabloları haritadaki ilişkilerden BFS ile birleştirir;
     * bir tabloya BİRDEN ÇOK eşit uzunlukta yol varsa ilk bulunan seçilir. "AUDİ marka araçların
     * tutarı" sorusunda Markalar'a `Araclar.MarkaId` yerine `Servisler.AnaBayiMarkaId` üzerinden
     * bağlanıldı — aracın markası değil SERVİSİN BAYİ MARKASI süzüldü, hiçbir hata çıkmadan.
     *
     * Kurtarıcıdan uyarmak denendi: kullanılan tabloların komşularına bakıp "birden çok yol var"
     * demek. Ölçümde GÜRÜLTÜLÜ çıktı — tek soruda 3 uyarı, çoğu alakasız (ör. TalepParcalari için
     * uyarıyor ama Talepler zaten sorguda). Sebep yapısal: kurtarıcı üreticinin SEÇTİĞİ yolu
     * bilmez, yalnız olasılıkları görür. Gürültü, gerçek uyarıyı (Markalar) gizliyordu.
     *
     * Doğru çözüm ÜRETİCİDEDİR: yol seçerken alternatif olup olmadığını bilen tek yer orası;
     * seçtiği yolu sonuçla birlikte rapor etmeli (QueryResult'a taşınacak bilgi). Roadmap'te
     * açık borç olarak duruyor. */

    /// <summary>S39 — "AUDİ" gibi bir METİN, sayısal <c>...Id</c> kolonuna filtre olarak yazılmışsa
    /// bunu taşıyabileceğimiz AD kolonunu bulur; taşınamıyorsa (null, null) döner ve filtre olduğu
    /// gibi kalır (üretici anlamlı hatasını verir).
    ///
    /// <para>Koşullar bilerek dar: değer TEK ve sayı DEĞİL, kolon sayısal bir <c>...Id</c>, haritada
    /// o kolondan giden bir ilişki VAR ve hedef tabloda metin tipli bir AD kolonu VAR. Biri bile
    /// tutmuyorsa dokunulmaz — yanlış tabloya taşınan bir filtre, çalışan ama YANLIŞ rakam
    /// üretirdi; sessiz yanlış, açık hatadan kötüdür.</para></summary>
    private static (DataMapTableDto? Tablo, DataMapColumnDto? Kolon) AdKolonunaTasi(
        DataMapDetail harita, DataMapTableDto tablo, DataMapColumnDto kolon, IReadOnlyList<object?> degerler)
    {
        if (degerler.Count != 1 || degerler[0] is not string metin || string.IsNullOrWhiteSpace(metin)
            || int.TryParse(metin, out _)
            || !kolon.HamAd.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || (kolon.VeriTipi ?? "").ToLowerInvariant() is not ("int" or "bigint" or "smallint" or "tinyint"))
        {
            return (null, null);
        }

        var iliski = harita.Iliskiler.FirstOrDefault(i =>
            (i.KaynakTabloId == tablo.Id && i.KaynakKolon.Equals(kolon.HamAd, StringComparison.OrdinalIgnoreCase))
            || (i.HedefTabloId == tablo.Id && i.HedefKolon.Equals(kolon.HamAd, StringComparison.OrdinalIgnoreCase)));
        if (iliski is null)
        {
            return (null, null);
        }

        var hedefId = iliski.KaynakTabloId == tablo.Id ? iliski.HedefTabloId : iliski.KaynakTabloId;
        var hedef = harita.Tablolar.FirstOrDefault(t => t.Id == hedefId);
        var ad = hedef?.Kolonlar.FirstOrDefault(c =>
            (c.VeriTipi ?? "").Contains("char", StringComparison.OrdinalIgnoreCase)
            && (c.HamAd.Equals("Ad", StringComparison.OrdinalIgnoreCase)
                || c.HamAd.EndsWith("Adi", StringComparison.OrdinalIgnoreCase)
                || c.HamAd.Equals("Isim", StringComparison.OrdinalIgnoreCase)));

        return ad is null ? (null, null) : (hedef, ad);
    }

    /// <summary>Kova uygulanabilir mi? Liste üreticinin <c>TarihTipiMi</c> kontrolüyle BİREBİR
    /// aynı olmak zorunda: burada geçen ama orada geçmeyen bir tip, kurtarıcının onardığını sanıp
    /// sorgunun yine de düşmesi demektir.</summary>
    private static bool TarihKolonuMu(DataMapColumnDto kolon) =>
        (kolon.VeriTipi ?? "").ToLowerInvariant()
            is "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset";

    private static string KovaAdi(TarihKovasi k) => k switch
    {
        TarihKovasi.Yil => "yıllık",
        TarihKovasi.Ceyrek => "çeyreklik",
        TarihKovasi.Ay => "aylık",
        TarihKovasi.Hafta => "haftalık",
        TarihKovasi.Gun => "günlük",
        _ => k.ToString(),
    };

    private static string OpAdi(KarsilastirmaOp op) => op switch
    {
        KarsilastirmaOp.Esit => "=",
        KarsilastirmaOp.EsitDegil => "≠",
        KarsilastirmaOp.Buyuk => ">",
        KarsilastirmaOp.BuyukEsit => "≥",
        KarsilastirmaOp.Kucuk => "<",
        KarsilastirmaOp.KucukEsit => "≤",
        KarsilastirmaOp.Icinde => "şunlardan biri:",
        KarsilastirmaOp.Arasinda => "arasında:",
        KarsilastirmaOp.Icerir => "içerir:",
        KarsilastirmaOp.Bos => "boş",
        KarsilastirmaOp.DoluDegil => "dolu",
        _ => op.ToString(),
    };
}

/// <summary>Modelin ürettiği ara JSON — istemdeki şemanın birebir karşılığı. Her şey iyimser
/// null'lu: eksik parça reddetmez, kurtarıcı doldurur/doğrular.</summary>
public sealed record NlqTaslak(
    List<NlqTaslakAlan>? Alanlar,
    List<string>? Metrikler,
    List<NlqTaslakFiltre>? Filtreler,
    List<NlqTaslakSiralama>? Siralama,
    int? Limit,
    string? Gorsellestirme,
    string? Hata);

public sealed record NlqTaslakAlan(string? Tablo, string? Kolon, string? Toplama, string? Kova);

public sealed record NlqTaslakFiltre(string? Tablo, string? Kolon, string? Op, List<JsonElement>? Degerler);

public sealed record NlqTaslakSiralama(string? Hedef, bool Azalan);
