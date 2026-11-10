using System.Text.Json;
using Kds.Business.DataMaps;
using Kds.Business.Identity;
using Kds.Business.Security;
using Kds.Execution;
using Kds.Contracts.Identity;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using MediatR;

namespace Kds.Business.Queries;

/// <summary>Tasarımcının "Çalıştır"ı: modeli parametreli SQL'e çevirir, sonra haritanın bağlantısında
/// <see cref="RunQueryRequest"/> ile koşar (tek kapı bekçi + audit oradan gelir). Model değerleri
/// JSON'dan <see cref="JsonElement"/> olarak gelir; SqlParameter'a bağlanabilmeleri için CLR tipine
/// çözülür.</summary>
public sealed class RunQueryModelHandler(
    IDataMapReader haritalar, ISender gonderici, IQueryGenerator uretici,
    ISatirKuraliCozucu satirKuralCozucu, IUserContext kullanici, Metrics.IMetrikCozucu metrikCozucu)
    : IRequestHandler<RunQueryModelRequest, QueryRunResult>
{
    public async Task<QueryRunResult> Handle(RunQueryModelRequest request, CancellationToken cancellationToken)
    {
        var dataMap = await haritalar.GetDetailAsync(request.Model.HaritaId, cancellationToken)
            ?? throw new KeyNotFoundException("Harita bulunamadı.");

        // S22: metrik REFERANSLARI katalogdan formüle çözülür — tek noktada, HER koşum yolu için
        // (tasarımcı, rapor, pano, abonelik, uyarı). Üretici metrik kavramını hiç bilmez.
        var model = await metrikCozucu.CozAsync(request.Model, cancellationToken);

        // RLS — HER model koşumu buradan geçer (tasarımcı "Çalıştır", rapor, pano widget'ı,
        // abonelik e-postası). Kuralı tek noktada uygulamak, yarın eklenecek bir koşum yolunun
        // yanlışlıkla filtresiz kalmasını önler.
        // Kimlik: normal koşumda isteğin kullanıcısı; zamanlanmış işlerde işin SAHİBİNİN anlık
        // görüntüsü (KosanKimlik). Arka planda istek bağlamı boştur — override olmasaydı
        // rol-kapsamlı kurallar sessizce düşer ve kısıtlı satırlar e-postayla dışarı çıkardı.
        var kosan = request.KosanKimlik
            ?? new CalistiranKullanici(kullanici.Roller, kullanici.KullaniciAdi, kullanici.EPosta, kullanici.Iddialar);
        var kurallar = await satirKuralCozucu.CozAsync(model.HaritaId, kosan, cancellationToken);

        var uretilen = uretici.Generate(model, dataMap, request.ParametreDegerleri, kurallar);
        var parametreler = uretilen.Parametreler.ToDictionary(p => p.Key, p => JsonDegerCozucu.Coz(p.Value));

        // Kod incelemesi Y1: model SatirLimiti yürütücüye de geçirilir — SQL TOP(@satir)=5000
        // üretilmişken okumanın varsayılan 1000'de kesilmesi tutarsızlığı kapanır (10.000 sert
        // tavan yürütücüde yine geçerli).
        var sonuc = await gonderici.Send(
            new RunQueryRequest(dataMap.BaglantiId, uretilen.Sql,
                SatirSiniri: model.SatirLimiti, Parametreler: parametreler,
                RaporId: request.RaporId),
            cancellationToken);

        /* S40: üreticinin YOLA KATTIĞI tablolar sonuca iliştirilir. Çalıştırıcı yalnız SQL görür,
         * hangi tabloların kullanıcı istediği için değil de birleştirme uğruna girdiğini bilemez;
         * bu bilgi tek burada, model ile üretilen SQL'in buluştuğu yerde vardır. */
        return uretilen.AraTablolar is { Count: > 0 }
            ? sonuc with { AraTablolar = uretilen.AraTablolar }
            : sonuc;
    }

    // JsonElement→CLR çevirisi paylaşılan JsonDegerCozucu'ya taşındı (M11: ham SQL yolu da kullanır).
}
