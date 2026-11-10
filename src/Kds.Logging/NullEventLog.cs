using Kds.Contracts.Logging;

namespace Kds.Logging;

/// <summary>Olay günlüğü kapalıyken (Mongo yapılandırması yok) kullanılan no-op — olayları yutar.</summary>
public sealed class NullEventLog : IEventLog
{
    public void Write(ILogEvent olay) { }
}
