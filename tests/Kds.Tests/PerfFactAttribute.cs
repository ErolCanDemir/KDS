using Xunit;

namespace Kds.Tests;

/// <summary>Ağır performans testleri için Fact — <c>KDS_PERF=1</c> yoksa ATLANIR (varsayılan süit hızlı
/// kalır). Ölçüt #2 kanıtı için elle koşulur: <c>KDS_PERF=1 dotnet test</c>. 5M satır tohumu ~65s sürer.</summary>
public sealed class PerfFactAttribute : FactAttribute
{
    public PerfFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("KDS_PERF") != "1")
        {
            Skip = "Ağır perf testi (başarı ölçütü #2, ~75s). KDS_PERF=1 ile çalıştırın.";
        }
    }
}
