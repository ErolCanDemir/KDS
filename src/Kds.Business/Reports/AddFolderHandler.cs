using Kds.Contracts.Reports;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Reports;

/// <summary>Yeni klasör (tenant içinde ad benzersiz).</summary>
public sealed class AddFolderHandler(KdsDbContext context)
    : IRequestHandler<AddFolderRequest, FolderDto>
{
    public async Task<FolderDto> Handle(AddFolderRequest request, CancellationToken cancellationToken)
    {
        var ad = request.Ad.Trim();
        if (await context.RaporKlasorleri.AnyAsync(k => k.Ad == ad, cancellationToken))
            throw new FluentValidation.ValidationException($"'{ad}' adında bir klasör zaten var.");
        var klasor = new ReportFolder { Ad = ad };
        context.RaporKlasorleri.Add(klasor);
        await context.SaveChangesAsync(cancellationToken);
        return new FolderDto(klasor.Id, klasor.Ad, 0);
    }
}
