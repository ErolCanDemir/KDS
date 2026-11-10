using Kds.Business.DataMaps;
using Kds.Contracts.Connections;
using Kds.Contracts.DataMaps;
using Kds.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kds.Tests;

/// <summary>S6 Veri Haritası kaydetme yolu — gerçek SQL Server (LocalDB) üstünde uçtan uca
/// persist doğrulaması: newEntity dataMap round-trip (liste + detay), güncelleme (alt varlıkları
/// tümüyle değiştirme) ve mod. İzole bir test tenant'ı kullanır; her testten önce/sonra temizler
/// (gerçek müşteri verisine dokunmaz — global query filter yalıtır).</summary>
public sealed class DataMapTests : IAsyncLifetime
{
    private const string LocalDb =
        "Server=(localdb)\\MSSQLLocalDB;Database=KdsMetadata;Trusted_Connection=True;TrustServerCertificate=True";
    private static readonly Guid TestTenant = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000a6a6");

    private static KdsDbContext YeniBaglam() =>
        new(new DbContextOptionsBuilder<KdsDbContext>().UseSqlServer(LocalDb).Options, new FixedTenantContext(TestTenant));

    public async Task InitializeAsync() => await TemizleAsync();
    public async Task DisposeAsync() => await TemizleAsync();

    private static async Task TemizleAsync()
    {
        await using var b = YeniBaglam();
        var hepsi = await b.Haritalar.ToListAsync();
        b.Haritalar.RemoveRange(hepsi);
        await b.SaveChangesAsync();
    }

    [Fact]
    public async Task yeni_harita_localdb_ye_yazilir_ve_geri_okunur()
    {
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var komut = new SaveDataMapRequest(null, "Satis Haritasi", Guid.NewGuid(), DataMode.Canli,
            new[]
            {
                new DataMapTableDto(t1, "dbo", "Musteriler", "Musteriler",
                    new[]
                    {
                        new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "uniqueidentifier"),
                        new DataMapColumnDto("Ad", "Musteri Adi", ColumnRole.Boyut, "nvarchar"),
                    }),
                new DataMapTableDto(t2, "dbo", "Siparisler", "Siparisler",
                    new[]
                    {
                        new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "uniqueidentifier"),
                        new DataMapColumnDto("Tutar", "Tutar", ColumnRole.Olcu, "money"),
                        new DataMapColumnDto("MusteriId", null, ColumnRole.Anahtar, "uniqueidentifier"),
                    }),
            },
            new[] { new DataMapRelationshipDto(t2, "MusteriId", t1, "Id", true) });

        DataMapSummary ozet;
        await using (var b = YeniBaglam())
            ozet = await new SaveDataMapHandler(b).Handle(komut, default);

        Assert.NotEqual(Guid.Empty, ozet.Id);
        Assert.Equal(2, ozet.TabloSayisi);
        Assert.Equal(1, ozet.IliskiSayisi);

        await using (var b = YeniBaglam())
        {
            var liste = await new ListDataMapsHandler(b).Handle(new ListDataMapsRequest(), default);
            var satir = Assert.Single(liste);
            Assert.Equal(ozet.Id, satir.Id);
            Assert.Equal("Satis Haritasi", satir.Ad);
            Assert.Equal(2, satir.TabloSayisi);
            Assert.Equal(1, satir.IliskiSayisi);
        }

        await using (var b = YeniBaglam())
        {
            var detay = await new GetDataMapHandler(new DataMapReader(b)).Handle(new GetDataMapRequest(ozet.Id), default);
            Assert.NotNull(detay);
            Assert.Equal(2, detay!.Tablolar.Count);
            var siparis = detay.Tablolar.Single(x => x.HamAd == "Siparisler");
            Assert.Equal(3, siparis.Kolonlar.Count);
            Assert.Contains(siparis.Kolonlar, k => k.HamAd == "Tutar" && k.Rol == ColumnRole.Olcu);
            var iliski = Assert.Single(detay.Iliskiler);
            Assert.Equal("MusteriId", iliski.KaynakKolon);
            Assert.True(iliski.FktenMi);
        }
    }

    [Fact]
    public async Task guncelleme_alt_varliklari_tumuyle_degistirir()
    {
        var t1 = Guid.NewGuid();
        var newEntity = new SaveDataMapRequest(null, "V1", Guid.NewGuid(), DataMode.Canli,
            new[] { new DataMapTableDto(t1, "dbo", "A", "A", new[] { new DataMapColumnDto("Id", null, ColumnRole.Anahtar, "int") }) },
            Array.Empty<DataMapRelationshipDto>());

        Guid id;
        await using (var b = YeniBaglam())
            id = (await new SaveDataMapHandler(b).Handle(newEntity, default)).Id;

        var t2 = Guid.NewGuid();
        var guncel = new SaveDataMapRequest(id, "V2", Guid.NewGuid(), DataMode.IceAlim,
            new[] { new DataMapTableDto(t2, "dbo", "B", "B urunler", new[] { new DataMapColumnDto("Kod", null, ColumnRole.Boyut, "nvarchar") }) },
            Array.Empty<DataMapRelationshipDto>());

        await using (var b = YeniBaglam())
            await new SaveDataMapHandler(b).Handle(guncel, default);

        await using (var b = YeniBaglam())
        {
            var detay = await new GetDataMapHandler(new DataMapReader(b)).Handle(new GetDataMapRequest(id), default);
            Assert.NotNull(detay);
            Assert.Equal("V2", detay!.Ad);
            Assert.Equal(DataMode.IceAlim, detay.Mod);
            var tablo = Assert.Single(detay.Tablolar);
            Assert.Equal("B", tablo.HamAd);
            Assert.Equal("B urunler", tablo.DostcaAd);
            Assert.Equal("Kod", Assert.Single(tablo.Kolonlar).HamAd);
        }

        await using (var b = YeniBaglam())
            Assert.True(await new DeleteDataMapHandler(b).Handle(new DeleteDataMapRequest(id), default));
    }
}
