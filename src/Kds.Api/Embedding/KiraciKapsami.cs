using Kds.Api.Identity;
using Kds.Business.Embedding;

namespace Kds.Api.Embedding;

/// <summary>S42 — <see cref="IKiraciKapsami"/> uygulaması: zamanlayıcıların kullandığı
/// <see cref="AmbientTenant"/> desenini iş katmanına açar. Gömülü istekte oturum yoktur; kiracı
/// ancak anahtar çözüldükten sonra bilinir ve sonraki sorgular bu kapsamda koşar.</summary>
public sealed class KiraciKapsami : IKiraciKapsami
{
    public IDisposable Baglan(Guid kiraciId) => AmbientTenant.Baglan(kiraciId);
}
