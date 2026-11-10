using Kds.Common.Cqrs;

namespace Kds.Business.Connections;

/// <summary>Kayıtlı bağlantıyı dener (parola çözülür) ve son test sonucunu bağlantıya işler.</summary>
public sealed record TestConnectionByIdRequest(Guid Id) : ICommand<ConnectionTestResult>;
