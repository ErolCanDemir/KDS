namespace Kds.Business.Embedding;

/// <summary>S42 — belirli bir kiracı adına çalışma kapsamı. Gömülü istekte oturum YOKTUR: kiracı
/// ancak anahtar çözüldükten sonra bilinir, dolayısıyla sonraki sorguların hangi kiracıda koşacağı
/// çalışma anında bağlanmalıdır.
///
/// <para>Uygulaması Api katmanındaki <c>AmbientTenant</c>'tır (zamanlayıcıların kullandığı desenin
/// aynısı). İş katmanı o tipi göremez — bu yüzden sınır burada soyutlanır.</para></summary>
public interface IKiraciKapsami
{
    IDisposable Baglan(Guid kiraciId);
}
