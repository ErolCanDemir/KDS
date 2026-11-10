using System.Diagnostics;
using Kds.Contracts.Logging;

namespace Kds.Api.Identity;

/// <summary>Her <c>/api</c> isteğini bir <see cref="ActionEvent"/> olarak olay günlüğüne yazar
/// (ActionId, yol, metot, durum, süre, kullanıcı/tenant/ip). ActionId isteğin başında üretilir
/// (<see cref="IActionContext"/>) → aynı istekte üretilen DataChange/Exception olayları aynı Id'yi
/// taşır. Middleware §4.6 izinli bölgedir; yazım bloklamaz (arka plan kanalı).</summary>
public sealed class ActionLogMiddleware(RequestDelegate sonraki)
{
    public async Task InvokeAsync(HttpContext context, IEventLog gunluk, IActionContext eylem)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await sonraki(context);
            return;
        }

        var kronometre = Stopwatch.StartNew();
        var actionId = eylem.ActionId; // erken üret → interceptor/exception aynı Id'yi görsün
        try
        {
            await sonraki(context);
        }
        finally
        {
            kronometre.Stop();
            gunluk.Write(new ActionEvent
            {
                Id = actionId,
                Timestamp = DateTime.UtcNow,
                TenantId = eylem.TenantId,
                UserId = eylem.UserId,
                UserName = eylem.UserName,
                Action = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value,
                Path = context.Request.Path.Value,
                Method = context.Request.Method,
                Status = context.Response.StatusCode,
                DurationMs = kronometre.ElapsedMilliseconds,
                Ip = eylem.Ip,
            });
        }
    }
}
