using System.Text.Encodings.Web;
using System.Text.Json;
using Kds.Contracts.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kds.Data;

/// <summary>Her SaveChanges'te Added/Modified/Deleted varlıkları <see cref="DataChangeEvent"/> olarak
/// olay günlüğüne yazar (eski→yeni + ait olduğu aksiyonun <c>ActionId</c>'si). Handler'lar bunu bilmez
/// (§4.6 — merkezi, tek nokta). Entity Id'leri client üretimli (<c>EntityBase.Id = Guid.NewGuid()</c>)
/// olduğundan birincil anahtar SaveChanges ÖNCESİ bellidir → SavingChanges'te güvenle yakalanır.
/// Yazım BLOKLAMAZ (arka plan kanalı); şifre alanları maskelenir; SQL denetim kaydı hariç tutulur.</summary>
public sealed class DataChangeInterceptor(IEventLog gunluk, IActionContext eylem) : SaveChangesInterceptor
{
    // Log'da Türkçe/Unicode kaçırılmadan okunur dursun (Ü değil "Ü").
    private static readonly JsonSerializerOptions JsonAyar = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Yakala(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Yakala(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Yakala(DbContext context)
    {
        foreach (var giris in context.ChangeTracker.Entries())
        {
            var islem = giris.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => (string?)null,
            };
            if (islem is null) continue;
            if (giris.Metadata.ClrType.Name == "AuditRecord") continue; // SQL denetim kaydı — çift loglama

            gunluk.Write(new DataChangeEvent
            {
                ActionId = eylem.ActionId,
                Timestamp = DateTime.UtcNow,
                EntityName = giris.Metadata.ClrType.Name,
                Key = AnahtarDegeri(giris),
                Operation = islem,
                Before = giris.State is EntityState.Modified or EntityState.Deleted ? Serialize(giris, eski: true) : null,
                After = giris.State is EntityState.Modified or EntityState.Added ? Serialize(giris, eski: false) : null,
                TenantId = eylem.TenantId,
                UserId = eylem.UserId,
            });
        }
    }

    private static string? AnahtarDegeri(EntityEntry giris)
    {
        var pk = giris.Metadata.FindPrimaryKey();
        if (pk is null) return null;
        return string.Join(",", pk.Properties.Select(p => giris.Property(p.Name).CurrentValue?.ToString()));
    }

    private static string Serialize(EntityEntry giris, bool eski)
    {
        var sozluk = new Dictionary<string, object?>();
        foreach (var p in giris.Properties)
        {
            // Şifre/hassas alanlar loga sızmaz (şifreli olsa bile).
            if (p.Metadata.Name.Contains("Sifre", StringComparison.OrdinalIgnoreCase))
            {
                sozluk[p.Metadata.Name] = "***";
                continue;
            }
            sozluk[p.Metadata.Name] = eski ? p.OriginalValue : p.CurrentValue;
        }
        return JsonSerializer.Serialize(sozluk, JsonAyar);
    }
}
