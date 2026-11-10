using System.Text;
using Kds.Contracts.DataMaps;
using Kds.Contracts.QueryModel;

namespace Kds.Execution;

/// <summary>Sorgu modelini MSSQL PARAMETRELİ SQL'e çevirir (00-analiz §5). Güvenlik çekirdeği:
/// <list type="bullet">
/// <item>Nesne adları (tablo/kolon/alias) daima <c>QUOTENAME</c> mantığıyla parantezlenir
/// (<c>]</c> → <c>]]</c>), asla birleştirilmez — kimlik enjeksiyonu kapanır.</item>
/// <item>Değerler asla SQL'e gömülmez; hepsi <c>@p0, @p1…</c> parametrelerine gider.</item>
/// <item>Yalnız haritada tanımlı tablo/kolon sorgulanabilir; join yolları haritanın ilişkilerinden.</item>
/// </list>
/// Üretilen SQL yine tek kapıdan (SELECT-only bekçisi) geçer. Deterministiktir → altın-örnek testleri.</summary>
public sealed class MssqlQueryGenerator : IQueryGenerator
{
    public GeneratedQuery Generate(QueryModel model, DataMapDetail dataMap,
        IReadOnlyDictionary<string, object?>? parametreDegerleri = null,
        IReadOnlyList<RowFilter>? satirKurallari = null)
        => Uret(model, dataMap, parametreDegerleri, "p", satirKurallari);

    /// <summary>Y15: birleşim (UNION) dallarında parametre adları çakışmasın diye ÖNEK alır
    /// (@p0 yerine @b1p0). Dış dünyaya kapalıdır — genel giriş <see cref="Generate"/>.</summary>
    private GeneratedQuery Uret(QueryModel model, DataMapDetail dataMap,
        IReadOnlyDictionary<string, object?>? parametreDegerleri, string paramOnek,
        IReadOnlyList<RowFilter>? satirKurallari = null)
    {
        // S22: hesaplanan kolon (ve ondan türeyen katalog metriği) tek başına geçerli bir sorgudur —
        // "yalnız Toplam Ciro" tek KPI değeri üretir; Metrikler ekranındaki Ölç de tam bunu yapar.
        if ((model.Alanlar is null || model.Alanlar.Count == 0)
            && (model.HesaplananAlanlar is null || model.HesaplananAlanlar.Count == 0))
            throw new QueryModelException("En az bir alan ya da hesaplanan kolon/metrik seçilmeli.");
        model = model with { Alanlar = model.Alanlar ?? [] };   // alansız (yalnız formül) model normalize

        // Y15 — BİRLEŞİM (UNION): dallar ayrı ayrı üretilir, sonra alt alta eklenir. Sıralama,
        // limit ve sayfalama SQL gereği yalnız birleşimin TAMAMINA uygulanabilir → ana modelden
        // alınır ve sarmalayıcı katmana yazılır; dalların kendi sıralaması olamaz (açık hata).
        if (model.Birlesim is { Count: > 0 })
            return BirlesimUret(model, dataMap, parametreDegerleri, paramOnek, satirKurallari);

        var tablolar = new Dictionary<Guid, DataMapTableDto>();
        foreach (var t in dataMap.Tablolar) tablolar[t.Id] = t;

        // Y14 — tablo örnekleri (self-join): örnek, haritadaki tablonun sorgu içindeki ikinci
        // kimliğidir. Haritaya SANAL olarak eklenir ve kaynak tablonun TÜM ilişkileri örneğe
        // kopyalanır; böylece modelin geri kalanı (alan/filtre/join/pencere) örneği normal bir
        // tablo gibi kullanır — tek satır bile özel-durum kodu gerekmez.
        if (model.TabloOrnekleri is { Count: > 0 })
        {
            var iliskiler = dataMap.Iliskiler.ToList();
            foreach (var ornek in model.TabloOrnekleri)
            {
                if (!tablolar.TryGetValue(ornek.KaynakTabloId, out var kaynak))
                    throw new QueryModelException("Tablo örneğinin kaynak tablosu haritada yok.");
                if (tablolar.ContainsKey(ornek.OrnekId))
                    throw new QueryModelException("Tablo örneğinin kimliği haritadaki bir tabloyla çakışıyor.");
                var etiket = string.IsNullOrWhiteSpace(ornek.Etiket)
                    ? (kaynak.DostcaAd ?? kaynak.HamAd) + " (2)"
                    : ornek.Etiket;
                tablolar[ornek.OrnekId] = kaynak with { Id = ornek.OrnekId, DostcaAd = etiket };
                foreach (var i in dataMap.Iliskiler)
                {
                    if (i.KaynakTabloId == ornek.KaynakTabloId)
                        iliskiler.Add(i with { KaynakTabloId = ornek.OrnekId });
                    if (i.HedefTabloId == ornek.KaynakTabloId)
                        iliskiler.Add(i with { HedefTabloId = ornek.OrnekId });
                }
            }
            dataMap = dataMap with { Tablolar = [.. tablolar.Values], Iliskiler = iliskiler };
        }

        void KolonDogrula(Guid tabloId, string kolon)
        {
            if (!tablolar.TryGetValue(tabloId, out var t))
                throw new QueryModelException("Seçilen tablo haritada yok.");
            if (!t.Kolonlar.Any(k => k.HamAd == kolon))
                throw new QueryModelException($"Kolon haritada yok: {t.HamAd}.{kolon}");
        }

        // Kullanılan tablolar (alanlar + filtre yaprakları + sıralama + elle gruplama).
        var kullanilan = new HashSet<Guid>();
        foreach (var a in model.Alanlar)
        {
            KolonDogrula(a.TabloId, a.Kolon);
            kullanilan.Add(a.TabloId);
            // Y4: tarih kovası bir BOYUT indirgemesidir — toplamayla birlikte anlamsızdır ve yalnız
            // tarih tipli kolonda çalışır. İkisi de erken, anlaşılır mesajla reddedilir.
            if (a.Kova is null) continue;
            if (a.Toplama != ToplamaTuru.Yok)
                throw new QueryModelException(
                    $"'{a.Kolon}': tarih kırılımı (yıl/ay/…) bir GRUPLAMA kolonudur — üzerine ayrıca toplama (Topla/Say/…) uygulanamaz.");
            var tip = tablolar[a.TabloId].Kolonlar.First(k => k.HamAd == a.Kolon).VeriTipi;
            if (!TarihTipiMi(tip))
                throw new QueryModelException(
                    $"'{a.Kolon}' bir tarih kolonu değil ({tip ?? "tip bilinmiyor"}) — tarih kırılımı yalnız tarih kolonlarında kullanılabilir.");
        }
        WalkFilter(model.Filtre, KolonDogrula, kullanilan);
        // Y2: çıkış adına göre sıralamada tablo/kolon YOKTUR (hesaplanan/özet/CASE kolonu olabilir),
        // doğrulaması SELECT kurulduktan sonra çıkış listesine karşı yapılır.
        foreach (var s in model.Siralama.Where(s => string.IsNullOrWhiteSpace(s.CikisAdi)))
            KolonDogrula(s.TabloId, s.Kolon);
        if (model.Gruplamalar is not null)
            foreach (var g in model.Gruplamalar) { KolonDogrula(g.TabloId, g.Kolon); kullanilan.Add(g.TabloId); }

        // İlişkili özet alanlar: BAĞ tablosu ana sorguda yer almalı → kullanılanlara girer (join
        // yolu BFS'te kurulur). Özetin KENDİ tablosu ana sorguya JOIN'LENMEZ (fan-trap: satır
        // çoğalması SUM/COUNT'ları şişirir) — türetilmiş tabloda kalır; üretim WHERE'den sonra.
        if (model.OzetAlanlar is not null)
            foreach (var oz in model.OzetAlanlar)
            {
                KolonDogrula(oz.TabloId, oz.Kolon);
                if (oz.Toplama == ToplamaTuru.Yok)
                    throw new QueryModelException("Özet alan bir toplama (Say/Topla/…) içermeli.");
                if (!tablolar.ContainsKey(oz.BagTabloId))
                    throw new QueryModelException("Özet alanın bağlanacağı tablo haritada yok.");
                kullanilan.Add(oz.BagTabloId);
            }

        // Koşullu ölçüler: koşul yaprakları + (varsa) toplanan kolonun tablosu sorguya girer.
        if (model.KosulluOlculer is not null)
            foreach (var ko in model.KosulluOlculer)
            {
                if (ko.Toplama == ToplamaTuru.Yok)
                    throw new QueryModelException("Koşullu ölçü bir toplama (Topla/Say/…) içermeli.");
                // O1: sabit 1'in DISTINCT sayımı her zaman ≤1 döner — anlamsız sonuca sessizce izin verme.
                if (ko.Toplama == ToplamaTuru.BenzersizSay && ko.Kolon is null)
                    throw new QueryModelException("Benzersiz say için bir değer kolonu seçin (sabit 1'in benzersiz sayımı anlamsızdır).");
                if (ko.Filtre is null)
                    throw new QueryModelException("Koşullu ölçünün koşulu boş olamaz.");
                if (ko.Kolon is not null)
                {
                    if (ko.TabloId is not Guid koTablo)
                        throw new QueryModelException("Koşullu ölçünün kolonu için tablo gerekli.");
                    KolonDogrula(koTablo, ko.Kolon);
                    kullanilan.Add(koTablo);
                }
                WalkFilter(ko.Filtre, KolonDogrula, kullanilan);
            }

        // Y3 özet koşulları (HAVING): kolon doğrulanır, tablosu sorguya girer (gerekirse join'lenir).
        if (model.OzetKosullar is not null)
            foreach (var ok in model.OzetKosullar)
            {
                if (ok.Toplama == ToplamaTuru.Yok)
                    throw new QueryModelException(
                        "Özet koşulu bir toplama (Say/Topla/…) içermeli — satır bazlı koşullar filtreye yazılır.");
                KolonDogrula(ok.TabloId, ok.Kolon);
                kullanilan.Add(ok.TabloId);
            }

        // M29 CASE kolonları: dal koşulları + sonuç kolonlarının tabloları sorguya girer.
        if (model.CaseAlanlar is not null)
            foreach (var ca in model.CaseAlanlar)
            {
                if (ca.Dallar is null || ca.Dallar.Count == 0)
                    throw new QueryModelException("CASE kolonu en az bir dal (WHEN) içermeli.");
                foreach (var dal in ca.Dallar)
                {
                    if (dal.Filtre is null)
                        throw new QueryModelException("CASE dalının koşulu boş olamaz.");
                    var kolonMu = dal.TabloId is not null && !string.IsNullOrEmpty(dal.Kolon);
                    if (kolonMu == (dal.Sabit is not null))
                        throw new QueryModelException("CASE dalının sonucu ya kolon ya sabit olmalı (tam biri).");
                    if (kolonMu) { KolonDogrula(dal.TabloId!.Value, dal.Kolon!); kullanilan.Add(dal.TabloId.Value); }
                    WalkFilter(dal.Filtre, KolonDogrula, kullanilan);
                }
                var varsKolonMu = ca.VarsayilanTabloId is not null && !string.IsNullOrEmpty(ca.VarsayilanKolon);
                if (varsKolonMu && ca.VarsayilanSabit is not null)
                    throw new QueryModelException("CASE varsayılanı ya kolon ya sabit olmalı (ikisi birden değil).");
                if (varsKolonMu) { KolonDogrula(ca.VarsayilanTabloId!.Value, ca.VarsayilanKolon!); kullanilan.Add(ca.VarsayilanTabloId.Value); }
            }

        // Birleşik kolonlar: kolon parçalarının tabloları sorguya girer (gerekirse otomatik join);
        // sabit metin parçaları aşağıda PARAMETRE olur. En az bir kolon parçası şart.
        if (model.BilesikAlanlar is not null)
            foreach (var b in model.BilesikAlanlar)
            {
                if (b.Parcalar is null || b.Parcalar.Count == 0)
                    throw new QueryModelException("Birleşik kolon en az bir parça içermeli.");
                var kolonVar = false;
                foreach (var p in b.Parcalar)
                {
                    var kolonMu = p.TabloId is not null && !string.IsNullOrEmpty(p.Kolon);
                    if (kolonMu == (p.Sabit is not null))
                        throw new QueryModelException("Birleşik parça ya kolon ya sabit metin olmalı (tam biri).");
                    if (kolonMu) { KolonDogrula(p.TabloId!.Value, p.Kolon!); kullanilan.Add(p.TabloId.Value); kolonVar = true; }
                }
                if (!kolonVar)
                    throw new QueryModelException("Birleşik kolon en az bir KOLON parçası içermeli.");
            }

        // Elle SQL koşulu: {Tablo.Kolon} yer tutucuları doğrulanır, tabloları sorguya girer
        // (otomatik join). Kalan metin OLDUĞU GİBİ WHERE'e eklenir — bu ekran zaten yalnız ham SQL
        // yazabilen rollere açık (SQL Editörü ile aynı güven düzeyi) ve üretilen SQL icrada yine
        // SELECT-only bekçisinden geçer. Kod incelemesi Y3: (a) çözüm tek-tırnak string
        // LİTERALLERİNİ ATLAR ('{X.Y}' metni veri olarak korunur); (b) ad birden çok tabloyla
        // eşleşirse (aynı DostcaAd) sessiz ilk-seçim yerine açıkça reddedilir.
        (Guid tabloId, string kolon) ElleYer(string ic)
        {
            var nokta = ic.LastIndexOf('.');
            if (nokta <= 0 || nokta == ic.Length - 1)
                throw new QueryModelException($"Elle koşulda geçersiz yer tutucu: {{{ic}}} — biçim {{Tablo.Kolon}} olmalı.");
            var tAd = ic[..nokta];
            var kAd = ic[(nokta + 1)..];
            var adaylar = dataMap.Tablolar.Where(t => t.HamAd == tAd || t.DostcaAd == tAd)
                .Select(t => t.Id).Distinct().ToList();
            if (adaylar.Count == 0)
                throw new QueryModelException($"Elle koşuldaki tablo haritada yok: {tAd}");
            if (adaylar.Count > 1)
                throw new QueryModelException($"Elle koşuldaki '{tAd}' birden çok tabloyla eşleşiyor — ham (şemadaki) adı kullanın.");
            KolonDogrula(adaylar[0], kAd);
            return (adaylar[0], kAd);
        }
        if (!string.IsNullOrWhiteSpace(model.ElleKosul))
            ResolveRawCondition(model.ElleKosul, ic => { kullanilan.Add(ElleYer(ic).tabloId); return "{" + ic + "}"; });

        // Y1/Y2 formüller: {Tablo.Kolon} yer tutucuları doğrulanır (tabloları sorguya girer), kalan
        // hesap ifadesi beyaz-liste denetiminden geçer (erken + anlaşılır hata). Y2 çıkış atıfları
        // ([Ad]) bu aşamada henüz bilinmez → denetimde sabite indirilir, gerçek çözümü SELECT
        // kurulduktan sonra yapılır.
        Guid? ilkIfadeTablosu = null;   // S22: alansız (yalnız formül/metrik) modelde FROM kökü buradan seçilir
        string DenetimMetni(string? ifade) =>
            ResolveOutputRefs(
                ResolveRawCondition(ifade ?? "", ic =>
                {
                    var t = ElleYer(ic).tabloId;
                    kullanilan.Add(t);
                    ilkIfadeTablosu ??= t;
                    return "0";
                }),
                _ => "0");
        if (model.HesaplananAlanlar is not null)
            foreach (var h in model.HesaplananAlanlar)
                IfadeDenetimi.Dogrula(DenetimMetni(h.Ifade),
                    $"Hesaplanan kolon{(string.IsNullOrWhiteSpace(h.TakmaAd) ? "" : $" '{h.TakmaAd}'")}");
        if (model.IfadeKosullari is not null)
            foreach (var ik in model.IfadeKosullari)
                IfadeDenetimi.Dogrula(DenetimMetni(ik.Ifade), "Formül koşulu");
        // Y6: pencere kolonunun değer/bölüm/sıra ifadelerindeki {Tablo.Kolon} tabloları da sorguya
        // girer (yoksa join kurulmaz ve "haritada yok" gibi alakasız hata alınırdı).
        if (model.PencereKolonlari is not null)
            foreach (var p in model.PencereKolonlari)
            {
                var nerede = $"Pencere kolonu{(string.IsNullOrWhiteSpace(p.TakmaAd) ? "" : $" '{p.TakmaAd}'")}";
                foreach (var m in new[] { p.Deger }.Concat(p.Bolum ?? []).Concat((p.Sira ?? []).Select(s => s.Ifade)))
                    if (!string.IsNullOrWhiteSpace(m)) IfadeDenetimi.Dogrula(DenetimMetni(m), nerede);
            }
        // Y7: grup içi ilk N'in bölüm/sıra ifadelerindeki tablolar da sorguya girer.
        if (model.GrupIciIlkN is not null)
            foreach (var m in (model.GrupIciIlkN.Bolum ?? []).Concat((model.GrupIciIlkN.Sira ?? []).Select(s => s.Ifade)))
                if (!string.IsNullOrWhiteSpace(m)) IfadeDenetimi.Dogrula(DenetimMetni(m), "Grup içi ilk N");

        // Kullanıcı join seçimleri: aynı tablo ÇİFTİ için o kenarda hangi KOLONLAR kullanılsın.
        // Join YAPISI (hangi tablolar birbirine bağlı) yine haritanın ilişkilerinden (BFS) gelir;
        // kullanıcı yalnız o kenardaki kolonları düzeltir (auto-join yanlış kolonu seçtiyse — aynı
        // çift için başka bir ilişki ya da tümüyle farklı bir kolon çifti). Güvenlik: her iki kolon
        // da haritada TANIMLI olmalı (parantezlenir; değer değil kolon adıdır → enjeksiyon yok).
        // Seçim ille bir DataMapRelationshipDto'ya karşılık gelmek zorunda değil — kolon çifti yeter.
        var joinSecim = new Dictionary<(Guid, Guid), DataMapRelationshipDto>();
        if (model.Joinler is not null)
            foreach (var j in model.Joinler)
            {
                KolonDogrula(j.KaynakTabloId, j.KaynakKolon);
                KolonDogrula(j.HedefTabloId, j.HedefKolon);
                joinSecim[Cift(j.KaynakTabloId, j.HedefTabloId)] =
                    new DataMapRelationshipDto(j.KaynakTabloId, j.KaynakKolon, j.HedefTabloId, j.HedefKolon, FktenMi: false);
            }

        // Join ayarları (Join Yolları adımı): tablo başına ebeveyn + tür + (ops.) elle koşullar.
        var joinAyarlari = new Dictionary<Guid, JoinSetting>();
        if (model.JoinAyarlari is not null)
            foreach (var a in model.JoinAyarlari)
            {
                if (!tablolar.ContainsKey(a.TabloId) || !tablolar.ContainsKey(a.EbeveynTabloId))
                    throw new QueryModelException("Join ayarındaki tablo haritada yok.");
                if (a.TabloId == a.EbeveynTabloId)
                    throw new QueryModelException("Tablo kendisine bağlanamaz (join ayarı).");
                if (a.Kosullar is not null)
                    foreach (var k in a.Kosullar)
                    {
                        KolonDogrula(a.TabloId, k.TabloKolon);
                        // M5: koşul, ana ebeveyn DIŞINDA sorgudaki başka bir tabloya da başvurabilir.
                        var kosulTablo = k.EbeveynTabloId ?? a.EbeveynTabloId;
                        if (k.EbeveynTabloId is Guid kt && !tablolar.ContainsKey(kt))
                            throw new QueryModelException("Join koşulundaki tablo haritada yok.");
                        KolonDogrula(kosulTablo, k.EbeveynKolon);
                        kullanilan.Add(kosulTablo);
                    }
                if (!joinAyarlari.TryAdd(a.TabloId, a))
                    throw new QueryModelException("Bir tablo için birden çok join ayarı verilemez.");
                kullanilan.Add(a.TabloId);
                kullanilan.Add(a.EbeveynTabloId);
            }

        // M17: alansız eklenen tablolar — SELECT'e girmez, yalnız JOIN'e katılır.
        if (model.EkTablolar is not null)
            foreach (var ek in model.EkTablolar)
            {
                if (!tablolar.ContainsKey(ek))
                    throw new QueryModelException("Alansız eklenen tablo haritada yok.");
                kullanilan.Add(ek);
            }

        // FROM kökü: kullanıcı seçtiyse o (Join Yolları adımı); yoksa ilk alanın tablosu.
        var kok = model.KokTabloId is Guid kokIstek
            ? (tablolar.ContainsKey(kokIstek) ? kokIstek
                : throw new QueryModelException("FROM kök tablosu haritada yok."))
            : model.Alanlar.Count > 0 ? model.Alanlar[0].TabloId
                // S22: alansız model (yalnız katalog metriği/formül) — kök, formülün İLK {Tablo.Kolon}
                // atfının tablosudur; ifade metni sabit olduğundan seçim deterministiktir.
                : ilkIfadeTablosu ?? throw new QueryModelException(
                    "Sorgu kökü belirlenemedi — formül en az bir {Tablo.Kolon} atfı içermeli.");
        if (joinAyarlari.ContainsKey(kok))
            throw new QueryModelException("FROM kök tablosu bir ebeveyne bağlanamaz (join ayarını kaldırın ya da kökü değiştirin).");
        kullanilan.Add(kok);
        // Deterministik hedef sırası (M4): kullanıcı Join Yolları'nda SIRA verdiyse (M18) o öncelikli;
        // sonra alanların ekleme sırası — frontend kanvasının `kullanilan` sırasıyla birebir;
        // kalanlar (filtre/özet/ayar tabloları) küme sırasıyla sona.
        var hedefSirasi = (model.TabloSirasi ?? []).Concat(model.Alanlar.Select(a => a.TabloId)).Concat(kullanilan)
            .Where(kullanilan.Contains).Distinct().ToList();
        var (joinSira, joinKenar) = JoinPlani(dataMap, kok, kullanilan, hedefSirasi, joinSecim, joinAyarlari);
        var alias = AssignAlias(joinSira, tablolar);

        /* ARA TABLOLAR (S40): yola giren ama kullanıcının SEÇMEDİĞİ tablolar. Bir tabloya birden
         * çok eşit uzunlukta yol varsa hangisinin seçildiği sorgunun ANLAMINI değiştirir — ölçülen
         * vaka: Markalar'a Araclar.MarkaId yerine Servisler.AnaBayiMarkaId üzerinden bağlanıldı,
         * yani aracın markası yerine servisin bayi markası süzüldü ve hiçbir hata çıkmadı.
         * Yol burada DEĞİŞTİRİLMEZ (doğrusu sorunun anlamına bağlı), yalnız görünür kılınır. */
        var araTablolar = joinSira
            .Where(t => !kullanilan.Contains(t))
            .Select(t => tablolar.TryGetValue(t, out var tb) ? tb.DostcaAd : null)
            .OfType<string>()
            .ToList();

        string KolIfade(Guid tabloId, string kolon) => Q(alias[tabloId]) + "." + Q(kolon);

        // SELECT + çıkış alias'ları (dedup).
        var alinanAlias = new HashSet<string>(StringComparer.Ordinal);
        var secilen = new List<(SelectedField alan, string cikis)>();
        var selectParcalar = new List<string>();
        // Y2: çıkış adı → o kolonun SQL İFADESİ. Sıralama ve ifade koşulları çıkış kolonlarına
        // ([Ad] ile) atıf yapabilsin diye tutulur — SQL, WHERE/HAVING'de alias'ı kabul etmediği
        // için atıflar METİN OLARAK ifadeyle değiştirilir.
        var cikisIfadeleri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void CikisEkle(string cikis, string ifade)
        {
            selectParcalar.Add(ifade + " AS " + Q(cikis));
            cikisIfadeleri[cikis] = ifade;
        }

        // Y1/Y2 — formül metnini SQL'e çevirir: {Tablo.Kolon} → alias'lı kolon; [Çıkış Adı] → o
        // kolonun İFADESİ (SQL, WHERE/HAVING/ORDER BY dışında alias'a atıf kabul etmez; ifadeyi
        // yerine koymak tek taşınabilir yol). Dönüş: (sql, ifade bir TOPLAMA içeriyor mu).
        // Toplama, atıf yapılan çıkış kolonundan da MİRAS alınır ([Ciro] = SUM(...) ise koşul
        // HAVING'e gitmelidir.)
        // cikisAliasla (Y8): DIŞ katman için çözüm — orada sorgu bir alt sorgudur ve çıkış kolonları
        // ADLARIYLA görünür; ifadeyi tekrar yerine koymak yanlış olurdu (pencere işlevi zaten
        // WHERE'de yasaktır). Dış katmanda {Tablo.Kolon} atfı YOKTUR: iç tabloların kolonları dışarı
        // sızmaz — açık mesajla reddedilir.
        (string sql, bool toplamaVar) IfadeCoz(string ham, string nerede, bool cikisAliasla = false)
        {
            var atifToplama = false;
            var atifCozulmus = ResolveOutputRefs(ham, ad =>
            {
                var duz = ad.Trim();
                var eslesen = cikisIfadeleri.Keys.FirstOrDefault(k => k.Equals(duz, StringComparison.OrdinalIgnoreCase));
                if (eslesen is null || !cikisIfadeleri.TryGetValue(duz, out var ifade))
                    throw new QueryModelException(
                        $"{nerede}: '[{duz}]' diye bir çıkış kolonu yok. Kullanılabilir kolonlar: " +
                        (cikisIfadeleri.Count == 0 ? "(henüz yok)" : string.Join(", ", cikisIfadeleri.Keys)));
                if (IfadeDenetimi.ToplamaIceriyorMu(ifade)) atifToplama = true;
                return cikisAliasla ? Q(eslesen) : "(" + ifade + ")";
            });
            var sql = ResolveRawCondition(atifCozulmus, ic =>
            {
                if (cikisAliasla)
                    throw new QueryModelException(
                        $"{nerede}: burada tablo kolonuna ({{{ic}}}) atıf yapılamaz — bu koşul ARA KÜME üzerinde " +
                        "çalışır; o kolonu önce sonuca ekleyip [adıyla] kullanın.");
                var (t, k) = ElleYer(ic);
                return KolIfade(t, k);
            });
            var kendiToplama = IfadeDenetimi.Dogrula(DenetimMetni(ham), nerede);
            return (sql, kendiToplama || atifToplama);
        }
        // Y4: alan ifadesi — kova varsa tarih indirgemesi (boyut), yoksa (varsa) toplama.
        // GROUP BY türetimi de AYNI ifadeyi kullanmak zorunda: tek yerden üretilir.
        string AlanIfade(SelectedField a) => a.Kova is TarihKovasi kv
            ? KovaSql(kv, KolIfade(a.TabloId, a.Kolon))
            : AggregationSql(a.Toplama, KolIfade(a.TabloId, a.Kolon));
        foreach (var a in model.Alanlar)
        {
            // İfade ÖNCE üretilir: aralık-dışı toplama türü burada anlaşılır hatayla reddedilir
            // (takma ad üretimi sözlük araması yapar ve ham KeyNotFound fırlatırdı).
            var ifade = AlanIfade(a);
            var cikis = UniqueAlias(a.TakmaAd is { Length: > 0 } ta ? ta : DefaultAlias(a), alinanAlias);
            secilen.Add((a, cikis));
            CikisEkle(cikis, ifade);
        }

        // WHERE — parametreler bu aşamada (DFS, soldan sağa) üretilir. Değerler kolonun dataMap
        // VeriTipi'ne göre CLR tipine çevrilir (UI metin gönderir; uniqueidentifier/int/date
        // kolonuna ham string bağlanırsa SQL Server icrada patlar — 500 yerine anlaşılır 400).
        var parametreler = new Dictionary<string, object?>();
        var pSayac = 0;
        string YeniParam(object? deger) { var ad = "@" + paramOnek + pSayac++; parametreler[ad] = deger; return ad; }
        // Sabit adlı parametreler (satır limiti · sayfalama · ilk N): varsayılan üretimde ad SADE
        // kalır (@satir) — yalnız birleşim dallarında önek alır (@b2satir) ki dallar çakışmasın.
        string SabitParam(string ad) => paramOnek == "p" ? "@" + ad : "@" + paramOnek + ad;
        string? KolonTipi(Guid tabloId, string kolon) =>
            tablolar[tabloId].Kolonlar.FirstOrDefault(k => k.HamAd == kolon)?.VeriTipi;

        // Parametre-referanslı ({ $param }) filtre değerini çalışma değerine (yoksa parametrenin
        // varsayılanına) çözer; TarihAralik'ta 'parca' ucunu (baslangic/bitis) çıkarır; zorunlu +
        // eksikse anlaşılır hata (→400). Skaler değer olduğu gibi döner → ConvertType kolon tipine çevirir.
        object? CozParametre(object? deger)
        {
            // Y5: parametreye bağlı OLMAYAN değerlerde de dinamik tarih çözülür — filtreye
            // doğrudan "$bugun-6ay" yazılabilsin (kaydedilen rapor zamanla kaysın).
            if (!ParametreRefMi(deger, out var anahtar, out var parca))
                return DinamikTarih.CozVarsa(deger);
            var bildirim = model.Parametreler?.FirstOrDefault(p => p.Anahtar == anahtar)
                ?? throw new QueryModelException($"Tanımsız parametre referansı: {anahtar}");
            var deg = parametreDegerleri is not null && parametreDegerleri.TryGetValue(anahtar, out var v)
                ? v : bildirim.Varsayilan;
            if (bildirim.Tip == ParametreTipi.TarihAralik)
                deg = AralikUcu(deg, parca);
            deg = DecodeJson(deg);
            // M25/Y5: dinamik tarih — "$bugun", "$bugun-6ay", "$ayBasi" … çalıştırma GÜNÜNE göre
            // çözülür (abonelikte "bitiş = bugün"; kayan zaman aralıkları).
            deg = DinamikTarih.CozVarsa(deg);
            if ((deg is null || (deg is string ss && ss.Length == 0)) && bildirim.Zorunlu)
                throw new QueryModelException($"'{bildirim.Etiket}' parametresi zorunlu — bir değer girin.");
            return deg;
        }

        var whereSql = model.Filtre is null ? null : FilterSql(model.Filtre, KolIfade, YeniParam, KolonTipi, CozParametre);

        // Elle SQL koşulu: yer tutucular alias'lı kolon ifadesine çevrilir (literaller atlanır);
        // filtrelerle seçilen bağlaçla (VE / VEYA) birleşir. VEYA'da iki taraf da parantezlenir.
        if (!string.IsNullOrWhiteSpace(model.ElleKosul))
        {
            var elle = ResolveRawCondition(model.ElleKosul.Trim(), ic =>
            {
                var (tid, kol) = ElleYer(ic);
                return KolIfade(tid, kol);
            });
            whereSql = whereSql is null
                ? "(" + elle + ")"
                : model.ElleKosulBaglaci == MantikBaglaci.Veya
                    ? "(" + whereSql + ") OR (" + elle + ")"
                    : whereSql + " AND (" + elle + ")";
        }

        // Y11 — varlık koşulları (EXISTS / NOT EXISTS). Kendi metodunda: WHERE'e katkısı tek bir
        // metin, kurulumu ise korelasyonlu alt sorgu (ayrı bir dünya).
        whereSql = VarlikKosullariniEkle(model, dataMap, tablolar, alias, whereSql,
            KolonDogrula, KolIfade, YeniParam, KolonTipi, CozParametre);

        // RLS — satır-düzeyi güvenlik. Kullanıcının kuralları modelden DEĞİL çağırandan gelir ve
        // buraya, kullanıcı filtrelerinin ÜSTÜNE eklenir. İki incelik:
        // (a) kullanıcı filtresi VEYA içerebilir → tamamı PARANTEZE alınır, yoksa "VEYA 1=1"
        //     benzeri bir kurgu kuralı etkisiz bırakırdı;
        // (b) kuralın tablosu sorguda yoksa kural atlanır — o tablodan zaten satır dönmüyor.
        foreach (var kural in satirKurallari ?? [])
        {
            if (!alias.ContainsKey(kural.TabloId)) continue;
            KolonDogrula(kural.TabloId, kural.Kolon);
            var kuralSql = ConditionSql(
                new FilterCondition(kural.TabloId, kural.Kolon, kural.Op, kural.Degerler),
                KolIfade, YeniParam, KolonTipi, CozParametre);
            whereSql = whereSql is null ? kuralSql : "(" + whereSql + ") AND " + kuralSql;
        }

        // Koşullu ölçüler → AGG(CASE WHEN koşul THEN değer …). Kolon yoksa değer sabit 1 (sayım).
        // Topla ELSE 0 (eşleşmeyen 0 katar); Say/Ortalama/EnAz/EnCok ELSE'siz — eşleşmeyen satır
        // NULL kalır: COUNT saymaz, AVG/MIN/MAX hesaba katmaz (0 yanlış anlam taşırdı).
        if (model.KosulluOlculer is not null)
        {
            var kNo = 0;
            foreach (var ko in model.KosulluOlculer)
            {
                kNo++;
                var kosul = FilterSql(ko.Filtre, KolIfade, YeniParam, KolonTipi, CozParametre)
                    ?? throw new QueryModelException("Koşullu ölçünün koşulu boş olamaz.");
                var deger = ko.Kolon is not null ? KolIfade(ko.TabloId!.Value, ko.Kolon) : "1";
                var caseIfade = ko.Toplama == ToplamaTuru.Topla
                    ? "CASE WHEN " + kosul + " THEN " + deger + " ELSE 0 END"
                    : "CASE WHEN " + kosul + " THEN " + deger + " END";
                var cikis = UniqueAlias(ko.TakmaAd is { Length: > 0 } ta ? ta : "Kosullu" + kNo, alinanAlias);
                CikisEkle(cikis, AggregationSql(ko.Toplama, caseIfade));
            }
        }

        // M29 CASE kolonları → CASE WHEN … THEN … [ELSE …] END; sabit sonuçlar parametre.
        var caseIfadeler = new List<string>(); // aggregate'li sorguda GROUP BY'a da girer (boyut ifadesi)
        if (model.CaseAlanlar is not null)
        {
            var cNo = 0;
            foreach (var ca in model.CaseAlanlar)
            {
                cNo++;
                var sb2 = new StringBuilder("CASE");
                foreach (var dal in ca.Dallar)
                {
                    var kosul = FilterSql(dal.Filtre, KolIfade, YeniParam, KolonTipi, CozParametre)
                        ?? throw new QueryModelException("CASE dalının koşulu boş olamaz.");
                    var sonuc = dal.Sabit is not null ? YeniParam(dal.Sabit) : KolIfade(dal.TabloId!.Value, dal.Kolon!);
                    sb2.Append(" WHEN ").Append(kosul).Append(" THEN ").Append(sonuc);
                }
                if (ca.VarsayilanSabit is not null)
                    sb2.Append(" ELSE ").Append(YeniParam(ca.VarsayilanSabit));
                else if (ca.VarsayilanTabloId is Guid vt && ca.VarsayilanKolon is { Length: > 0 } vk)
                    sb2.Append(" ELSE ").Append(KolIfade(vt, vk));
                sb2.Append(" END");
                var ifade = sb2.ToString();
                var cikis = UniqueAlias(ca.TakmaAd is { Length: > 0 } ta ? ta : "Case" + cNo, alinanAlias);
                CikisEkle(cikis, ifade);
                caseIfadeler.Add(ifade);
            }
        }

        var hesapBoyutIfadeleri = new List<string>(); // Y1: TOPLAMASIZ hesap ifadeleri (GROUP BY'a girer)
        var hesapToplamaVar = false;                  // Y1: en az bir hesap ifadesi TOPLAMA içeriyor mu

        // Birleşik kolonlar → CONCAT (NULL parça '+' gibi tümünü NULL'a düşürmez); sabitler parametre.
        var bilesikIfadeler = new List<string>(); // aggregate'li sorguda GROUP BY'a da girer
        if (model.BilesikAlanlar is not null)
        {
            var bNo = 0;
            foreach (var b in model.BilesikAlanlar)
            {
                bNo++;
                var parcalar = b.Parcalar
                    .Select(p => p.Sabit is not null ? YeniParam(p.Sabit) : KolIfade(p.TabloId!.Value, p.Kolon!))
                    .ToList();
                var ifade = parcalar.Count == 1 ? parcalar[0] : "CONCAT(" + string.Join(", ", parcalar) + ")";
                var cikis = UniqueAlias(b.TakmaAd is { Length: > 0 } ta ? ta : "Bilesik" + bNo, alinanAlias);
                CikisEkle(cikis, ifade);
                bilesikIfadeler.Add(ifade);
            }
        }

        // İlişkili özet alanlar → türetilmiş-tablo LEFT JOIN'leri. Her özet kendi filtresiyle
        // ÖNCE bağ kolonuna göre gruplanır, SONRA ana sorguya bağlanır — ana satırları çoğaltmaz
        // (scalar subquery'nin set-tabanlı eşdeğeri). Değerler yine parametre, adlar yine Q().
        // BİRLEŞTİRME (kullanıcı isteği, 15 Ağu 2026): aynı tabloya + aynı bağ kolonuna + aynı
        // filtreye düşen özet alanlar TEK türetilmiş tabloda toplanır (deger1..degerN). Böylece
        // "SUM + COUNT" gibi iki ölçü kaynağı İKİ KEZ taratmaz ve üretilen SQL elle yazılanla aynı
        // şekle gelir. Tek ölçülü grupta kolon adı yine [deger] (SQL okunur kalsın).
        var ozetJoinler = new List<string>();
        var ozetIfadeler = new List<string>(); // aggregate'li sorguda GROUP BY'a da girer
        if (model.OzetAlanlar is not null)
        {
            // Gruplama filtre SQL'i ÜRETİLMEDEN yapılır: aynı filtre her üretimde farklı parametre
            // adı alacağı için (@p0/@p1) metin karşılaştırması yanıltır — yapısal imza kullanılır.
            // Üyeler İNDEKSLE tutulur: SummaryField bir record'dur (değer eşitliği) — iki özdeş özet
            // alan sözlükte tek anahtara düşer ve eşleme bozulurdu.
            var ozetGruplari = new List<(Guid TabloId, string OzKol, Guid BagTabloId, string BagKol,
                FilterNode? Filtre, string Imza, List<int> Uyeler)>();

            for (var oi = 0; oi < model.OzetAlanlar.Count; oi++)
            {
                var oz = model.OzetAlanlar[oi];
                // Bağ kolonları: kullanıcının join seçimi varsa o; yoksa haritadaki ilk ilişki.
                var cift = Cift(oz.TabloId, oz.BagTabloId);
                var il = joinSecim.TryGetValue(cift, out var secili) ? secili
                    : dataMap.Iliskiler.FirstOrDefault(i => Cift(i.KaynakTabloId, i.HedefTabloId) == cift)
                    ?? throw new QueryModelException(
                        $"Özet tablosu ({tablolar[oz.TabloId].HamAd}) ile bağlanacağı tablo ({tablolar[oz.BagTabloId].HamAd}) arasında haritada ilişki yok.");
                var (ozKol, _, bagKol) = KenarUclari(il, oz.TabloId);

                // Özet filtresi YALNIZ özet tablosunun kolonlarını kullanabilir (türetilmiş tablonun
                // içinden ana sorgu alias'ları görünmez; kapsam sızıntısı da böylece kapanır).
                WalkFilter(oz.Filtre, (tid, kol) =>
                {
                    if (tid != oz.TabloId)
                        throw new QueryModelException("Özet filtresi yalnız özet tablosunun kolonlarını kullanabilir.");
                    KolonDogrula(tid, kol);
                }, []);

                var imza = string.Join('|', oz.TabloId, ozKol, oz.BagTabloId, bagKol, FiltreImzasi(oz.Filtre));
                var grup = ozetGruplari.FindIndex(g => g.Imza == imza);
                if (grup < 0)
                    ozetGruplari.Add((oz.TabloId, ozKol, oz.BagTabloId, bagKol, oz.Filtre, imza, [oi]));
                else
                    ozetGruplari[grup].Uyeler.Add(oi);
            }

            // Her özet alanın hangi türetilmiş tablonun hangi kolonundan okunacağı (model sırasına göre).
            var ozetKaynak = new (string Alias, string Kolon)[model.OzetAlanlar.Count];
            var ozNo = 0;
            foreach (var g in ozetGruplari)
            {
                var ozAlias = "oz" + ++ozNo; // AssignAlias tek harf + sayaç üretir → "ozN" çakışmaz
                var icWhere = g.Filtre is null ? null
                    : FilterSql(g.Filtre, (_, kol) => Q(kol), YeniParam, KolonTipi, CozParametre);

                var degerler = new List<string>();
                for (var i = 0; i < g.Uyeler.Count; i++)
                {
                    var uye = model.OzetAlanlar[g.Uyeler[i]];
                    var kolonAdi = g.Uyeler.Count == 1 ? "deger" : "deger" + (i + 1);
                    degerler.Add(AggregationSql(uye.Toplama, Q(uye.Kolon)) + " AS " + Q(kolonAdi));
                    ozetKaynak[g.Uyeler[i]] = (ozAlias, kolonAdi);
                }

                ozetJoinler.Add(
                    "LEFT JOIN (SELECT " + Q(g.OzKol) + " AS [bag], " + string.Join(", ", degerler) +
                    " FROM " + TableSql(tablolar[g.TabloId]) +
                    (icWhere is not null ? " WHERE " + icWhere : "") +
                    " GROUP BY " + Q(g.OzKol) + ") AS " + Q(ozAlias) +
                    " ON " + Q(ozAlias) + ".[bag] = " + KolIfade(g.BagTabloId, g.BagKol));
            }

            // SELECT parçaları MODEL SIRASINDA eklenir (gruplama kolon sırasını değiştirmesin).
            for (var oi = 0; oi < model.OzetAlanlar.Count; oi++)
            {
                var oz = model.OzetAlanlar[oi];
                var (ozAlias, kolonAdi) = ozetKaynak[oi];
                // Eşleşmesi olmayan ana satırda COUNT/SUM 0 okunmalı (LEFT JOIN NULL bırakır);
                // AVG/MIN/MAX'ta NULL dürüst olandır (0 yanlış anlam taşır).
                var ifade = oz.Toplama is ToplamaTuru.Say or ToplamaTuru.BenzersizSay or ToplamaTuru.Topla
                    ? "ISNULL(" + Q(ozAlias) + "." + Q(kolonAdi) + ", 0)"
                    : Q(ozAlias) + "." + Q(kolonAdi);
                var cikis = UniqueAlias(
                    oz.TakmaAd is { Length: > 0 } ta ? ta : ToplamaOnek[oz.Toplama] + oz.Kolon, alinanAlias);
                CikisEkle(cikis, ifade);
                ozetIfadeler.Add(ifade);
            }
        }

        // Y6 pencere kolonları — özetlerden SONRA (özet kolonlara [Ad] ile atıf yapabilsinler),
        // hesaplanan kolonlardan ÖNCE (hesap, pencere sonucuna atıf yapabilsin: "bölüm toplamına
        // oran"). Pencere işlevi GROUP BY'dan SONRA çalışır → asla GROUP BY'a eklenmez.
        if (model.PencereKolonlari is not null)
        {
            var pNo = 0;
            foreach (var p in model.PencereKolonlari)
            {
                pNo++;
                var etiket = $"Pencere kolonu{(string.IsNullOrWhiteSpace(p.TakmaAd) ? "" : $" '{p.TakmaAd}'")}";
                var cikis = UniqueAlias(p.TakmaAd is { Length: > 0 } ta ? ta : "Pencere" + pNo, alinanAlias);
                CikisEkle(cikis, PencereSql(p, etiket, ifade => IfadeCoz(ifade, etiket).sql));
            }
        }

        // Y1 hesaplanan kolonlar — EN SONDA üretilir ki formül DİĞER TÜM çıkış kolonlarına
        // ([Ad] ile) atıf yapabilsin (alan, koşullu ölçü, CASE, birleşik, özet). TOPLAMA içeren
        // ifade grup düzeyidir (GROUP BY'a GİRMEZ); içermeyen ifade satır düzeyi bir boyuttur →
        // aggregate'li sorguda GROUP BY'a eklenir (bileşik/CASE ile aynı kural).
        if (model.HesaplananAlanlar is not null)
        {
            var hNo = 0;
            foreach (var h in model.HesaplananAlanlar)
            {
                hNo++;
                var etiket = $"Hesaplanan kolon{(string.IsNullOrWhiteSpace(h.TakmaAd) ? "" : $" '{h.TakmaAd}'")}";
                var (sql, toplamaVar) = IfadeCoz(h.Ifade ?? "", etiket);
                var cikis = UniqueAlias(h.TakmaAd is { Length: > 0 } ta ? ta : "Hesap" + hNo, alinanAlias);
                CikisEkle(cikis, "(" + sql + ")");
                if (toplamaVar) hesapToplamaVar = true;
                else hesapBoyutIfadeleri.Add("(" + sql + ")");
            }
        }

        // GROUP BY — model elle liste taşıyorsa TAM o kullanılır; yoksa (otomatik) herhangi bir
        // aggregate varsa aggregate-olmayan seçili alanlardan türetilir.
        var aggVar = model.Alanlar.Any(a => a.Toplama != ToplamaTuru.Yok)
            || model.KosulluOlculer is { Count: > 0 } // koşullu ölçü de aggregate'tir
            // Y1: toplama İÇEREN hesaplanan kolon da sorguyu aggregate yapar (SUM(x)/COUNT(y) gibi).
            // Bayrak ÇÖZÜLMÜŞ ifadeden gelir: [Ciro] gibi bir çıkış atfı da toplama taşıyabilir.
            || hesapToplamaVar;
        // Y4: gruplama ifadesi SELECT'tekiyle BİREBİR aynı olmalı — kovalı kolon GROUP BY'a
        // indirgenmiş hâliyle girer (ham kolon girerse SQL "seçili değil" der ve aylık kırılım bozulur).
        var gruplar = model.Gruplamalar is not null
            ? model.Gruplamalar
                .Select(g => g.Kova is TarihKovasi gk ? KovaSql(gk, KolIfade(g.TabloId, g.Kolon)) : KolIfade(g.TabloId, g.Kolon))
                .Distinct().ToList()
            : aggVar
                ? model.Alanlar.Where(a => a.Toplama == ToplamaTuru.Yok)
                    .Select(AlanIfade).Distinct().ToList() // aynı boyut iki kez → tek GROUP BY
                : [];
        // Ana sorgu gruplanıyorsa birleşik + özet + CASE ifadeleri SQL gereği GROUP BY'a girmek zorunda
        // (birleşik/CASE = boyut ifadesi; türetilmiş özet bağ-kolon başına tek satır → sonucu değiştirmez).
        if ((bilesikIfadeler.Count > 0 || ozetIfadeler.Count > 0 || caseIfadeler.Count > 0 || hesapBoyutIfadeleri.Count > 0)
            && (aggVar || gruplar.Count > 0))
        {
            gruplar.AddRange(bilesikIfadeler);
            gruplar.AddRange(ozetIfadeler);
            gruplar.AddRange(caseIfadeler);
            gruplar.AddRange(hesapBoyutIfadeleri); // Y1: yalnız TOPLAMASIZ hesap ifadeleri (boyut)
        }

        // Y10 — ara toplam etiketi için GROUPING() bayrakları (dış katman kullanacak).
        var toplamBayraklari = new Dictionary<string, string>(StringComparer.Ordinal);
        if (model.AraToplam is SubtotalSpec araToplam)
        {
            if (gruplar.Count == 0)
                throw new QueryModelException(
                    "Ara toplam için en az bir kırılım gerekir — önce bir boyut kolonu seçin (ya da gruplamayı elle tanımlayın).");
            if (model.Capraz is not null)
                throw new QueryModelException("Ara toplam satırları çapraz tabloyla birlikte kullanılamaz.");
            if (araToplam.Etiketle)
                foreach (var g in gruplar)
                {
                    var bayrak = UniqueAlias("_araToplam" + (toplamBayraklari.Count + 1), alinanAlias);
                    selectParcalar.Add("GROUPING(" + g + ") AS " + Q(bayrak));
                    toplamBayraklari[g] = bayrak;
                }
        }

        // ORDER BY — çıkış kolonları üzerinden (alias'la). ORDER BY, SELECT'ten SONRA çalıştığı için
        // alias'a atıf yapabilir; Y2 ile yalnız seçili ALANLAR değil HER çıkış kolonu (hesaplanan,
        // özet, koşullu ölçü, CASE, birleşik) sıralanabilir.
        var siraParcalar = new List<string>();
        foreach (var s in model.Siralama)
        {
            string cikisAdi;
            if (!string.IsNullOrWhiteSpace(s.CikisAdi))
            {
                var duz = s.CikisAdi.Trim();
                var eslesen = cikisIfadeleri.Keys.FirstOrDefault(k => k.Equals(duz, StringComparison.OrdinalIgnoreCase));
                if (eslesen is null)
                    throw new QueryModelException(
                        $"Sıralanacak '{duz}' kolonu çıkışta yok. Kullanılabilir kolonlar: " +
                        (cikisIfadeleri.Count == 0 ? "(yok)" : string.Join(", ", cikisIfadeleri.Keys)));
                cikisAdi = eslesen;
            }
            else
            {
                // Y4: aynı kolonun farklı kovaları AYRI çıkış kolonlarıdır — eşleşmeye kova da girer.
                var esles = secilen.FirstOrDefault(x =>
                    x.alan.TabloId == s.TabloId && x.alan.Kolon == s.Kolon && x.alan.Toplama == s.Toplama
                    && x.alan.Kova == s.Kova);
                if (esles.cikis is null)
                    throw new QueryModelException("Sıralama yalnız seçili bir alan üzerinde yapılabilir.");
                cikisAdi = esles.cikis;
            }
            siraParcalar.Add(Q(cikisAdi) + (s.Azalan ? " DESC" : " ASC"));
        }

        // Y3 — HAVING: özet koşulları soldan sağa bağlaçlarla birleşir (filtre satırlarıyla aynı
        // kural). Sol taraf AGG(kolon) olduğundan ConditionSql'e "kolon ifadesi" olarak toplama
        // sarmalanmış hâli verilir — op/değer/parametre makinesi aynen paylaşılır (tek doğruluk).
        // Say/BenzersizSay'da karşılaştırma değeri SAYIdır: kolonun kendi tipine değil bigint'e çevrilir
        // (ör. COUNT(DISTINCT Ad) >= 3'te değer nvarchar'a çevrilmemeli).
        string? havingSql = null;
        if (model.OzetKosullar is { Count: > 0 })
        {
            foreach (var ok in model.OzetKosullar)
            {
                var parca = ConditionSql(
                    new FilterCondition(ok.TabloId, ok.Kolon, ok.Op, ok.Degerler),
                    (t, kol) => AggregationSql(ok.Toplama, KolIfade(t, kol)),
                    YeniParam,
                    (t, kol) => ok.Toplama is ToplamaTuru.Say or ToplamaTuru.BenzersizSay ? "bigint" : KolonTipi(t, kol),
                    CozParametre);
                havingSql = havingSql is null
                    ? parca
                    : ok.Baglac == MantikBaglaci.Veya ? "(" + havingSql + ") OR " + parca : havingSql + " AND " + parca;
            }
        }

        // Y2 — formül koşulları: sol taraf bir HESAP ya da [Çıkış Kolonu] atfı. NEREYE gideceğini
        // üretici seçer: ifade toplama İÇERİYORSA gruplama sonrası (HAVING), içermiyorsa satır
        // bazında (WHERE). Sağ taraf her filtre gibi ConditionSql'den geçer (parametre + dinamik
        // tarih aynı makine). Tip: ifadenin tipi bilinemez → değer kendi tipiyle parametrelenir
        // (dürüst olan; kolon tipine zorlamak yanlış CAST üretirdi).
        var disKosullar = new List<string>(); // Y8: ARA KÜME üzerinde (dış katmanda) uygulanacaklar
        if (model.IfadeKosullari is not null)
        {
            foreach (var ik in model.IfadeKosullari)
            {
                var (ifadeSql, toplamaVar) = IfadeCoz(ik.Ifade ?? "", "Formül koşulu");
                // Y8: pencere işlevi SQL'de ne WHERE'de ne HAVING'de kullanılabilir. Böyle bir koşul
                // (ör. "[Ciro] > [OrtalamaCiro]") sorguyu ARA KÜMEYE çevirip DIŞ katmanda uygulanır —
                // kullanıcı bu ayrımı bilmek zorunda değil.
                if (ifadeSql.Contains("OVER (", StringComparison.Ordinal))
                {
                    var disIfade = IfadeCoz(ik.Ifade ?? "", "Formül koşulu", cikisAliasla: true).sql;
                    var disParca = ConditionSql(
                        new FilterCondition(Guid.Empty, "formül", ik.Op, ik.Degerler),
                        (_, _) => "(" + disIfade + ")", YeniParam, (_, _) => null, CozParametre);
                    disKosullar.Add(disKosullar.Count > 0 && ik.Baglac == MantikBaglaci.Veya
                        ? "OR " + disParca : (disKosullar.Count > 0 ? "AND " + disParca : disParca));
                    continue;
                }
                var parca = ConditionSql(
                    new FilterCondition(Guid.Empty, "formül", ik.Op, ik.Degerler),
                    (_, _) => "(" + ifadeSql + ")",
                    YeniParam,
                    (_, _) => null,
                    CozParametre);
                if (toplamaVar)
                    havingSql = havingSql is null
                        ? parca
                        : ik.Baglac == MantikBaglaci.Veya ? "(" + havingSql + ") OR " + parca : havingSql + " AND " + parca;
                else
                    whereSql = whereSql is null
                        ? parca
                        : ik.Baglac == MantikBaglaci.Veya ? "(" + whereSql + ") OR " + parca : whereSql + " AND " + parca;
            }
        }

        // Limit → TOP (@satir) (parametreli). Çalıştırıcı ayrıca kendi sert satır tavanını uygular.
        // Y16: sayfalama istendiyse TOP YERİNE OFFSET/FETCH kullanılır — ikisi aynı sorguda
        // birlikte kullanılamaz; bu durumda satır limiti SAYFA BOYU anlamına gelir.
        string? topOnek = null;
        string? sayfaSonEk = null;
        if (model.SatirLimiti is int lim && lim <= 0)
            throw new QueryModelException("Satır limiti pozitif olmalı.");
        if (model.Atla is int atla)
        {
            if (atla < 0) throw new QueryModelException("Sayfalamada atlanacak satır sayısı negatif olamaz.");
            var atlaAd = SabitParam("atla"); parametreler[atlaAd] = atla;
            sayfaSonEk = "\nOFFSET (" + atlaAd + ") ROWS";
            if (model.SatirLimiti is int sayfa)
            {
                var sayfaAd = SabitParam("satir"); parametreler[sayfaAd] = sayfa;
                sayfaSonEk += " FETCH NEXT (" + sayfaAd + ") ROWS ONLY";
            }
        }
        else if (model.SatirLimiti is int lim2)
        {
            var satirAd = SabitParam("satir"); parametreler[satirAd] = lim2;
            topOnek = "TOP (" + satirAd + ") ";
        }

        // Y7 — grup içi ilk N: sıra numarası bir PENCERE işleviyle hesaplanır; pencereye WHERE'de
        // atıf yapılamadığı için sorgu bir alt sorguyla SARMALANIR. Sarmalama varsa TOP ve ORDER BY
        // DIŞ katmana taşınır: iç katmanda TOP, bölüm kırpmasından ÖNCE satır keserdi (yanlış
        // sonuç) ve alt sorguda TOP'suz ORDER BY zaten geçersizdir.
        string? ilkNKolon = null;
        if (model.GrupIciIlkN is TopNPerGroup ilkN)
        {
            if (ilkN.Adet <= 0)
                throw new QueryModelException("Grup içi ilk N: kaç satır kalacağı 0'dan büyük olmalı.");
            var ilkNSira = (ilkN.Sira ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Ifade))
                .Select(s => IfadeCoz(s.Ifade, "Grup içi ilk N").sql + (s.Azalan ? " DESC" : " ASC")).ToList();
            if (ilkNSira.Count == 0)
                throw new QueryModelException(
                    "Grup içi ilk N: bir SIRA seçin — 'ilk' sözcüğü neye göre olduğu söylenmeden anlam taşımaz (ör. ciroya göre azalan).");
            var ilkNBolum = (ilkN.Bolum ?? []).Where(b => !string.IsNullOrWhiteSpace(b))
                .Select(b => IfadeCoz(b, "Grup içi ilk N").sql).ToList();
            // Beraberlikte: RANK hepsini bırakır (N'den çok satır dönebilir), ROW_NUMBER tam N verir.
            ilkNKolon = UniqueAlias("_ilkNSira", alinanAlias);
            selectParcalar.Add(
                (ilkN.EsitleriDeAl ? "RANK()" : "ROW_NUMBER()") + " OVER ("
                + (ilkNBolum.Count > 0 ? "PARTITION BY " + string.Join(", ", ilkNBolum) + " " : "")
                + "ORDER BY " + string.Join(", ", ilkNSira) + ") AS " + Q(ilkNKolon));
        }

        // Sarmalama (Y7/Y8) varsa TOP ve ORDER BY DIŞ katmana taşınır: iç katmanda TOP, kırpma/
        // filtreden ÖNCE satır keserdi ve alt sorguda TOP'suz ORDER BY zaten geçersizdir.
        var sarmala = ilkNKolon is not null || disKosullar.Count > 0 || model.Capraz is not null
            || toplamBayraklari.Count > 0
            || model.AraKumeOzeti is { Count: > 0 } || (model.AraKumeGruplari?.Any(g => !string.IsNullOrWhiteSpace(g)) ?? false);

        var sb = new StringBuilder();
        // Y12: DISTINCT sarmalama olsa bile İÇ katmandadır — tekilleştirme kaynak satırlar üzerinde
        // yapılır; dış katman zaten iç sonucun üstünde çalışır.
        sb.Append("SELECT ").Append(model.Tekille ? "DISTINCT " : null)
          .Append(sarmala ? null : topOnek).Append(string.Join(", ", selectParcalar));
        sb.Append("\nFROM ").Append(TableSql(tablolar[kok])).Append(" AS ").Append(Q(alias[kok]));
        foreach (var kp in joinKenar)
        {
            var onParcalar = kp.Kosullar
                .Select(k => KolIfade(kp.Cocuk, k.CocukKol) + " = " + KolIfade(k.AnaTablo, k.AnaKol)).ToList();
            // Y13: ON'a eklenen SABİT koşullar. LEFT join'de bu, WHERE'e yazmaktan FARKLIDIR:
            // eşleşmeyi daraltır ama ana satırı elemez (kolonlar NULL gelir).
            if (joinAyarlari.TryGetValue(kp.Cocuk, out var kpAyar) && kpAyar.EkKosullar is { Count: > 0 })
                foreach (var ek in kpAyar.EkKosullar)
                {
                    KolonDogrula(ek.TabloId, ek.Kolon);
                    if (!alias.ContainsKey(ek.TabloId))
                        throw new QueryModelException(
                            $"Join koşulundaki '{tablolar[ek.TabloId].DostcaAd ?? tablolar[ek.TabloId].HamAd}' tablosu sorguda yok.");
                    onParcalar.Add(ConditionSql(ek, KolIfade, YeniParam, KolonTipi, CozParametre));
                }
            sb.Append(kp.Tur == JoinType.Left ? "\nLEFT JOIN " : "\nJOIN ")
              .Append(TableSql(tablolar[kp.Cocuk])).Append(" AS ").Append(Q(alias[kp.Cocuk]))
              .Append(" ON ").Append(string.Join(" AND ", onParcalar));
        }
        foreach (var oj in ozetJoinler) sb.Append('\n').Append(oj);
        if (whereSql is not null) sb.Append("\nWHERE ").Append(whereSql);
        // Y10 — ara toplam: GROUP BY yerine GROUPING SETS/ROLLUP. Özet satırlarında kırılım hücresi
        // NULL gelir; GERÇEK boş değerden ayırt edebilmek için her kırılıma bir GROUPING() bayrağı
        // eklenir (dış katman etiketi bu bayrakla yazar).
        if (gruplar.Count > 0 && model.AraToplam is SubtotalSpec at)
        {
            sb.Append("\nGROUP BY ").Append(at.Tur == AraToplamTuru.Kademeli
                ? "ROLLUP(" + string.Join(", ", gruplar) + ")"
                : "GROUPING SETS ((" + string.Join(", ", gruplar) + "), ())");
        }
        else if (gruplar.Count > 0) sb.Append("\nGROUP BY ").Append(string.Join(", ", gruplar));
        if (havingSql is not null) sb.Append("\nHAVING ").Append(havingSql);
        if (!sarmala && siraParcalar.Count > 0)
            sb.Append("\nORDER BY ").Append(string.Join(", ", siraParcalar));
        // Y16: OFFSET/FETCH yalnız ORDER BY'lı sorguda geçerlidir — "kaçıncı sayfa" sorusu
        // sıralama olmadan anlamsızdır (SQL Server da reddeder).
        if (!sarmala && sayfaSonEk is not null)
        {
            if (siraParcalar.Count == 0)
                throw new QueryModelException(
                    "Sayfalama için bir SIRALAMA gerekir — sıra verilmezse 'kaçıncı sayfa' sorusunun sabit bir cevabı olmaz.");
            sb.Append(sayfaSonEk);
        }

        // İç sorgu bitti. Sarmalama gerekmiyorsa sonuç budur; gerekiyorsa DIŞ KATMAN ayrı bir
        // adımdır (Y7 grup içi ilk N · Y8 ara küme · Y9 çapraz · Y10 ara toplam etiketi) ve
        // kendi metodunda yaşar — iç sorgu kurulumuyla karışmasın.
        if (!sarmala) return new GeneratedQuery(sb.ToString(), parametreler, araTablolar);
        return DisKatmanUret(new DisKatmanGirdi(
            model, sb, parametreler, cikisIfadeleri, secilen, topOnek, sayfaSonEk,
            ilkNKolon, disKosullar, toplamBayraklari, YeniParam, SabitParam, araTablolar));
    }

    /// <summary>Y11 — "eşleşen kaydı olanlar/olmayanlar" koşullarını WHERE'e ekler.
    /// Join ile yapılsaydı çoklu eşleşme ana satırı ÇOĞALTIR (toplamlar şişer) ve "hiç yok" sorusu
    /// sorulamazdı: aranan tablo ana sorguya JOIN'LENMEZ, korelasyonlu alt sorguda kalır.
    /// Bağ kolonları verilmemişse haritadaki ilişkiden türetilir; bağlanılan tablo sorguda değilse
    /// ne yapılacağını söyleyen hata verilir.</summary>
    private static string? VarlikKosullariniEkle(
        QueryModel model, DataMapDetail dataMap, Dictionary<Guid, DataMapTableDto> tablolar,
        Dictionary<Guid, string> alias, string? whereSql,
        Action<Guid, string> kolonDogrula, Func<Guid, string, string> kolIfade,
        Func<object?, string> yeniParam, Func<Guid, string, string?> kolonTipi,
        Func<object?, object?> cozParam)
    {
        if (model.VarlikKosullari is null) return whereSql;
        var vNo = 0;
        foreach (var vk in model.VarlikKosullari)
        {
            vNo++;
            if (!tablolar.TryGetValue(vk.TabloId, out var vTablo))
                throw new QueryModelException("Varlık koşulundaki tablo haritada yok.");
            var vAlias = "_v" + vNo;
            var baglar = (vk.Baglar ?? []).ToList();
            if (baglar.Count == 0)
            {
                var iliski = dataMap.Iliskiler.FirstOrDefault(i =>
                    (i.KaynakTabloId == vk.TabloId && alias.ContainsKey(i.HedefTabloId))
                    || (i.HedefTabloId == vk.TabloId && alias.ContainsKey(i.KaynakTabloId)))
                    ?? throw new QueryModelException(
                        $"'{vTablo.DostcaAd ?? vTablo.HamAd}' tablosunun sorgudaki tablolarla ilişkisi haritada yok — " +
                        "Veri Haritası'ndan ilişki ekleyin ya da bağ kolonlarını elle seçin.");
                baglar.Add(iliski.KaynakTabloId == vk.TabloId
                    ? new ExistenceLink(iliski.KaynakKolon, iliski.HedefTabloId, iliski.HedefKolon)
                    : new ExistenceLink(iliski.HedefKolon, iliski.KaynakTabloId, iliski.KaynakKolon));
            }
            var esKosullar = new List<string>();
            foreach (var b in baglar)
            {
                kolonDogrula(vk.TabloId, b.Kolon);
                kolonDogrula(b.AnaTabloId, b.AnaKolon);
                if (!alias.ContainsKey(b.AnaTabloId))
                    throw new QueryModelException(
                        $"Varlık koşulu '{tablolar[b.AnaTabloId].DostcaAd ?? tablolar[b.AnaTabloId].HamAd}' tablosuna bağlanıyor " +
                        "ama o tablo sorguda yok — önce tabloyu sorguya ekleyin.");
                esKosullar.Add(Q(vAlias) + "." + Q(b.Kolon) + " = " + kolIfade(b.AnaTabloId, b.AnaKolon));
            }
            // Alt sorgu filtresi: aranan tablonun kolonları yeni alias'a, diğerleri ana sorgunun
            // alias'ına çözülür (korelasyon serbest).
            if (vk.Filtre is not null)
            {
                var ic = FilterSql(vk.Filtre,
                    (t, k) => t == vk.TabloId ? Q(vAlias) + "." + Q(k) : kolIfade(t, k),
                    yeniParam, kolonTipi, cozParam);
                if (ic is not null) esKosullar.Add("(" + ic + ")");
            }
            var esSql = (vk.Yok ? "NOT EXISTS (SELECT 1 FROM " : "EXISTS (SELECT 1 FROM ")
                + TableSql(vTablo) + " AS " + Q(vAlias)
                + " WHERE " + string.Join(" AND ", esKosullar) + ")";
            whereSql = whereSql is null
                ? esSql
                : vk.Baglac == MantikBaglaci.Veya ? "(" + whereSql + ") OR " + esSql : whereSql + " AND " + esSql;
        }
        return whereSql;
    }

    /// <summary>Dış (sarmalayıcı) katmanın girdisi — iç sorgu kurulduktan sonra elde kalan her şey.
    /// Metot imzasını 12 parametreye şişirmemek için tek nesnede toplanır.</summary>
    private sealed record DisKatmanGirdi(
        QueryModel Model,
        StringBuilder IcSorgu,
        Dictionary<string, object?> Parametreler,
        /// <summary>Çıkış adı → o kolonun İÇ ifadesi (ekleme sırasını korur).</summary>
        Dictionary<string, string> CikisIfadeleri,
        IReadOnlyList<(SelectedField alan, string cikis)> Secilen,
        string? TopOnek,
        string? SayfaSonEk,
        /// <summary>Y7: iç sorguya eklenen yardımcı sıra kolonu (yoksa null).</summary>
        string? IlkNKolon,
        /// <summary>Y8: dış katmanda uygulanacak koşullar (pencereye atıf yapanlar).</summary>
        IReadOnlyList<string> DisKosullar,
        /// <summary>Y10: grup ifadesi → GROUPING() bayrak kolonu.</summary>
        IReadOnlyDictionary<string, string> ToplamBayraklari,
        Func<object?, string> YeniParam,
        Func<string, string> SabitParam,
        /// <summary>S40: kullanıcının seçmediği hâlde yola giren tablolar — sarmalanan sorguda da
        /// AYNI join planı kullanıldığı için dış katmana olduğu gibi taşınır.</summary>
        IReadOnlyList<string> AraTablolar);

    /// <summary>Y7–Y10 — DIŞ KATMAN. Sorgu bir alt sorguya sarıldığında sonucu burada kurulur:
    /// çapraz tablo (PIVOT) · ara küme özeti · kırpma/filtre + ara toplam etiketi. Ortak nokta:
    /// bu katmanda iç tabloların kolonları GÖRÜNMEZ, yalnız çıkış ADLARI vardır.</summary>
    private static GeneratedQuery DisKatmanUret(DisKatmanGirdi girdi)
    {
        var (model, sb, parametreler, cikisIfadeleri, secilen, topOnek, sayfaSonEk,
             ilkNKolon, disKosullar, toplamBayraklari, YeniParam, SabitParam, araTablolar) = girdi;

        var disOzet = model.AraKumeOzeti is { Count: > 0 };
        var disGruplar = (model.AraKumeGruplari ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        // Dış katmanda çıkış kolonları ADLARIYLA görünür; ad doğrulaması burada yapılır.
        string DisAd(string ad, string nerede)
        {
            var eslesen = cikisIfadeleri.Keys.FirstOrDefault(k => k.Equals(ad.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new QueryModelException(
                    $"{nerede}: '{ad}' diye bir çıkış kolonu yok. Kullanılabilir kolonlar: " +
                    string.Join(", ", cikisIfadeleri.Keys));
            return eslesen;
        }

        // ---- Y9 — ÇAPRAZ TABLO (PIVOT) ----
        // Kaynak, sarmalanan sorgunun kendisidir; PIVOT başlık ve değer DIŞINDAKİ TÜM kolonlara
        // göre satır oluşturur (klasik tuzak: sonuca fazladan bir kolon girerse satırlar bölünür) —
        // bu yüzden satır ekseni "çapraza girmeyen kolonlar" olarak açıkça anlatılır.
        if (model.Capraz is PivotSpec pv)
        {
            if (disOzet || disGruplar.Count > 0 || ilkNKolon is not null || disKosullar.Count > 0)
                throw new QueryModelException(
                    "Çapraz tablo, ara küme özeti / grup içi ilk N / pencere kolonuna göre filtre ile birlikte kullanılamaz — " +
                    "önce bu sonucu kaydedip ikinci bir raporda çaprazlayın.");
            var baslikAd = DisAd(pv.BaslikKolonu ?? "", "Çapraz tablo başlık kolonu");
            var degerAd = DisAd(pv.DegerKolonu ?? "", "Çapraz tablo değer kolonu");
            if (baslikAd.Equals(degerAd, StringComparison.OrdinalIgnoreCase))
                throw new QueryModelException("Çapraz tabloda başlık ve değer aynı kolon olamaz.");
            var basliklar = (pv.Basliklar ?? []).Select(b => (b ?? "").Trim()).Where(b => b.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (basliklar.Count == 0)
                throw new QueryModelException(
                    "Çapraz tablo: sütuna dönecek DEĞERLERİ girin (SQL'de çapraz sütun listesi sabittir; " +
                    "ekrandaki “başlıkları getir” düğmesi bu listeyi veriden okur).");
            if (pv.Toplama == ToplamaTuru.Yok)
                throw new QueryModelException("Çapraz tabloda hücre bir toplama (Topla/Say/Ortalama/…) ile hesaplanır.");
            var satirKolonlari = cikisIfadeleri.Keys
                .Where(k => !k.Equals(baslikAd, StringComparison.OrdinalIgnoreCase)
                         && !k.Equals(degerAd, StringComparison.OrdinalIgnoreCase)).ToList();

            var cp = new StringBuilder();
            cp.Append("SELECT ").Append(topOnek)
              .Append(string.Join(", ", satirKolonlari.Concat(basliklar).Select(Q)))
              .Append("\nFROM (\n").Append(sb).Append("\n) AS ").Append(Q("_kaynak"))
              .Append("\nPIVOT (").Append(AggregationSql(pv.Toplama, Q(degerAd)))
              .Append(" FOR ").Append(Q(baslikAd))
              .Append(" IN (").Append(string.Join(", ", basliklar.Select(Q))).Append(")) AS ").Append(Q("_capraz"));
            // Sıralama yalnız çapraz sonucunda VAR OLAN kolonlarla yapılabilir.
            var cpAlias = new HashSet<string>(satirKolonlari.Concat(basliklar), StringComparer.OrdinalIgnoreCase);
            var cpSira = new List<string>();
            foreach (var s in model.Siralama)
            {
                var ad = !string.IsNullOrWhiteSpace(s.CikisAdi)
                    ? s.CikisAdi.Trim()
                    : secilen.FirstOrDefault(x => x.alan.TabloId == s.TabloId && x.alan.Kolon == s.Kolon
                        && x.alan.Toplama == s.Toplama && x.alan.Kova == s.Kova).cikis;
                if (ad is null) continue;
                if (!cpAlias.Contains(ad))
                    throw new QueryModelException(
                        $"'{ad}' çapraz tabloda bir kolon değil — sıralama satır kolonlarıyla ya da sütun başlıklarıyla yapılabilir: "
                        + string.Join(", ", cpAlias));
                cpSira.Add(Q(ad) + (s.Azalan ? " DESC" : " ASC"));
            }
            if (cpSira.Count > 0) cp.Append("\nORDER BY ").Append(string.Join(", ", cpSira));
            if (sayfaSonEk is not null)
            {
                if (cpSira.Count == 0)
                    throw new QueryModelException("Sayfalama için bir SIRALAMA gerekir.");
                cp.Append(sayfaSonEk);
            }
            return new GeneratedQuery(cp.ToString(), parametreler, araTablolar);
        }

        var disAlias = new HashSet<string>(StringComparer.Ordinal);
        var disSelect = new List<string>();
        if (disOzet || disGruplar.Count > 0)
        {
            // "Özetin özeti": dış katman yalnız seçilen kırılımları + toplamaları taşır.
            foreach (var g in disGruplar)
            {
                var ad = DisAd(g, "Ara küme kırılımı");
                disAlias.Add(ad);
                disSelect.Add(Q(ad));
            }
            foreach (var oz in model.AraKumeOzeti ?? [])
            {
                if (oz.Toplama == ToplamaTuru.Yok)
                    throw new QueryModelException(
                        "Ara küme özeti bir toplama (Say/Topla/Ortalama/…) içermeli — ham kolon için kırılım listesini kullanın.");
                var ad = DisAd(oz.CikisAdi ?? "", "Ara küme özeti");
                var cikis = UniqueAlias(oz.TakmaAd is { Length: > 0 } ta ? ta : ToplamaOnek[oz.Toplama] + ad, disAlias);
                disSelect.Add(AggregationSql(oz.Toplama, Q(ad)) + " AS " + Q(cikis));
            }
        }
        else
        {
            // Kırpma/filtre katmanı: sonuç kolonları AYNEN geçer (yardımcı kolonlar hariç).
            // Y10: ara toplam etiketi açıksa KIRILIM kolonları, o satır bir özet satırıysa etiketle
            // değiştirilir — GROUPING() bayrağı sayesinde gerçek boş değerle karışmaz.
            foreach (var (ad, ifade) in cikisIfadeleri)
            {
                disAlias.Add(ad);
                if (model.AraToplam is SubtotalSpec at2 && at2.Etiketle && toplamBayraklari.TryGetValue(ifade, out var bayrak))
                {
                    var etiket = string.IsNullOrWhiteSpace(at2.Etiket) ? "(Tümü)" : at2.Etiket;
                    disSelect.Add("CASE WHEN " + Q(bayrak) + " = 1 THEN " + YeniParam(etiket)
                        + " ELSE CAST(" + Q(ad) + " AS nvarchar(4000)) END AS " + Q(ad));
                }
                else disSelect.Add(Q(ad));
            }
        }

        var disWhere = new List<string>();
        if (ilkNKolon is not null)
        {
            var ilkNAd = SabitParam("ilkN"); parametreler[ilkNAd] = model.GrupIciIlkN!.Adet;
            disWhere.Add(Q(ilkNKolon) + " <= " + ilkNAd);
        }
        if (disKosullar.Count > 0) disWhere.Add("(" + string.Join(" ", disKosullar) + ")");

        // Dış katmanda ORDER BY yalnız DIŞ projeksiyonda bulunan kolonlara yapılabilir (özet katmanı
        // iç kolonları yutar) — sessizce düşürmek yerine ne yapılacağını söyleyen hata veriyoruz.
        var disSira = new List<string>();
        foreach (var s in model.Siralama)
        {
            var ad = !string.IsNullOrWhiteSpace(s.CikisAdi)
                ? DisAd(s.CikisAdi, "Sıralama")
                : secilen.FirstOrDefault(x => x.alan.TabloId == s.TabloId && x.alan.Kolon == s.Kolon
                    && x.alan.Toplama == s.Toplama && x.alan.Kova == s.Kova).cikis;
            if (ad is null) continue;
            if (!disAlias.Contains(ad))
                throw new QueryModelException(
                    $"'{ad}' ara kümenin dışında kaldığı için sıralamada kullanılamaz — " +
                    "ya ara küme kırılımlarına ekleyin ya da sıralamayı dış kolonlardan biriyle yapın: "
                    + string.Join(", ", disAlias));
            disSira.Add(Q(ad) + (s.Azalan ? " DESC" : " ASC"));
        }

        var dis = new StringBuilder();
        dis.Append("SELECT ").Append(topOnek).Append(string.Join(", ", disSelect))
           .Append("\nFROM (\n").Append(sb).Append("\n) AS ").Append(Q("_arakume"));
        if (disWhere.Count > 0) dis.Append("\nWHERE ").Append(string.Join(" AND ", disWhere));
        if (disGruplar.Count > 0)
            dis.Append("\nGROUP BY ").Append(string.Join(", ", disGruplar.Select(g => Q(DisAd(g, "Ara küme kırılımı")))));
        if (disSira.Count > 0) dis.Append("\nORDER BY ").Append(string.Join(", ", disSira));
        if (sayfaSonEk is not null)
        {
            if (disSira.Count == 0)
                throw new QueryModelException("Sayfalama için bir SIRALAMA gerekir.");
            dis.Append(sayfaSonEk);
        }
        return new GeneratedQuery(dis.ToString(), parametreler, araTablolar);
    }

    /// <summary>Y15 — birleşim (UNION / UNION ALL). Her dal KENDİ başına üretilir (parametre adları
    /// önekle ayrılır: @b1p0, @b2p0…), sonra alt alta eklenip bir türetilmiş tabloya sarılır.
    /// Sarmalama şart değil ama sıralama/sayfalamayı tek yerden ve çıkış ADIYLA uygulamayı sağlar.
    /// <para>Kural: dalların kolon SAYISI aynı olmalı (SQL zorunluluğu — anlaşılır mesajla erken
    /// söylenir) ve dalların kendi sıralaması/limiti olamaz (birleşimde anlamsızdır).</para></summary>
    private GeneratedQuery BirlesimUret(QueryModel model, DataMapDetail dataMap,
        IReadOnlyDictionary<string, object?>? parametreDegerleri, string paramOnek,
        IReadOnlyList<RowFilter>? satirKurallari)
    {
        var dallar = new List<QueryModel> { model with { Birlesim = null } };
        dallar.AddRange(model.Birlesim!);

        var parametreler = new Dictionary<string, object?>();
        var sqlDallari = new List<string>();
        int? kolonSayisi = null;
        IReadOnlyList<string>? ilkKolonAdlari = null;

        for (var i = 0; i < dallar.Count; i++)
        {
            // Sıralama/limit/sayfalama yalnız BÜTÜNE uygulanır: dal üretiminde kapatılır.
            var dal = dallar[i];
            if (i > 0 && (dal.Siralama is { Count: > 0 } || dal.SatirLimiti is not null || dal.Atla is not null))
                throw new QueryModelException(
                    $"{i + 1}. sorgunun kendi sıralaması/limiti olamaz — birleşimde sıralama ve satır limiti " +
                    "TÜM sonuca uygulanır; bunları ilk sorguda tanımlayın.");
            var uretilen = Uret(dal with { Siralama = [], SatirLimiti = null, Atla = null },
                dataMap, parametreDegerleri, paramOnek + "b" + (i + 1), satirKurallari);
            foreach (var (ad, deger) in uretilen.Parametreler) parametreler[ad] = deger;

            var adlar = CikisAdlariniOku(uretilen.Sql);
            if (kolonSayisi is null) { kolonSayisi = adlar.Count; ilkKolonAdlari = adlar; }
            else if (adlar.Count != kolonSayisi)
                throw new QueryModelException(
                    $"Birleştirilen sorguların kolon SAYISI aynı olmalı: 1. sorguda {kolonSayisi}, " +
                    $"{i + 1}. sorguda {adlar.Count} kolon var.");
            sqlDallari.Add(uretilen.Sql);
        }

        var sb = new StringBuilder();
        sb.Append("SELECT ");
        // Limit/sayfalama sarmalayıcıda: TOP ve OFFSET aynı sorguda kullanılamaz.
        string? sayfaSonEk = null;
        if (model.Atla is int atla)
        {
            if (atla < 0) throw new QueryModelException("Sayfalamada atlanacak satır sayısı negatif olamaz.");
            var atlaAd = paramOnek == "p" ? "@atla" : "@" + paramOnek + "atla";
            parametreler[atlaAd] = atla;
            sayfaSonEk = "\nOFFSET (" + atlaAd + ") ROWS";
            if (model.SatirLimiti is int sayfa)
            {
                var sayfaAd = paramOnek == "p" ? "@satir" : "@" + paramOnek + "satir";
                parametreler[sayfaAd] = sayfa;
                sayfaSonEk += " FETCH NEXT (" + sayfaAd + ") ROWS ONLY";
            }
        }
        else if (model.SatirLimiti is int lim)
        {
            if (lim <= 0) throw new QueryModelException("Satır limiti pozitif olmalı.");
            var satirAd = paramOnek == "p" ? "@satir" : "@" + paramOnek + "satir";
            parametreler[satirAd] = lim;
            sb.Append("TOP (").Append(satirAd).Append(") ");
        }
        var cikisAdlari = ilkKolonAdlari ?? [];
        sb.Append(string.Join(", ", cikisAdlari.Select(Q)))
          .Append("\nFROM (\n")
          .Append(string.Join(model.BirlesimTekille ? "\nUNION\n" : "\nUNION ALL\n", sqlDallari))
          .Append("\n) AS ").Append(Q("_birlesim"));

        // Sıralama: birleşimin çıkış ADLARIYLA (ilk sorgunun kolon adları).
        var sira = new List<string>();
        foreach (var s in model.Siralama)
        {
            var ad = cikisAdlari.FirstOrDefault(k =>
                k.Equals((s.CikisAdi ?? s.Kolon ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (ad is null)
                throw new QueryModelException(
                    $"Birleşimde sıralama, sonucun kolon ADIYLA yapılır: '{s.CikisAdi ?? s.Kolon}' bulunamadı. " +
                    "Kullanılabilir kolonlar: " + string.Join(", ", cikisAdlari));
            sira.Add(Q(ad) + (s.Azalan ? " DESC" : " ASC"));
        }
        if (sira.Count > 0) sb.Append("\nORDER BY ").Append(string.Join(", ", sira));
        if (sayfaSonEk is not null)
        {
            if (sira.Count == 0) throw new QueryModelException("Sayfalama için bir SIRALAMA gerekir.");
            sb.Append(sayfaSonEk);
        }

        /* Birleşim (UNION) dalında ara tablo raporlanmaz: burada tek bir join planı yok, her alt
         * sorgunun kendi planı var. Bu dal NLQ'dan da gelmiyor (model birleşim kurmuyor) — boş
         * bırakmak, yanlış birleştirilmiş bir liste sunmaktan doğrudur. */
        return new GeneratedQuery(sb.ToString(), parametreler);
    }

    /// <summary>Üretilen SQL'in ilk SELECT listesindeki çıkış adlarını okur. Adlar bizim
    /// ürettiğimiz <c>… AS [Ad]</c> kalıbındadır (kullanıcı metni değil) — bu yüzden basit ve
    /// güvenli bir tarama yeter.</summary>
    private static List<string> CikisAdlariniOku(string sql)
    {
        var adlar = new List<string>();
        var satir = sql.Split('\n')[0];
        var i = 0;
        while (true)
        {
            var yer = satir.IndexOf(" AS [", i, StringComparison.Ordinal);
            if (yer < 0) break;
            var bas = yer + 5;
            var son = bas;
            while (son < satir.Length)
            {
                if (satir[son] == ']')
                {
                    if (son + 1 < satir.Length && satir[son + 1] == ']') { son += 2; continue; } // ]] kaçışı
                    break;
                }
                son++;
            }
            adlar.Add(satir[bas..son].Replace("]]", "]"));
            i = son + 1;
        }
        return adlar;
    }

    // --- filtre ---

    private static void WalkFilter(FilterNode? d, Action<Guid, string> dogrula, HashSet<Guid> kullanilan)
    {
        switch (d)
        {
            case null: return;
            case FilterGroup g:
                if (g.Cocuklar is not null) foreach (var c in g.Cocuklar) WalkFilter(c, dogrula, kullanilan);
                return;
            case FilterCondition k:
                dogrula(k.TabloId, k.Kolon); kullanilan.Add(k.TabloId); return;
            default: throw new QueryModelException("Bilinmeyen filtre düğümü.");
        }
    }

    private static string? FilterSql(FilterNode d, Func<Guid, string, string> kolIfade,
        Func<object?, string> yeniParam, Func<Guid, string, string?> kolonTipi, Func<object?, object?> cozParam)
    {
        switch (d)
        {
            case FilterGroup g:
            {
                if (g.Cocuklar is null || g.Cocuklar.Count == 0) return null;
                var parcalar = new List<string>();
                foreach (var c in g.Cocuklar)
                    if (FilterSql(c, kolIfade, yeniParam, kolonTipi, cozParam) is { } s) parcalar.Add(s);
                if (parcalar.Count == 0) return null;
                var baglac = g.Baglac switch
                {
                    MantikBaglaci.Ve => " AND ",
                    MantikBaglaci.Veya => " OR ",
                    _ => throw new QueryModelException("Bilinmeyen mantık bağlacı."),
                };
                return "(" + string.Join(baglac, parcalar) + ")";
            }
            case FilterCondition k:
                return ConditionSql(k, kolIfade, yeniParam, kolonTipi, cozParam);
            default:
                throw new QueryModelException("Bilinmeyen filtre düğümü.");
        }
    }

    private static string ConditionSql(FilterCondition k, Func<Guid, string, string> kolIfade,
        Func<object?, string> yeniParam, Func<Guid, string, string?> kolonTipi, Func<object?, object?> cozParam)
    {
        var kol = kolIfade(k.TabloId, k.Kolon);
        var d = k.Degerler ?? [];
        var tip = kolonTipi(k.TabloId, k.Kolon);
        // Değer parametre-referansıysa (cozParam) önce çalışma değerine çözülür, SONRA kolon tipine.
        string Param(object? deger) => yeniParam(ConvertType(cozParam(deger), tip, k.Kolon));
        switch (k.Op)
        {
            case KarsilastirmaOp.Bos: return kol + " IS NULL";
            case KarsilastirmaOp.DoluDegil: return kol + " IS NOT NULL";
            case KarsilastirmaOp.Icinde:
                if (d.Count == 0) throw new QueryModelException("'İçinde' filtresi en az bir değer ister.");
                return kol + " IN (" + string.Join(", ", d.Select(Param)) + ")";
            case KarsilastirmaOp.Arasinda:
                if (d.Count != 2) throw new QueryModelException("'Arasında' filtresi tam iki değer ister.");
                return kol + " BETWEEN " + Param(d[0]) + " AND " + Param(d[1]);
            case KarsilastirmaOp.Icerir:
                if (d.Count != 1) throw new QueryModelException("'İçerir' filtresi tek değer ister.");
                // Metin araması: değer daima string kalır. Parametreli; içindeki LIKE jokerleri
                // (% _ [) LİTERAL aransın diye kaçışlanır ([x] karakter-sınıfıyla). Sıra: önce [.
                var ham = RawText(cozParam(d[0]))
                    .Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                /* HARF/AKSAN DUYARSIZ ARAMA (S39, 25 Ağu 2026): kullanıcı "AUDİ" yazıyor, veride
                 * "Audi" duruyor; varsayılan collation'da 'İ' (U+0130) ile 'i' AYRI karakterlerdir
                 * ve eşleşme sessizce BOŞ döner — NLQ'da en sinsi yanlış budur (sorgu "başarılı",
                 * sonuç 0 satır).
                 *
                 * ÖNCE Turkish_CI_AI denendi ve İŞE YARAMADI (ölçüldü): Türkçe alfabede İ/i ve I/ı
                 * AYRI harf çiftleridir, yani Türkçe collation tam da istemediğimiz ayrımı KORUR —
                 * 'Audi' ile 'AUDİ' eşleşmez, hatta 'AUDI' bile eşleşmez ('I'nın küçüğü 'ı'dır).
                 * Latin AI collation ise noktayı AKSAN sayar: 'İ'≈'i', ayrıca ş≈s, ğ≈g, ü≈u, ö≈o,
                 * ç≈c — Türkçe metin aramasında kullanıcının beklediği davranış budur.
                 *
                 * 'İçerir' zaten LIKE'tır, yani indeks aramasını ZATEN kullanmaz: collation'ın ek
                 * maliyeti yok. Eşitlik (=) BİLEREK dokunulmaz — orada indeks seek'i bozardı. */
                return kol + " COLLATE SQL_Latin1_General_CP1_CI_AI LIKE " + yeniParam("%" + ham + "%");
            default:
                if (d.Count != 1) throw new QueryModelException("Bu filtre tek değer ister.");
                var op = k.Op switch
                {
                    KarsilastirmaOp.Esit => "=",
                    KarsilastirmaOp.EsitDegil => "<>",
                    KarsilastirmaOp.Buyuk => ">",
                    KarsilastirmaOp.BuyukEsit => ">=",
                    KarsilastirmaOp.Kucuk => "<",
                    KarsilastirmaOp.KucukEsit => "<=",
                    _ => throw new QueryModelException("Bilinmeyen operatör."),
                };
                return kol + " " + op + " " + Param(d[0]);
        }
    }

    // --- filtre değeri tip dönüşümü ---

    /// <summary>JSON'dan gelen değeri (çoğu zaman string ya da <see cref="JsonElement"/>) kolonun
    /// dataMap <c>VeriTipi</c>'ne göre CLR tipine çevirir. Çevrilemiyorsa icra 500'ü yerine anlaşılır
    /// <see cref="QueryModelException"/> (→ 400) fırlatır. Bilinmeyen/boş tip → metin olarak geçer.</summary>
    private static object? ConvertType(object? deger, string? veriTipi, string kolon)
    {
        var duz = DecodeJson(deger);
        if (duz is null) return null;
        var tip = (veriTipi ?? string.Empty).ToLowerInvariant();
        var s = duz as string;

        object? Hata(string beklenen) =>
            throw new QueryModelException($"'{s ?? duz}' değeri {kolon} kolonunun tipine ({veriTipi}) çevrilemedi — {beklenen} bekleniyor.");

        if (tip == "uniqueidentifier")
            return duz is Guid g ? g : Guid.TryParse(s, out var gs) ? gs : Hata("GUID (ör. 3F2504E0-4F89-...)");
        if (tip is "int" or "bigint" or "smallint" or "tinyint")
            return duz switch
            {
                long l => l,
                double dd when double.IsInteger(dd) => (long)dd,
                _ => long.TryParse(s, out var ls) ? ls : Hata("tam sayı"),
            };
        if (tip == "bit")
            return duz switch
            {
                bool b => b,
                long l => l != 0,
                _ => s is "1" ? true : s is "0" ? false : bool.TryParse(s, out var bs) ? bs : Hata("1/0 ya da true/false"),
            };
        // Kod incelemesi Y2: para/kesin ondalık tipler DECIMAL bağlanır (double ikili-kayan
        // hassasiyeti para karşılaştırmalarını saptırır; float parametre tipi indeks verimini
        // bozabilir). float/real ise SQL tarafında da ikili-kayan olduğundan double kalır.
        if (tip is "decimal" or "numeric" or "money" or "smallmoney")
        {
            if (duz is decimal dm) return dm;
            if (duz is double dd2) return (decimal)dd2;
            if (duz is long l2) return (decimal)l2;
            var norm = s?.Replace(',', '.'); // "10,5" gibi Türkçe girişi de kabul et
            return decimal.TryParse(norm, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var dsm) ? dsm : Hata("sayı");
        }
        if (tip is "float" or "real")
        {
            if (duz is double dd3) return dd3;
            if (duz is long l3) return (double)l3;
            var norm = s?.Replace(',', '.');
            return double.TryParse(norm, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ds) ? ds : Hata("sayı");
        }
        if (tip.StartsWith("date", StringComparison.Ordinal) || tip == "smalldatetime")
            return DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var t) ? t : Hata("tarih (ör. 2026-01-31)");

        return s ?? duz; // nvarchar/char/bilinmeyen → metin
    }

    /// <summary>API yolunda değerler System.Text.Json <see cref="JsonElement"/> olarak gelir;
    /// karşılaştırma/çevirme için önce CLR ilkeline indirger.</summary>
    private static object? DecodeJson(object? deger) => deger switch
    {
        System.Text.Json.JsonElement e => e.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => e.GetString(),
            System.Text.Json.JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            _ => e.ToString(),
        },
        _ => deger,
    };

    private static string RawText(object? deger) => DecodeJson(deger)?.ToString() ?? string.Empty;

    // --- parametre referansı ---

    /// <summary>Filtre değeri bir parametre referansı mı? Tel biçimi
    /// <c>{ "$param": "anahtar", "parca"?: "baslangic|bitis" }</c> (API'de JsonElement; testlerde
    /// IDictionary de kabul). Değilse false — değer sabittir.</summary>
    private static bool ParametreRefMi(object? deger, out string anahtar, out string? parca)
    {
        anahtar = string.Empty;
        parca = null;
        if (deger is System.Text.Json.JsonElement e && e.ValueKind == System.Text.Json.JsonValueKind.Object
            && e.TryGetProperty("$param", out var pk) && pk.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            anahtar = pk.GetString() ?? string.Empty;
            if (e.TryGetProperty("parca", out var pc) && pc.ValueKind == System.Text.Json.JsonValueKind.String)
                parca = pc.GetString();
            return anahtar.Length > 0;
        }
        if (deger is IDictionary<string, object?> dd && dd.TryGetValue("$param", out var a) && a is string s && s.Length > 0)
        {
            anahtar = s;
            if (dd.TryGetValue("parca", out var pcv) && pcv is string ps) parca = ps;
            return true;
        }
        return false;
    }

    /// <summary>TarihAralik değerinin (<c>{ baslangic, bitis }</c>) istenen ucunu döndürür
    /// (<paramref name="parca"/> boş → baslangic). Bulunamazsa null.</summary>
    private static object? AralikUcu(object? aralik, string? parca)
    {
        var uc = string.IsNullOrEmpty(parca) ? "baslangic" : parca;
        return aralik switch
        {
            System.Text.Json.JsonElement e when e.ValueKind == System.Text.Json.JsonValueKind.Object
                && e.TryGetProperty(uc, out var v) => DecodeJson(v),
            IDictionary<string, object?> d when d.TryGetValue(uc, out var dv) => dv,
            _ => null,
        };
    }

    // --- join planı (BFS spanning tree) ---

    /// <summary>İki tablo Id'sini yönden bağımsız (kanonik) çift-anahtara indirger.</summary>
    private static (Guid, Guid) Cift(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    /// <summary>Filtre ağacının YAPISAL imzası — özet alanları tek türetilmiş tabloda birleştirirken
    /// "aynı filtre mi?" sorusunu yanıtlar. Üretilen SQL metni karşılaştırılamaz: aynı filtre her
    /// üretimde farklı parametre adı (@p0/@p1) alır. Filtre ağacı JSON'a polimorfik serileşir
    /// (<c>tur</c> ayrımcısıyla), bu yüzden imza deterministiktir.</summary>
    private static string FiltreImzasi(FilterNode? filtre) =>
        filtre is null ? string.Empty : System.Text.Json.JsonSerializer.Serialize(filtre);

    /// <summary>Planlanmış tek join kenarı: çocuk tablo + türü + koşulları (bileşik olabilir).</summary>
    private sealed record EdgePlan(Guid Cocuk, JoinType Tur,
        IReadOnlyList<(string CocukKol, Guid AnaTablo, string AnaKol)> Kosullar);

    /// <summary>Join ağacını kurar. İki katman: (1) kullanıcının JOIN AYARLARI (ebeveyn/tür/koşul)
    /// zincir çözülerek bağlanır — ebeveyni köke ulaşmayan ayar reddedilir; (2) kalan hedef
    /// tablolar KULLANILAN-öncelikli deterministik seçimle bağlanır (test turu M4/M13/M15;
    /// frontend buildTree ile birebir aynı kural). <paramref name="hedefSirasi"/> = hedeflerin
    /// deterministik işleme sırası (alan ekleme sırası). Çift-kolon override'ı (joinSecim) her
    /// iki katmanda geçerli.</summary>
    private static (List<Guid> sira, List<EdgePlan> kenarlar) JoinPlani(
        DataMapDetail dataMap, Guid kok, HashSet<Guid> hedefler, IReadOnlyList<Guid> hedefSirasi,
        IReadOnlyDictionary<(Guid, Guid), DataMapRelationshipDto> joinSecim,
        IReadOnlyDictionary<Guid, JoinSetting> ayarlar)
    {
        (string, Guid, string)[] CiftKosul(Guid cocuk, Guid ana)
        {
            var cift = Cift(cocuk, ana);
            var il = joinSecim.TryGetValue(cift, out var sec) ? sec
                : dataMap.Iliskiler.FirstOrDefault(i => Cift(i.KaynakTabloId, i.HedefTabloId) == cift)
                ?? throw new QueryModelException(
                    "Join ayarındaki tablolar arasında haritada ilişki yok — koşulları elle belirtin.");
            var (ck, at, ak) = KenarUclari(il, cocuk);
            return [(ck, at, ak)];
        }

        // 1) KULLANILAN-öncelikli, deterministik ebeveyn seçimi (test turu M4/M13/M15). Eski kör
        //    BFS haritanın TÜMÜNDE ilişki-dizisi sırasıyla koşuyordu: kullanılan tablo, kullanılmayan
        //    bir ARA tablo üzerinden erken keşfedilebiliyor (KisiAdresleri↔Adresler bağlanamıyor
        //    görünüyordu) ya da kestirme FK'ya bağlanıyordu (Iller'e Adresler.IlId dururken
        //    Kisiler.DogumYeriIlId). Yeni kural — frontend buildTree (designer/model.ts) birebir aynı:
        //    (a) ayarlı tablo, ebeveyni yerleşince yerleşir;
        //    (b) kullanılan tablo MÜMKÜNSE yerleşik bir tabloya DOĞRUDAN bağlanır; birden çok aday
        //        varsa EN DERİN ebeveyn kazanır (snowflake zinciri sezgisi), eşitlikte ilişki sırası;
        //    (c) doğrudan bağ kalmadıysa yerleşik kümeden çok-kaynaklı BFS ile en yakın kullanılan
        //        tabloya ARA-zincir kurulur (aradaki tablolar da plana girer).
        var otoParent = new Dictionary<Guid, (Guid ana, DataMapRelationshipDto il)>();
        var derinlikSecim = new Dictionary<Guid, int> { [kok] = 0 };
        var yerlesikSira = new List<Guid> { kok };
        var yerlesti = new HashSet<Guid> { kok };
        void Yerlestir(Guid t, Guid ana, DataMapRelationshipDto? il)
        {
            yerlesti.Add(t);
            yerlesikSira.Add(t);
            derinlikSecim[t] = derinlikSecim.GetValueOrDefault(ana) + 1;
            if (il is not null) otoParent[t] = (ana, il);
        }

        var bekleyen = hedefSirasi.Where(t => t != kok).ToList();
        var ilerleme = true;
        while (ilerleme && bekleyen.Any(t => !yerlesti.Contains(t)))
        {
            ilerleme = false;
            // (a) ayarlılar: kullanıcı ebeveyni sabitledi — EBEVEYN ve koşullarının başvurduğu TÜM
            //     tablolar yerleşince yerleşir (M5: koşul tabloları mantıksal ebeveyndir; erken
            //     yerleşirse otomatik tablolar ona bağlanıp döngü doğurabiliyordu).
            foreach (var t in bekleyen)
            {
                if (yerlesti.Contains(t) || !ayarlar.TryGetValue(t, out var a)) continue;
                var dayanaklar = new List<Guid> { a.EbeveynTabloId };
                if (a.Kosullar is not null)
                    dayanaklar.AddRange(a.Kosullar.Select(k => k.EbeveynTabloId ?? a.EbeveynTabloId));
                if (dayanaklar.All(yerlesti.Contains))
                {
                    var enDerin = dayanaklar.OrderByDescending(x => derinlikSecim.GetValueOrDefault(x)).First();
                    Yerlestir(t, a.EbeveynTabloId, null);
                    derinlikSecim[t] = derinlikSecim.GetValueOrDefault(enDerin) + 1; // derinlik = en derin dayanak + 1
                    ilerleme = true;
                }
            }
            if (ilerleme) continue;

            // (b) doğrudan bağ: ekleme sırasıyla ilk uygun tablo; en derin yerleşik komşu kazanır.
            foreach (var t in bekleyen)
            {
                if (yerlesti.Contains(t) || ayarlar.ContainsKey(t)) continue;
                (Guid ana, DataMapRelationshipDto il)? aday = null;
                foreach (var il in dataMap.Iliskiler)
                {
                    var p = il.KaynakTabloId == t && yerlesti.Contains(il.HedefTabloId) ? il.HedefTabloId
                        : il.HedefTabloId == t && yerlesti.Contains(il.KaynakTabloId) ? il.KaynakTabloId : (Guid?)null;
                    if (p is Guid pg && (aday is null
                        || derinlikSecim.GetValueOrDefault(pg) > derinlikSecim.GetValueOrDefault(aday.Value.ana)))
                        aday = (pg, il);
                }
                if (aday is { } secim) { Yerlestir(t, secim.ana, secim.il); ilerleme = true; break; }
            }
            if (ilerleme) continue;

            // (c) ara-zincir: yerleşik kümeden dışarı BFS; ulaşılan İLK bekleyen kullanılan tablo,
            //     aradaki (kullanılmayan) tablolarla birlikte zincir hâlinde yerleşir.
            var bfsParent = new Dictionary<Guid, (Guid ana, DataMapRelationshipDto il)>();
            var gorulen = new HashSet<Guid>(yerlesti);
            var kuyruk = new Queue<Guid>(yerlesikSira);
            Guid? hedefT = null;
            while (kuyruk.Count > 0 && hedefT is null)
            {
                var u = kuyruk.Dequeue();
                foreach (var il in dataMap.Iliskiler)
                {
                    var v = il.KaynakTabloId == u ? il.HedefTabloId : il.HedefTabloId == u ? il.KaynakTabloId : (Guid?)null;
                    if (v is not Guid vg || !gorulen.Add(vg)) continue;
                    bfsParent[vg] = (u, il);
                    if (bekleyen.Contains(vg) && !ayarlar.ContainsKey(vg) && !yerlesti.Contains(vg)) { hedefT = vg; break; }
                    kuyruk.Enqueue(vg);
                }
            }
            if (hedefT is Guid hg)
            {
                var zincir = new List<Guid>();
                for (var c = hg; !yerlesti.Contains(c); c = bfsParent[c].ana) zincir.Insert(0, c);
                foreach (var c in zincir) { var pc = bfsParent[c]; Yerlestir(c, pc.ana, pc.il); }
                ilerleme = true;
            }
        }

        // 2) Birleşik ebeveyn: ayar varsa o, yoksa seçim. Her hedefin köke zinciri yürünür (gerekli
        //    küme + döngü/kopukluk denetimi) — ayar ebeveyni otomatik bağlanan bir tablo da olabilir.
        Guid? ParentOf(Guid t) => ayarlar.TryGetValue(t, out var a) ? a.EbeveynTabloId
            : otoParent.TryGetValue(t, out var p) ? p.ana : null;

        var gerekli = new HashSet<Guid> { kok };
        foreach (var h in hedefler)
        {
            var cur = h;
            var yol = new HashSet<Guid>();
            var ayarliZincir = false;
            while (cur != kok)
            {
                ayarliZincir |= ayarlar.ContainsKey(cur);
                if (!yol.Add(cur) || ParentOf(cur) is not Guid ana)
                    throw new QueryModelException(ayarliZincir
                        ? "Join ayarındaki ebeveyn zinciri FROM köküne ulaşmıyor (döngü ya da kopukluk)."
                        : "Seçilen tablolar haritada ilişkili değil; aralarında bir join yolu yok.");
                gerekli.Add(cur);
                cur = ana;
            }
        }

        // 3) Sıra: köke uzaklığa göre (ebeveyn hep önce); eşit derinlikte Guid — deterministik.
        //    M5: çok-tablolu join koşulları ek bağımlılık yaratır — koşulun başvurduğu HER tablo,
        //    çocuktan ÖNCE join'lenmiş olmalı (alias'ı ON'da kullanılıyor). Kahn topolojik sıralama;
        //    hazır adaylar arasında (derinlik, Guid) önceliği korunur.
        var derinlik = new Dictionary<Guid, int> { [kok] = 0 };
        int Derinlik(Guid t)
        {
            if (derinlik.TryGetValue(t, out var d)) return d;
            var dd = Derinlik(ParentOf(t)!.Value) + 1; // gerekli kümesi için zincir yukarıda doğrulandı
            derinlik[t] = dd;
            return dd;
        }
        var bagimlilik = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var t in gerekli.Where(x => x != kok))
        {
            var b = new HashSet<Guid>();
            if (ParentOf(t) is Guid ebeveynT) b.Add(ebeveynT);
            if (ayarlar.TryGetValue(t, out var ayarT) && ayarT.Kosullar is not null)
                foreach (var k in ayarT.Kosullar)
                {
                    var referans = k.EbeveynTabloId ?? ayarT.EbeveynTabloId;
                    if (!gerekli.Contains(referans))
                        throw new QueryModelException(
                            "Join koşulu, sorguda yer almayan bir tabloya başvuruyor — önce o tabloyu sorguya ekleyin ya da koşulu düzeltin.");
                    b.Add(referans);
                }
            b.Remove(t);
            b.Remove(kok); // kök zaten hep ilk
            bagimlilik[t] = b;
        }
        var sira = new List<Guid> { kok };
        var kalanlar = gerekli.Where(t => t != kok).ToHashSet();
        while (kalanlar.Count > 0)
        {
            var hazir = kalanlar.Where(t => !bagimlilik[t].Any(kalanlar.Contains))
                .OrderBy(Derinlik).ThenBy(t => t).ToList();
            if (hazir.Count == 0)
                throw new QueryModelException(
                    "Join koşulları arasında döngü var — iki tablonun koşulları birbirine karşılıklı başvuramaz.");
            sira.Add(hazir[0]);
            kalanlar.Remove(hazir[0]);
        }

        // Ara tablolar KolonDogrula'dan geçmez; bozuk haritada fantom tablo → açıkça reddet.
        var haritadaki = dataMap.Tablolar.Select(t => t.Id).ToHashSet();
        var kenarlar = new List<EdgePlan>();
        foreach (var t in sira.Where(t => t != kok))
        {
            if (!haritadaki.Contains(t))
                throw new QueryModelException("Harita ilişkisi, haritada tanımlı olmayan bir tabloya işaret ediyor.");
            if (ayarlar.TryGetValue(t, out var a))
            {
                var kosullar = a.Kosullar is { Count: > 0 }
                    // M5: her koşul kendi tablosuna başvurabilir (null = ayarın ana ebeveyni).
                    ? a.Kosullar.Select(k => (k.TabloKolon, k.EbeveynTabloId ?? a.EbeveynTabloId, k.EbeveynKolon)).ToArray()
                    : CiftKosul(t, a.EbeveynTabloId);
                kenarlar.Add(new EdgePlan(t, a.Tur, kosullar));
            }
            else
            {
                var (ana, varsayilan) = otoParent[t];
                var il = joinSecim.TryGetValue(Cift(t, ana), out var secilen) ? secilen : varsayilan;
                var (ck, at, ak) = KenarUclari(il, t);
                kenarlar.Add(new EdgePlan(t, JoinType.Inner, [(ck, at, ak)]));
            }
        }
        return (sira, kenarlar);
    }

    private static (string cocukKol, Guid digerTablo, string digerKol) KenarUclari(DataMapRelationshipDto il, Guid cocuk) =>
        il.KaynakTabloId == cocuk
            ? (il.KaynakKolon, il.HedefTabloId, il.HedefKolon)
            : (il.HedefKolon, il.KaynakTabloId, il.KaynakKolon);

    private static Dictionary<Guid, string> AssignAlias(List<Guid> sira, Dictionary<Guid, DataMapTableDto> tablolar)
    {
        var result = new Dictionary<Guid, string>();
        var sayac = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in sira)
        {
            var ad = tablolar[id].HamAd;
            var harf = ad.FirstOrDefault(char.IsLetter);
            var kok = char.ToLowerInvariant(harf == default ? 't' : harf).ToString();
            var n = sayac.TryGetValue(kok, out var c) ? c : 0;
            sayac[kok] = n + 1;
            result[id] = n == 0 ? kok : kok + n;
        }
        return result;
    }

    /// <summary>Elle koşul metnini yürür: tek-tırnak string LİTERALLERİ olduğu gibi kopyalanır
    /// ('' kaçışı desteklenir — içlerindeki {X.Y} veri sayılır), literal DIŞINDAKİ her
    /// <c>{...}</c> yer tutucusu <paramref name="coz"/> ile değiştirilir (Y3).</summary>
    private static string ResolveRawCondition(string metin, Func<string, string> coz)
        => ResolveDelimited(metin, '{', '}', coz);

    /// <summary>Y2 — çıkış kolonu atıfları: literal DIŞINDAKİ her <c>[Ad]</c>
    /// <paramref name="coz"/> ile değiştirilir (SELECT'teki takma ada atıf).</summary>
    private static string ResolveOutputRefs(string metin, Func<string, string> coz)
        => ResolveDelimited(metin, '[', ']', coz);

    private static string ResolveDelimited(string metin, char ac, char kapa, Func<string, string> coz)
    {
        var sb = new StringBuilder(metin.Length + 32);
        var i = 0;
        while (i < metin.Length)
        {
            var c = metin[i];
            if (c == '\'')
            {
                var j = i + 1;
                while (j < metin.Length)
                {
                    if (metin[j] == '\'')
                    {
                        if (j + 1 < metin.Length && metin[j + 1] == '\'') { j += 2; continue; } // '' kaçışı
                        j++;
                        break;
                    }
                    j++;
                }
                sb.Append(metin, i, j - i);
                i = j;
            }
            else if (c == ac)
            {
                var son = metin.IndexOf(kapa, i + 1);
                if (son < 0) { sb.Append(metin, i, metin.Length - i); break; }
                sb.Append(coz(metin.Substring(i + 1, son - i - 1)));
                i = son + 1;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }

    // --- render yardımcıları ---

    /// <summary>QUOTENAME karşılığı: köşeli parantez + <c>]</c> kaçışı. Kimlik enjeksiyonunu kapatır.</summary>
    private static string Q(string ad) => "[" + ad.Replace("]", "]]") + "]";

    private static string TableSql(DataMapTableDto t) =>
        Q(string.IsNullOrWhiteSpace(t.Sema) ? "dbo" : t.Sema) + "." + Q(t.HamAd);

    private static string AggregationSql(ToplamaTuru t, string kolIfade) => t switch
    {
        ToplamaTuru.Yok => kolIfade,
        ToplamaTuru.Topla => "SUM(" + kolIfade + ")",
        ToplamaTuru.Ortalama => "AVG(" + kolIfade + ")",
        ToplamaTuru.EnAz => "MIN(" + kolIfade + ")",
        ToplamaTuru.EnCok => "MAX(" + kolIfade + ")",
        ToplamaTuru.Say => "COUNT(" + kolIfade + ")",
        ToplamaTuru.BenzersizSay => "COUNT(DISTINCT " + kolIfade + ")",
        // Aralık-dışı enum (JSON'da sayısal token) sessizce ham kolona düşüp GROUP BY dışında
        // kalır → geçersiz SQL. Açıkça reddet (bekçinin yakalayamayacağı sözdizimsel-geçerli hata).
        _ => throw new QueryModelException("Bilinmeyen toplama türü."),
    };

    private static readonly Dictionary<ToplamaTuru, string> ToplamaOnek = new()
    {
        [ToplamaTuru.Topla] = "Toplam", [ToplamaTuru.Ortalama] = "Ortalama", [ToplamaTuru.EnAz] = "EnAz",
        [ToplamaTuru.EnCok] = "EnCok", [ToplamaTuru.Say] = "Sayi", [ToplamaTuru.BenzersizSay] = "BenzersizSayi",
    };

    private static string DefaultAlias(SelectedField a) =>
        (a.Toplama == ToplamaTuru.Yok ? a.Kolon : ToplamaOnek[a.Toplama] + a.Kolon)
        + (a.Kova is TarihKovasi kv ? "_" + KovaSonEk[kv] : "");

    /// <summary>Y4 — tarih kovası SQL'i. Dönem BAŞLANGICI ailesinde <c>DATEADD(x, DATEDIFF(x, 0, k), 0)</c>
    /// deseni kullanılır (0 = 1900-01-01; dönemin ilk günü). Parça ailesi (AyNo/CeyrekNo/SaatNo)
    /// yıldan bağımsız sayı döner — mevsimsellik/saatlik desen için. Hepsi deterministiktir ve
    /// yalnız SELECT/GROUP BY'da kullanılır (WHERE'e girmediği için indeks aramasını bozmaz).
    /// <para><b>Hafta bilerek farklı üretilir:</b> yaygın <c>DATEADD(week, DATEDIFF(week, 0, k), 0)</c>
    /// deseni PAZAR gününü BİR SONRAKİ pazartesiye atar (gerçek SQL Server'da doğrulandı:
    /// 2026-08-16 Pazar → 2026-08-17) — yani hafta pazar başlar ama pazartesi etiketiyle görünür ve
    /// kayan tarih dilbilgisindeki <c>$haftaBasi</c> (pazartesi) ile ÇELİŞİR. Gün-modülo deseni
    /// (<c>DATEDIFF(day, 0, k) % 7</c>; 0 = pazartesi) haftayı gerçekten pazartesiye çeker ve
    /// oturumun DATEFIRST ayarından bağımsızdır.</para></summary>
    private static string KovaSql(TarihKovasi kova, string kolIfade) => kova switch
    {
        TarihKovasi.Yil => "DATEADD(year, DATEDIFF(year, 0, " + kolIfade + "), 0)",
        TarihKovasi.Ceyrek => "DATEADD(quarter, DATEDIFF(quarter, 0, " + kolIfade + "), 0)",
        TarihKovasi.Ay => "DATEADD(month, DATEDIFF(month, 0, " + kolIfade + "), 0)",
        TarihKovasi.Hafta =>
            "DATEADD(day, -(DATEDIFF(day, 0, " + kolIfade + ") % 7), CAST(" + kolIfade + " AS date))",
        TarihKovasi.Gun => "CAST(" + kolIfade + " AS date)",
        TarihKovasi.YilNo => "YEAR(" + kolIfade + ")",
        TarihKovasi.CeyrekNo => "DATEPART(quarter, " + kolIfade + ")",
        TarihKovasi.AyNo => "MONTH(" + kolIfade + ")",
        TarihKovasi.SaatNo => "DATEPART(hour, " + kolIfade + ")",
        _ => throw new QueryModelException("Bilinmeyen tarih kovası."),
    };

    private static readonly Dictionary<TarihKovasi, string> KovaSonEk = new()
    {
        [TarihKovasi.Yil] = "Yil", [TarihKovasi.Ceyrek] = "Ceyrek", [TarihKovasi.Ay] = "Ay",
        [TarihKovasi.Hafta] = "Hafta", [TarihKovasi.Gun] = "Gun",
        [TarihKovasi.YilNo] = "YilNo", [TarihKovasi.CeyrekNo] = "CeyrekNo",
        [TarihKovasi.AyNo] = "AyNo", [TarihKovasi.SaatNo] = "SaatNo",
    };

    /// <summary>Y6 — pencere kolonu SQL'i. <paramref name="coz"/> formül metnini
    /// ({Tablo.Kolon} / [Çıkış Kolonu]) SQL'e çevirir (Y1/Y2 ile aynı makine).
    /// Kurallar açık mesajla uygulanır: sıra gerektiren işlevde sıra zorunlu, değer gerektiren
    /// işlevde değer zorunlu, sayı pozitif. <c>BolumToplami</c> bilerek SIRASIZDIR: sıra eklenirse
    /// SQL Server onu kümülatif toplama çevirir ve "paya oran" sessizce yanlış çıkardı.</summary>
    private static string PencereSql(WindowColumn p, string nerede, Func<string, string> coz)
    {
        string Deger()
        {
            if (string.IsNullOrWhiteSpace(p.Deger))
                throw new QueryModelException($"{nerede}: bu işlev için bir DEĞER kolonu/formülü seçin.");
            return coz(p.Deger);
        }
        int Sayi(int varsayilan, string ad)
        {
            var n = p.Sayi ?? varsayilan;
            if (n <= 0) throw new QueryModelException($"{nerede}: {ad} 0'dan büyük olmalı.");
            return n;
        }
        var bolum = (p.Bolum ?? []).Where(b => !string.IsNullOrWhiteSpace(b)).Select(coz).ToList();
        var sira = (p.Sira ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Ifade))
            .Select(s => coz(s.Ifade) + (s.Azalan ? " DESC" : " ASC")).ToList();

        var siraGerekli = p.Islev is PencereIslevi.SiraNo or PencereIslevi.Sira or PencereIslevi.YogunSira
            or PencereIslevi.Dilim or PencereIslevi.Onceki or PencereIslevi.Sonraki
            or PencereIslevi.KosanToplam or PencereIslevi.HareketliOrtalama;
        if (siraGerekli && sira.Count == 0)
            throw new QueryModelException(
                $"{nerede}: bir SIRA seçin — bu işlev 'neye göre' sorusunun cevabı olmadan hesaplanamaz (ör. tarihe göre).");

        var over = (bolum.Count > 0 ? "PARTITION BY " + string.Join(", ", bolum) : "")
            + (bolum.Count > 0 && sira.Count > 0 ? " " : "")
            + (sira.Count > 0 && p.Islev != PencereIslevi.BolumToplami ? "ORDER BY " + string.Join(", ", sira) : "");

        return p.Islev switch
        {
            PencereIslevi.SiraNo => "ROW_NUMBER() OVER (" + over + ")",
            PencereIslevi.Sira => "RANK() OVER (" + over + ")",
            PencereIslevi.YogunSira => "DENSE_RANK() OVER (" + over + ")",
            // NTILE/LAG/LEAD sayıları SABİTtir (parametre kabul etmez); int olarak doğrulandığı için
            // metne gömülmesi enjeksiyon yüzeyi açmaz.
            PencereIslevi.Dilim => "NTILE(" + Sayi(4, "dilim sayısı") + ") OVER (" + over + ")",
            PencereIslevi.Onceki => "LAG(" + Deger() + ", " + Sayi(1, "kaydırma") + ") OVER (" + over + ")",
            PencereIslevi.Sonraki => "LEAD(" + Deger() + ", " + Sayi(1, "kaydırma") + ") OVER (" + over + ")",
            PencereIslevi.KosanToplam =>
                "SUM(" + Deger() + ") OVER (" + over + " ROWS UNBOUNDED PRECEDING)",
            PencereIslevi.BolumToplami =>
                "SUM(" + Deger() + ") OVER (" + (bolum.Count > 0 ? "PARTITION BY " + string.Join(", ", bolum) : "") + ")",
            PencereIslevi.HareketliOrtalama =>
                "AVG(" + Deger() + ") OVER (" + over + " ROWS BETWEEN " + (Sayi(3, "pencere boyu") - 1)
                + " PRECEDING AND CURRENT ROW)",
            _ => throw new QueryModelException($"{nerede}: bilinmeyen pencere işlevi."),
        };
    }

    /// <summary>Kova yalnız TARİH tipli kolonlarda anlamlıdır — metin/sayı kolonunda SQL Server
    /// örtük çevrim denemesiyle çalışma-anı hatası verirdi; erken ve anlaşılır reddedilir.</summary>
    private static bool TarihTipiMi(string? veriTipi) =>
        (veriTipi ?? "").ToLowerInvariant() is "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset";

    private static string UniqueAlias(string istenen, HashSet<string> kullanilan)
    {
        var ad = istenen;
        var n = 2;
        while (!kullanilan.Add(ad)) ad = istenen + "_" + n++;
        return ad;
    }
}
