using System.Text.Json;
using FluentValidation;
using Kds.Business.Identity;
using Kds.Contracts.Agents;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Agents;

/// <summary>S45 — ajan kaydı üretimi. Anahtar YALNIZ bu yanıtta döner ve saklanmaz (Gömme'deki
/// parola deseni): saklanan sır, sızabilecek ikinci kopyadır. Kaybedilirse ajan iptal edilip
/// yenisi üretilir.</summary>
public sealed class CreateAgentHandler(
    KdsDbContext context, IAjanAnahtarKoruyucu koruyucu, IUserContext kb)
    : IRequestHandler<CreateAgentRequest, CreateAgentResult>
{
    public async Task<CreateAgentResult> Handle(CreateAgentRequest request, CancellationToken cancellationToken)
    {
        var ajan = new Agent
        {
            Ad = request.Ad.Trim(),
            OlusturanId = await Reports.ReportCommon.MyIdAsync(context, kb, cancellationToken) ?? Guid.Empty,
        };
        context.Ajanlar.Add(ajan);
        await context.SaveChangesAsync(cancellationToken);

        var anahtar = koruyucu.Uret(ajan.Id);
        return new CreateAgentResult(ajan.Id, anahtar, OrnekYapilandirma(anahtar));
    }

    /// <summary>Ajan makinesine konacak örnek <c>ajan.json</c>. Adres yer tutucu ({KDS_ADRESI}) —
    /// sunucu kendi dış adresini güvenilir bilemez (Gömme'deki karar); ekran gerçek adresle doldurur.
    /// Bağlantı dizeleri BURADA, müşteri tarafında durur — KDS'ye hiç gönderilmez.</summary>
    private static string OrnekYapilandirma(string anahtar) => JsonSerializer.Serialize(new
    {
        kdsAdres = "{KDS_ADRESI}",
        anahtar,
        yoklamaSaniye = 60,
        baglantilar = new Dictionary<string, string>
        {
            ["<baglanti-id>"] = "Server=SUNUCU;Database=VERITABANI;User Id=salt_okur;Password=***;TrustServerCertificate=True",
        },
    }, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class CreateAgentValidation : AbstractValidator<CreateAgentRequest>
{
    public CreateAgentValidation()
    {
        RuleFor(x => x.Ad).NotEmpty().MaximumLength(200)
            .WithMessage("Ajana tanınabilir bir ad verin (ör. \"Merkez fabrika ağı\").");
    }
}

public sealed class ListAgentsHandler(KdsDbContext context)
    : IRequestHandler<ListAgentsRequest, IReadOnlyList<AgentDto>>
{
    public async Task<IReadOnlyList<AgentDto>> Handle(ListAgentsRequest request, CancellationToken cancellationToken)
    {
        // Sıralama bellekte — SQLite DateTimeOffset ORDER BY çeviremiyor (raporlardaki desen).
        var liste = await context.Ajanlar.AsNoTracking()
            .Select(a => new AgentDto(
                a.Id, a.Ad, a.SonGorulme, a.OlusturmaTarihi,
                a.IptalTarihi != null ? "iptal" : "gecerli",
                context.Baglantilar.Count(b => b.AjanId == a.Id)))
            .ToListAsync(cancellationToken);
        return [.. liste.OrderByDescending(a => a.OlusturmaTarihi)];
    }
}

/// <summary>İPTAL eder — silmez ("ne zaman kapatıldı" denetlenebilsin; Gömme deseni).</summary>
public sealed class RevokeAgentHandler(KdsDbContext context)
    : IRequestHandler<RevokeAgentRequest, bool>
{
    public async Task<bool> Handle(RevokeAgentRequest request, CancellationToken cancellationToken)
    {
        var a = await context.Ajanlar.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (a is null || a.IptalTarihi is not null)
        {
            return false;
        }

        a.IptalTarihi = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
