using System.Text.RegularExpressions;
using Kds.Execution;
using Xunit;

namespace Kds.Tests;

/// <summary>S47 — fonksiyon ipucu ile ifade BEKÇİSİ ayrışamaz: ipucunda gösterilen her ad izinli
/// kümede olmalı ve her örnek ifade denetimden GEÇMELİ. Bu test kırılırsa ya ipucuna izinsiz
/// fonksiyon yazılmış ya da bekçi daraltılıp ipucu unutulmuştur — ikisi de kullanıcıya
/// "önerip reddetme" yaşatır.</summary>
public sealed class IfadeYardimiTests
{
    [Fact]
    public void ipucundaki_her_ad_bekcinin_izinli_kumesinde()
    {
        foreach (var grup in IfadeYardimi.Gruplar)
        {
            Assert.NotEmpty(grup.Fonksiyonlar);
            foreach (var f in grup.Fonksiyonlar)
            {
                Assert.True(IfadeDenetimi.IzinliAdlar.Contains(f.Ad),
                    $"'{f.Ad}' ipucunda var ama bekçinin izinli kümesinde yok.");
                Assert.False(string.IsNullOrWhiteSpace(f.Ornek));
                Assert.False(string.IsNullOrWhiteSpace(f.Aciklama));
            }
        }
    }

    [Fact]
    public void ipucundaki_her_ornek_ifade_denetiminden_gecer()
    {
        foreach (var f in IfadeYardimi.Gruplar.SelectMany(g => g.Fonksiyonlar))
        {
            // Gerçek akışta {Tablo.Kolon} yer tutucuları denetimden ÖNCE çözülür (üreticideki
            // DenetimMetni sabite indirir) — testte aynı indirme uygulanır.
            var cozulmus = Regex.Replace(f.Ornek, @"\{[^{}]+\}", "0");
            var hata = Record.Exception(() => IfadeDenetimi.Dogrula(cozulmus, $"İpucu örneği ({f.Ad})"));
            Assert.True(hata is null, $"'{f.Ad}' örneği denetimden geçmedi: {hata?.Message}");
        }
    }
}
