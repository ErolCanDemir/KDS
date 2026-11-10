using Kds.Execution;
using Kds.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kds.Business.Connections;

/// <summary>Kayıtlı bağlantıyı dener (parola çözülür) ve son test sonucunu bağlantıya işler.</summary>
public sealed class TestConnectionByIdHandler(KdsDbContext context, IConnectionProber denetci, IPasswordProtector koruma)
    : IRequestHandler<TestConnectionByIdRequest, ConnectionTestResult>
{
    public async Task<ConnectionTestResult> Handle(TestConnectionByIdRequest request, CancellationToken cancellationToken)
    {
        var b = await context.Baglantilar.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (b is null) return new ConnectionTestResult(false, null, "Bağlantı bulunamadı.");

        var sifre = string.IsNullOrEmpty(b.SifreSifreli) ? null : koruma.Unprotect(b.SifreSifreli);
        var s = await denetci.ProbeAsync(b.Sunucu, b.Veritabani, b.KullaniciAdi, sifre, cancellationToken);

        b.SonTestBasarili = s.Basarili;
        b.SonTestMs = s.Ms;
        b.SonTestTarihi = DateTimeOffset.Now;
        await context.SaveChangesAsync(cancellationToken);

        return new ConnectionTestResult(s.Basarili, s.Ms, s.Mesaj);
    }
}
