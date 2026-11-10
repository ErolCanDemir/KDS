using Kds.Common.Cqrs;
using Kds.Data;
using Kds.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kds.Business.Settings;

/// <summary>Ekrana dönen tek ayar satırı: tanım + geçerli değer + değerin nereden geldiği.</summary>
/// <summary><see cref="Deger"/> SIR tipindeki ayarlarda BOŞ döner (parola dışarı çıkmaz);
/// değerin girili olup olmadığı <see cref="Dolu"/> ile bildirilir.</summary>
public sealed record AyarDto(
    string Anahtar, string Grup, string Etiket, string Tip, string Aciklama,
    string Varsayilan, string Deger, string Kaynak, bool Dolu = false);

public sealed record ListSettingsRequest : IQuery<IReadOnlyList<AyarDto>>;

/// <summary>Ayarları yazar. Değer varsayılana EŞİTSE satır silinir (temiz kalsın: "varsayılan"
/// durumu satırın YOKLUĞU ile temsil edilir). Kapsam: kurulum geneli (TenantId null).</summary>
public sealed record SaveSettingsRequest(IReadOnlyDictionary<string, string> Degerler) : ICommand<int>;

public sealed class ListSettingsHandler(KdsDbContext context, ITenantContext tenant)
    : IRequestHandler<ListSettingsRequest, IReadOnlyList<AyarDto>>
{
    public async Task<IReadOnlyList<AyarDto>> Handle(ListSettingsRequest request, CancellationToken cancellationToken)
    {
        var kayitlar = await context.SistemAyarlari.AsNoTracking().ToListAsync(cancellationToken);
        var kiraciId = tenant.CurrentTenantId;

        return AyarTanimlari.Tumu.Select(t =>
        {
            var kiraci = kayitlar.FirstOrDefault(a => a.Anahtar == t.Anahtar && a.TenantId == kiraciId);
            var genel = kayitlar.FirstOrDefault(a => a.Anahtar == t.Anahtar && a.TenantId == null);
            var (deger, kaynak) = kiraci is not null ? (kiraci.Deger, "kiracı")
                : genel is not null ? (genel.Deger, "kurulum")
                : (t.Varsayilan, "varsayılan");
            // Sırlar ASLA dışarı çıkmaz; ekran yalnız "girili mi" bilgisini görür.
            var sir = t.Tip == AyarTipi.Sir;
            return new AyarDto(t.Anahtar, t.Grup, t.Etiket, t.Tip.ToString(), t.Aciklama, t.Varsayilan,
                sir ? string.Empty : deger, kaynak, Dolu: sir && !string.IsNullOrEmpty(deger));
        }).ToList();
    }
}

public sealed class SaveSettingsHandler(
    KdsDbContext context, IMemoryCache onbellek, Connections.IPasswordProtector? koruma = null)
    : IRequestHandler<SaveSettingsRequest, int>
{
    public async Task<int> Handle(SaveSettingsRequest request, CancellationToken cancellationToken)
    {
        var mevcut = await context.SistemAyarlari.Where(a => a.TenantId == null).ToListAsync(cancellationToken);
        var degisen = 0;

        foreach (var (anahtar, gelen) in request.Degerler)
        {
            var tanim = AyarTanimlari.Bul(anahtar);
            if (tanim is null) continue; // bilinmeyen anahtar sessizce yok sayılır (ileri/geri uyum)
            Dogrula(tanim, gelen);

            var sir = tanim.Tip == AyarTipi.Sir;
            var satir = mevcut.FirstOrDefault(a => a.Anahtar == anahtar);
            if (gelen == tanim.Varsayilan)
            {
                if (satir is not null) { context.SistemAyarlari.Remove(satir); degisen++; }
                continue;
            }
            // Sır ekrandan BOŞ gelirse "değiştirme" demektir (ekran mevcut parolayı hiç görmez);
            // temizlemek isteyen varsayılana (boş) döndürme yolunu kullanır — yukarıda ele alındı.
            var saklanacak = sir && koruma is not null ? koruma.Protect(gelen) : gelen;
            if (satir is null)
            {
                context.SistemAyarlari.Add(new SystemSetting
                {
                    Anahtar = anahtar, Deger = saklanacak, SirMi = sir,
                });
                degisen++;
            }
            else if (sir || satir.Deger != saklanacak)
            {
                // Sırda karşılaştırma anlamsız (her şifreleme farklı çıktı verir) → doğrudan yaz.
                satir.Deger = saklanacak;
                degisen++;
            }
        }

        if (degisen > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            SistemAyarlari.Bosalt(onbellek); // sonraki okuma tazelensin
        }
        return degisen;
    }

    private static void Dogrula(AyarTanimi tanim, string deger)
    {
        switch (tanim.Tip)
        {
            case AyarTipi.Sayi when !int.TryParse(deger, out var sayi) || sayi <= 0:
                throw new FluentValidation.ValidationException(
                    $"{tanim.Etiket}: pozitif bir sayı olmalı ('{deger}').");
            case AyarTipi.EvetHayir when !bool.TryParse(deger, out _):
                throw new FluentValidation.ValidationException($"{tanim.Etiket}: evet/hayır olmalı.");
        }
    }
}
