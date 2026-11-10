using Kds.Contracts.DataMaps;
using MediatR;

namespace Kds.Business.DataMaps;

/// <summary>Editörün "haritayı getir" ucu — asıl okuma <see cref="IDataMapReader"/>'da (sorgu
/// handler'larıyla paylaşılan tek yol); burası yalnız isteği servise bağlar.</summary>
public sealed class GetDataMapHandler(IDataMapReader okuyucu)
    : IRequestHandler<GetDataMapRequest, DataMapDetail?>
{
    public Task<DataMapDetail?> Handle(GetDataMapRequest request, CancellationToken cancellationToken) =>
        okuyucu.GetDetailAsync(request.Id, cancellationToken);
}
