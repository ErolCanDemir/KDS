using Kds.Execution;
using MediatR;

namespace Kds.Business.Connections;

/// <summary>Form değerleriyle (kaydetmeden) dener — parola düz metin gelir, saklanmaz.</summary>
public sealed class TestConnectionHandler(IConnectionProber denetci)
    : IRequestHandler<TestConnectionRequest, ConnectionTestResult>
{
    public async Task<ConnectionTestResult> Handle(TestConnectionRequest request, CancellationToken cancellationToken)
    {
        var s = await denetci.ProbeAsync(request.Sunucu, request.Veritabani, request.KullaniciAdi, request.Sifre, cancellationToken);
        return new ConnectionTestResult(s.Basarili, s.Ms, s.Mesaj);
    }
}
