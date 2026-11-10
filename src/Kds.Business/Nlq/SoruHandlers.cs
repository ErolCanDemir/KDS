using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Kds.Business.DataMaps;
using Kds.Business.Reports;
using Kds.Contracts.Nlq;
using Kds.Contracts.QueryModel;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Nlq;

/// <summary>Soru Sor (S23): Türkçe soru → istem → yerel model → kurtarma → NORMAL koşum hattı.
///
/// <para>Güven zinciri: AI'nın ürettiği şey SQL değil, elle kurulmuş sorgudan FARKSIZ bir JSON
/// modelidir ve <see cref="RunQueryModelRequest"/> ile koşulur — yani metrik çözümü, RLS, satır
/// sınırı, SELECT bekçisi ve denetim AYNEN uygulanır. AI için ayrı bir koşum yolu YOKTUR; bu,
/// Ölç/abonelik/uyarı ile aynı mimari karardır.</para></summary>
public sealed class AskQuestionHandler(
    IDataMapReader haritalar, KdsDbContext context, ISoruCevirici cevirici, ISender gonderici)
    : IRequestHandler<AskQuestionRequest, AskQuestionResult>
{
    public async Task<AskQuestionResult> Handle(AskQuestionRequest request, CancellationToken cancellationToken)
    {
        var harita = await haritalar.GetDetailAsync(request.HaritaId, cancellationToken)
            ?? throw new KeyNotFoundException("Harita bulunamadı.");

        // Katalog = BU haritanın aktif metrikleri (tenant süzgeci DbContext'te). Modele yalnız
        // ad+açıklama gider; formül gitmez — modelin formüle ihtiyacı yok, referans yetiyor.
        var katalog = await context.Metrikler.AsNoTracking()
            .Where(m => m.VeriHaritasiId == request.HaritaId && m.Aktif)
            .OrderBy(m => m.Ad)
            .ToListAsync(cancellationToken);

        // Düzeltme turu: önceki modeli MODELİN KENDİ ara biçiminde göster (S30c — gerekçe
        // ModelKurtarici.AraJsonaCevir'de: iç modeli göstermek çıktıyı 3.7 katına çıkarıyordu).
        var onceki = OncekiModel(request.OncekiModelJson);
        var ozet = onceki is null ? null : ModelKurtarici.AraJsonaCevir(onceki, harita, katalog);
        var istem = SoruIstemleri.Kur(request.Soru, harita, katalog, ozet,
            filtreGizli: onceki is not null && ModelKurtarici.FiltreTemsilEdilemez(onceki));

        var kronometre = Stopwatch.StartNew();
        var ham = await cevirici.CevirAsync(istem, cancellationToken);
        kronometre.Stop();

        var kurtarilan = ModelKurtarici.Kurtar(ham, harita, katalog);
        var (model, notlar) = DuzeltmeKorumasi(kurtarilan.Model, kurtarilan.NasilHesaplandi, onceki);
        var sonuc = await gonderici.Send(new RunQueryModelRequest(model), cancellationToken);

        /* S40 — JOIN YOLU GÖRÜNÜR OLSUN. Üretici, sorgudaki tabloları haritadaki ilişkilerden
         * kendisi birleştirir; bir tabloya birden çok eşit uzunlukta yol varsa birini seçer.
         * Ölçülen vaka: "AUDİ marka araçların tutarı" sorusunda Markalar'a Araclar.MarkaId yerine
         * Servisler.AnaBayiMarkaId üzerinden bağlanıldı — yani ARACIN markası değil SERVİSİN BAYİ
         * MARKASI süzüldü ve hiçbir hata çıkmadı. Yol burada değiştirilmez (doğrusu sorunun
         * anlamına bağlıdır); kullanıcının seçmediği hâlde yola giren tablolar söylenir ki rakam
         * beklenenden farklıysa nereye bakacağını bilsin. */
        if (sonuc.AraTablolar is { Count: > 0 } ara)
        {
            notlar = [.. notlar, $"Bağlantı için {string.Join(", ", ara)} tablosu üzerinden geçildi — " +
                "sonuç beklediğiniz değilse soruda hangi tablodan gidileceğini belirtin."];
        }

        return new AskQuestionResult(
            sonuc,
            model,
            notlar,
            GidenIstem: istem,
            ModelCiktisi: ham.Trim(),
            CeviriSureMs: (int)kronometre.Elapsed.TotalMilliseconds,
            OneriGorsellestirme: kurtarilan.Gorsellestirme,
            Durum: kurtarilan.Durum);
    }

    /// <summary>Düzeltme kipinin deterministik güvencesi. Canlı gözlem (23 Ağu 2026): 7b model
    /// "ilk 2yi göster" düzeltmesinde limit'i uygulamak yerine SİLDİ, sıralamayı da düşürdü —
    /// istem kuralı tek başına yetmiyor. Kural: düzeltme cevabında limit/sıralama BOŞ geldiyse
    /// önceki modelden korunur ve bu, "nasıl hesaplandı"ya açıkça yazılır. Bilinçli bedel:
    /// "limiti kaldır" gibi bir istek bu korumaya takılır — sessizce bozulan sonuca göre küçük
    /// ve şeffaf bir bedel (not satırı nedenini söyler).</summary>
    private static (QueryModel Model, IReadOnlyList<string> Notlar) DuzeltmeKorumasi(
        QueryModel model, IReadOnlyList<string> notlar, QueryModel? onceki)
    {
        if (onceki is null)
        {
            return (model, notlar);
        }

        var ekNotlar = new List<string>(notlar);
        if (model.SatirLimiti is null && onceki.SatirLimiti is not null)
        {
            model = model with { SatirLimiti = onceki.SatirLimiti };
            ekNotlar.Add($"Düzeltmede limit korundu: ilk {onceki.SatirLimiti} satır (önceki modelden).");
        }

        if (model.Siralama is not { Count: > 0 } && onceki.Siralama is { Count: > 0 })
        {
            model = model with { Siralama = onceki.Siralama };
            ekNotlar.Add("Düzeltmede sıralama önceki modelden korundu.");
        }

        /* Ara biçimde GÖSTERİLEMEYEN filtre (VEYA/iç içe) modelden geri gelemez — istem de
         * "boş bırak" dedi; burada eski filtre aynen taşınır. Aksi hâlde düzeltme, kullanıcının
         * kurduğu filtreyi SESSİZCE düşürürdü (yanlış rakam üretmenin en sinsi yolu). */
        if (model.Filtre is null && onceki.Filtre is not null && ModelKurtarici.FiltreTemsilEdilemez(onceki))
        {
            model = model with { Filtre = onceki.Filtre };
            ekNotlar.Add("Düzeltmede mevcut filtreler korundu (karmaşık filtre modele gösterilmez).");
        }

        return (model, ekNotlar);
    }

    /// <summary>Ekrandan gelen önceki model — dar tipli parse (AGENTS §4.6 izinli bölge);
    /// çözülemezse düzeltme kipi devre dışı kalır, soru normal yoldan koşar.</summary>
    private static QueryModel? OncekiModel(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try { return JsonSerializer.Deserialize<QueryModel>(json, ReportCommon.JsonAyar); }
        catch (JsonException) { return null; }
    }
}

public sealed class NlqStatusHandler(ISoruCevirici cevirici)
    : IRequestHandler<NlqStatusRequest, NlqStatusResult>
{
    public Task<NlqStatusResult> Handle(NlqStatusRequest request, CancellationToken cancellationToken) =>
        cevirici.DurumAsync(cancellationToken);
}

/// <summary>S34 — ISITMA. Soru sormakla AYNI öneki kurar (<see cref="SoruIstemleri.OnekKur"/>) ve
/// modele işletir; tek fark cevap beklenmemesidir. Önek burada yeniden yazılmaz — tek kaynaktan
/// gelir, çünkü bir karakterlik fark bile önbelleği ıskalatır ve kazanç sıfırlanır.</summary>
public sealed class NlqIsitHandler(IDataMapReader haritalar, KdsDbContext context, ISoruCevirici cevirici)
    : IRequestHandler<NlqIsitRequest, NlqIsitResult>
{
    public async Task<NlqIsitResult> Handle(NlqIsitRequest request, CancellationToken cancellationToken)
    {
        var harita = await haritalar.GetDetailAsync(request.HaritaId, cancellationToken);
        if (harita is null)
        {
            // Isıtma bir iyileştirmedir: harita yoksa hata fırlatmak yerine "ısıtılmadı" denir.
            return new NlqIsitResult(false, 0);
        }

        var katalog = await context.Metrikler.AsNoTracking()
            .Where(m => m.VeriHaritasiId == request.HaritaId && m.Aktif)
            .OrderBy(m => m.Ad)
            .ToListAsync(cancellationToken);

        var onek = SoruIstemleri.OnekKur(harita, katalog);
        await cevirici.IsitAsync(onek, cancellationToken);
        return new NlqIsitResult(true, onek.Length);
    }
}

/// <summary>Biçimsel doğrulama — içerik doğrulaması kurtarıcıda (harita/katalog gerektirir).</summary>
public sealed class AskQuestionValidation : AbstractValidator<AskQuestionRequest>
{
    public AskQuestionValidation()
    {
        RuleFor(x => x.HaritaId).NotEmpty();
        RuleFor(x => x.Soru).NotEmpty().MaximumLength(500)
            .WithMessage("Soru boş olamaz ve 500 karakteri aşamaz.");
    }
}
