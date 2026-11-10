using Xunit;

namespace Kds.Tests;

/// <summary>S40 — ORTAM DEĞİŞKENİ YARIŞINI KESER. `VeriDizini` ortam değişkeni SÜREÇ GENELİNDEDİR;
/// onu değiştiren iki test sınıfı (<see cref="KurulumHizmetiTests"/> ve <c>AnahtarHalkasiTests</c>)
/// xUnit tarafından PARALEL koşuluyor ve birbirinin ayarını eziyordu.
///
/// <para>Belirti kararsızdı: <c>sifirdan_kurulum_…</c> tam süitte kimi koşumda düşüyor, izole
/// koşumda ve bazen aynı süitin sonraki denemesinde geçiyordu (25–26 Ağu 2026). Testlerin kendisi
/// doğruydu — her biri benzersiz DB adı ve geçici dizin kullanıyor; çakışan tek şey global
/// değişkendi.</para>
///
/// <para>Aynı koleksiyondaki sınıflar ARDIŞIK koşar, yani bu iki sınıf artık birbirinin ortamını
/// bozamaz. Süitin geri kalanı paralel kalmaya devam eder — koleksiyon yalnız bu ikisini bağlar.
/// Ortam değişkenine dokunan YENİ bir test sınıfı eklenirse o da bu koleksiyona alınmalıdır.</para></summary>
[CollectionDefinition(Ad)]
public sealed class VeriDiziniKoleksiyonu
{
    public const string Ad = "VeriDizini (süreç genelinde ortam değişkeni)";
}
