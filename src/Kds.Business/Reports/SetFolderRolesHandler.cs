using Kds.Business.Identity;
using Kds.Contracts.Reports;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>S27 — klasörün rol erişimini günceller (yalnız admin; controller kilitler).
/// Boş/null liste erişimi kaldırır.</summary>
public sealed class SetFolderRolesHandler(KdsDbContext context)
    : IRequestHandler<SetFolderRolesRequest, FolderDto>
{
    public async Task<FolderDto> Handle(SetFolderRolesRequest request, CancellationToken cancellationToken)
    {
        var klasor = await context.RaporKlasorleri.FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Klasör bulunamadı.");

        klasor.ErisimRolleri = RolKurallari.Normallestir(request.Roller);
        await context.SaveChangesAsync(cancellationToken);

        var raporSayisi = await context.Raporlar.CountAsync(r => r.KlasorId == klasor.Id, cancellationToken);
        return new FolderDto(klasor.Id, klasor.Ad, raporSayisi,
            klasor.ErisimRolleri?.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }
}
