using Kds.Business.Queries;
using Kds.Execution;
using Kds.Contracts.Connections;
using Kds.Contracts.DataMaps;
using Kds.Contracts.Queries;
using Kds.Contracts.QueryModel;
using MediatR;
using Xunit;

namespace Kds.Tests;

/// <summary>RunQueryModelHandler zinciri: dataMap yüklenir → üretilir → yürütücüye gider.
/// Y1 regresyonu: modelin SatirLimiti yürütücü komutuna da taşınmalı (TOP 5000 üretilip
/// okumanın 1000'de kesilmesi tutarsızlığı).</summary>
public sealed class RunQueryModelTests
{
    private static readonly Guid TabloId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static DataMapDetail Harita() => new(
        Guid.NewGuid(), "H", Guid.NewGuid(), DataMode.Canli,
        [new DataMapTableDto(TabloId, "dbo", "T", "T",
            [new DataMapColumnDto("Ad", null, ColumnRole.Boyut, "nvarchar")])],
        []);

    /// <summary>Sahte dataMap okuyucu: hangi Id sorulursa sorulsun verilen haritayı döner.</summary>
    private sealed class SahteOkuyucu(DataMapDetail dataMap) : Kds.Business.DataMaps.IDataMapReader
    {
        public Task<DataMapDetail?> GetDetailAsync(Guid haritaId, CancellationToken cancellationToken) =>
            Task.FromResult<DataMapDetail?>(dataMap);
    }

    /// <summary>Sahte gönderici: tek-kapı RunQueryRequest'nu yakalar, boş sonuç döner.</summary>
    private sealed class SahteGonderici : ISender
    {
        public RunQueryRequest? Yurutulen;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is RunQueryRequest k)
            {
                Yurutulen = k;
                return Task.FromResult((TResponse)(object)new QueryRunResult([], [], false, 0));
            }
            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task model_satir_limiti_yurutucuye_tasinir()
    {
        var gonderici = new SahteGonderici();
        var handler = new RunQueryModelHandler(new SahteOkuyucu(Harita()), gonderici, new MssqlQueryGenerator(), new KuralsizCozucu(), new SahteKullanici(), new MetriksizCozucu());
        var model = new QueryModel(Guid.NewGuid(),
            [new SelectedField(TabloId, "Ad")], null, [], SatirLimiti: 5000);

        await handler.Handle(new RunQueryModelRequest(model), default);

        Assert.NotNull(gonderici.Yurutulen);
        Assert.Equal(5000, gonderici.Yurutulen!.SatirSiniri); // Y1: 1000 varsayılanına düşmez
        Assert.Contains("TOP (@satir)", gonderici.Yurutulen.Sql);
    }
}

/// <summary>Metrik içermeyen testler için no-op çözücü — modeli olduğu gibi geçirir.</summary>
public sealed class MetriksizCozucu : Kds.Business.Metrics.IMetrikCozucu
{
    public Task<Kds.Contracts.QueryModel.QueryModel> CozAsync(
        Kds.Contracts.QueryModel.QueryModel model, CancellationToken ct) => Task.FromResult(model);
}
