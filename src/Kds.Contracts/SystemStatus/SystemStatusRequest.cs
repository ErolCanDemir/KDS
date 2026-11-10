using Kds.Common.Cqrs;

namespace Kds.Contracts.SystemStatus;

/// <summary>İskeletin dikey kanıtı: API → MediatR → handler hattının çalıştığını
/// gösteren, DB'ye inmeyen tek sorgu.</summary>
public sealed record SystemStatusRequest : IQuery<SystemStatusResponse>;
