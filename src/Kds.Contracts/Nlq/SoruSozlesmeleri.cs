using Kds.Common.Cqrs;
using Kds.Contracts.Queries;

namespace Kds.Contracts.Nlq;

/// <summary>Soru Sor (S23 · NLQ): Türkçe soru → JSON sorgu modeli → NORMAL koşum hattı.
/// Model asla SQL üretmez; ürettiği şey elle kurulmuş sorgudan farksız bir <see cref="QueryModel"/>'dir
/// ve üretici + SELECT bekçisi + RLS + satır sınırı + denetim aynen uygulanır.</summary>
/// <summary>S30: <paramref name="OncekiModelJson"/> doluysa ÇOK TURLU DÜZELTME — model sıfırdan
/// kurmaz, mevcut modeli kullanıcının isteğine göre günceller.</summary>
public sealed record AskQuestionRequest(Guid HaritaId, string Soru, string? OncekiModelJson = null) : ICommand<AskQuestionResult>;

/// <summary>Cevap + ŞEFFAFLIK: ekran "nasıl hesaplandı"yı ve "modele gideni" bu alanlardan gösterir.
/// <para><see cref="GidenIstem"/> — SQLST'nin Show Prompt kuralı: kullanıcının gördüğü metin,
/// modele gidenin TA KENDİSİDİR (gölge kopya yok; güven bunu gerektirir).</para></summary>
public sealed record AskQuestionResult(
    QueryRunResult Sonuc,
    /// <summary>AI'nın kurduğu model — "Rapor olarak kaydet" bunu SaveReport'a taşır; kullanıcı
    /// tasarımcıda açıp düzeltebilir (AI kara kutu değil, sorgu kuran yardımcı).</summary>
    QueryModel.QueryModel Model,
    /// <summary>İnsan dili özet: kullanılan metrik(ler), kırılım, filtre, sıralama, limit.</summary>
    IReadOnlyList<string> NasilHesaplandi,
    string GidenIstem,
    /// <summary>Modelin ham çıktısı (ayıklanmış JSON) — teşhis ve güven için.</summary>
    string ModelCiktisi,
    int CeviriSureMs,
    /// <summary>S30: modelin (veya deterministik tahminin) önerdiği görünüm — Tablo|Cubuk|Cizgi|Pasta|SayiKarti.</summary>
    string OneriGorsellestirme = "Tablo",
    /// <summary>S31: sorgunun YAPILANDIRILMIŞ hâli — ekrandaki "durum rayı" ve atıf satırı bunu basar.
    /// <see cref="NasilHesaplandi"/> ile aynı bilgiyi taşır ama metin ayrıştırmaya zorlamaz.</summary>
    SorguDurumu? Durum = null);

/// <summary>S31 — cevabın dayandığı şeyin tek bakışta görünür hâli. Ekranda iki yere basılır:
/// (a) <b>durum rayı</b> (çipler; kullanıcı hangi kırılım/limit/sıralamanın açık olduğunu hatırlamak
/// zorunda kalmasın), (b) <b>atıf satırı</b> (hangi tablo/metrik, ne zaman doğrulanmış).
///
/// <para>Gerekçe (23 Ağu 2026 tasarım araştırması): sohbet arayüzlerinde aktif filtre/limit mesajlara
/// dağılınca kullanıcı "unutulmuş filtre altında" yanlış çıkarım yapıyor; çalışma belleği ~4 parça.
/// Durumu sürekli görünür kılmak bu hatanın doğrudan panzehiri.</para></summary>
public sealed record SorguDurumu(
    string HaritaAd,
    /// <summary>Sorguda geçen tabloların dostça adları.</summary>
    IReadOnlyList<string> Tablolar,
    IReadOnlyList<MetrikAtfi> Metrikler,
    IReadOnlyList<DurumCipi> Cipler);

/// <summary>Kullanılan metrik + tanımının son değişme tarihi. Tarih bilerek gösterilir: metrik tanımı
/// kayınca cevaplar sessizce bayatlar; kullanıcı "bu tanım ne zamandır böyle?" diye sorabilmeli.</summary>
public sealed record MetrikAtfi(Guid Id, string Ad, DateTimeOffset? SonDegisiklik);

/// <summary><paramref name="Tur"/>: metrik | kirilim | hesap | filtre | siralama | limit — ekran çipi
/// buna göre renklendirir. <paramref name="Kaldirilabilir"/> false ise çip yapının kendisidir
/// (ör. kırılımsız sorguda ölçü) ve tek tıkla atılamaz.</summary>
public sealed record DurumCipi(string Tur, string Etiket, bool Kaldirilabilir = true);

/// <summary>Sağlayıcı erişilebilirlik denetimi — ekran açılışta "Yerel model hazır / ulaşılamadı"
/// rozetini bununla doldurur (SQLST dayanıklılık dersi: AI yoksa ürün çökmez, yol gösterir).</summary>
public sealed record NlqStatusRequest : IQuery<NlqStatusResult>;

public sealed record NlqStatusResult(bool Hazir, string? Model, string? Sebep);

/// <summary>S34 — ISITMA: Soru Sor ekranı açılıp harita seçildiğinde çağrılır. İstemin değişmeyen
/// önekini modele önceden işletir; kullanıcı sorusunu yazarken önek önbelleğe girer, böylece İLK
/// soru da "ikinci soru" hızında cevaplanır (ölçüm 24 Ağu 2026: önek işleme 49.7 sn → 0.2 sn).
/// Ateşle-unut — hata kullanıcıya yansıtılmaz.</summary>
public sealed record NlqIsitRequest(Guid HaritaId) : ICommand<NlqIsitResult>;

/// <param name="Isitildi">Isıtma denendi mi (harita yoksa false).</param>
/// <param name="OnekKarakter">Gönderilen önekin uzunluğu — teşhis içindir, ekranda gösterilmez.</param>
public sealed record NlqIsitResult(bool Isitildi, int OnekKarakter);
