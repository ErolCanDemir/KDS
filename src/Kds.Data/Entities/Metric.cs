namespace Kds.Data.Entities;

/// <summary>Metrik (S22 · "Metrikler"): bir ölçütün <b>tek, adlandırılmış, merkezi</b> tanımı —
/// "Toplam Ciro = SUM({Odemeler.Tutar})" gibi. Bir kez tanımlanır; tasarımcı/rapor/pano
/// <b>referansla</b> kullanır (modele formülün kopyası değil bu kaydın kimliği girer, formül koşum
/// anında buradan çözülür). Böylece tanım güncellenince onu kullanan her rapor bir sonraki koşumda
/// yeni tanımı kullanır — "iki raporda iki farklı ciro" sorununun kökten çözümü.
///
/// <para><b>Formül dili</b> tasarımcının Y1 ifade dilidir (<c>SUM({Tablo.Kolon})</c>, CASE,
/// aritmetik) ve kaydedilirken üreticinin kendisiyle harita üzerinde DOĞRULANIR — "geçerli görünüp
/// çalışmayan metrik" kaydedilemez.</para>
///
/// <para><b>Stratejik amaç:</b> Soru Sor (NLQ) için sözlük — <see cref="Aciklama"/> alanı, modelin
/// "ciro nedir?" sorusuna halüsinasyonsuz cevap verebilmesinin zeminidir.</para></summary>
public class Metric : EntityBase, ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Ad { get; set; } = string.Empty;

    /// <summary>Formülün kolonları bu haritadan gelir; metrik yalnız bu haritanın sorgularında
    /// kullanılabilir. Harita silinirse metrik öksüz kalmasın diye kaskaddır (DbContext).</summary>
    public Guid VeriHaritasiId { get; set; }

    /// <summary>Y1 ifade dilinde formül — ör. <c>SUM({Odemeler.Tutar})</c>.</summary>
    public string Ifade { get; set; } = string.Empty;

    /// <summary>Gösterim biçimi: "Sayi" | "Para" | "Yuzde" (MetrikBicimi adı).</summary>
    public string Bicim { get; set; } = "Sayi";

    /// <summary>İnsan (ve ileride NLQ) için: bu ölçüt neyi sayar, neyi saymaz.</summary>
    public string? Aciklama { get; set; }

    /// <summary>Local <c>User.Id</c> (yumuşak referans) — tanımlayan kişi katalogda görünür.</summary>
    public Guid SahipId { get; set; }

    public bool Aktif { get; set; } = true;
}
