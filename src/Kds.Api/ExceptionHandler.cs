using FluentValidation;
using Kds.Contracts.Logging;
using Kds.Execution;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kds.Api;

/// <summary>Alan (domain) istisnalarını temiz <see cref="ProblemDetails"/>'e çevirir: model/dataMap
/// tutarsızlığı (<see cref="QueryModelException"/>) ve doğrulama (<see cref="ValidationException"/>,
/// MediatR pipeline'ındaki ValidationBehavior fırlatır) → 400; bulunamayan kaynak
/// (<see cref="KeyNotFoundException"/>) → 404. Tanımadıklarını bırakır (framework 500 üretir).
/// Böylece handler'larda elle try/catch gerekmez (AGENTS §4.6 dışı ihlal önlenir).</summary>
internal sealed class ExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception hata, CancellationToken cancellationToken)
    {
        var (kod, baslik, detay) = hata switch
        {
            // S16/A1: uygulama henüz kurulmamış (yönetim veritabanı yok) — önyüz bunu görünce
            // kurulum sihirbazına yönlendirir. 503: geçici, yapılandırma bekleniyor.
            Kds.Data.KurulumGerekliException => (StatusCodes.Status503ServiceUnavailable,
                "Kurulum gerekli", hata.Message),
            QueryModelException => (StatusCodes.Status400BadRequest, "Geçersiz sorgu modeli", hata.Message),
            // SELECT-only bekçisinin reddi (S5/S9): icra hiç başlamadı; sebep insana okunur.
            QuerySecurityException => (StatusCodes.Status400BadRequest, "Sorgu reddedildi", hata.Message),
            // RLS: satır kuralı uygulanamadı (iddia eksik) ya da kurala tabi kullanıcı ham SQL
            // denedi. 403: yetki meselesi; sorgu ÇALIŞTIRILMADI ve mesaj yol gösterir.
            Kds.Business.Security.SatirKuraliHatasi => (StatusCodes.Status403Forbidden,
                "Satır-düzeyi güvenlik", hata.Message),
            ValidationException dv => (StatusCodes.Status400BadRequest, "Doğrulama hatası",
                string.Join(" · ", dv.Errors.Select(e => e.ErrorMessage).DefaultIfEmpty(dv.Message))),
            // Zaman aşımı (test turu M7): "çalışmadı"nın en yaygın nedeni. Büyük tablolarda İLK
            // çalıştırma (soğuk önbellek) sonrakilerden kat kat uzun sürebilir — kullanıcı
            // "kaydedince çalıştı" sanıyordu; fark yalnız ısınmış önbellekti. Mesaj eyleme dönük;
            // şema sızdırmaz (musteri de görebilir).
            Microsoft.Data.SqlClient.SqlException { Number: -2 } => (StatusCodes.Status400BadRequest,
                "Sorgu zaman aşımına uğradı",
                "Sorgu izin verilen sürede (60 sn) tamamlanamadı. Büyük tablolarda İLK çalıştırma " +
                "(soğuk önbellek) sonrakilerden çok daha uzun sürebilir — bir kez daha deneyin. " +
                "Sürüyorsa: filtre ekleyin, satır limitini düşürün ya da sıralamayı kaldırın."),
            // İcra sırasında hedef DB'nin verdiği hata (tip dönüşümü, eksik nesne vb.) → 400.
            // Ayrıntılı DB mesajı yalnız TASARIM yetkililerine (Admin/Geliştirici) gösterilir;
            // musteri rapor koşarken iç şema/sunucu bilgisi SIZMAZ (kod incelemesi O6) — denetim
            // kaydında tam mesaj zaten var.
            Microsoft.Data.SqlClient.SqlException sql => (StatusCodes.Status400BadRequest,
                "Sorgu çalıştırılamadı",
                ctx.User.IsInRole(Kds.Contracts.Identity.Roles.Admin) || ctx.User.IsInRole(Kds.Contracts.Identity.Roles.Gelistirici)
                    ? sql.Message
                    : "Report veri kaynağında hata verdi. Ayrıntı için rapor sahibine ya da yöneticinize başvurun."),
            // E-posta gönderimi (abonelik/uyarı): SMTP hataları kimlik/port/TLS gibi YAPILANDIRMA
            // sorunlarıdır ve çözümü yöneticidedir. Mesaj zaten "ne yapmalı"yı söyler + sunucunun
            // ham yanıtını taşır; genel 500 yerine burada döner ki ekranda okunabilsin.
            Kds.Business.Subscriptions.EpostaGonderimHatasi => (StatusCodes.Status400BadRequest,
                "E-posta gönderilemedi", hata.Message),
            // Soru Sor (S23): yerel model ortam sorunu (Ollama kapalı / model inmemiş / süre doldu)
            // → 503: geçici, kurulum bekleniyor; mesaj zaten "ne yapmalı"yı söyler.
            Kds.Business.Nlq.SoruCeviriHatasi => (StatusCodes.Status503ServiceUnavailable,
                "Yerel modele ulaşılamadı", hata.Message),
            // Roles & Yetkiler (S27): Keycloak Admin API sorunu (yapılandırma/izin/erişim) → 503;
            // iş-kuralı redleri de (sistem rolü silinemez, üyesi olan rol) aynı tipten gelir ve
            // mesaj kullanıcıya aynen gösterilir.
            Kds.Business.Identity.RolYonetimHatasi => (StatusCodes.Status503ServiceUnavailable,
                "Rol servisi", hata.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Bulunamadı", hata.Message),
            _ => (0, string.Empty, string.Empty),
        };

        // Her istisnayı olay günlüğüne yaz (bilinen 400/404 + bilinmeyen 500), ait olduğu ActionId ile.
        // Bu handler singleton olduğundan scoped bağımlılıklar istek servislerinden çözülür.
        var durum = kod == 0 ? StatusCodes.Status500InternalServerError : kod;
        var gunluk = ctx.RequestServices.GetRequiredService<IEventLog>();
        var eylem = ctx.RequestServices.GetRequiredService<IActionContext>();
        gunluk.Write(new ExceptionEvent
        {
            ActionId = eylem.ActionId,
            Timestamp = DateTime.UtcNow,
            Type = hata.GetType().FullName,
            Message = hata.Message,
            StackTrace = hata.StackTrace,
            Path = ctx.Request.Path.Value,
            Method = ctx.Request.Method,
            Status = durum,
            TenantId = eylem.TenantId,
            UserId = eylem.UserId,
        });

        if (kod == 0) return false; // bu istisna bize ait değil → framework ele alsın (500)

        ctx.Response.StatusCode = kod;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = kod, Title = baslik, Detail = detay }, cancellationToken);
        return true;
    }
}
