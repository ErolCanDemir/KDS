# İngilizce Kod Geçişi — Sözlük ve Yaklaşım

**Karar (2026-08-08):** Proje dili İngilizce. **Kod** (sınıf, metot, özellik, enum, namespace,
proje adları; backend + frontend) İngilizce olur. **DB Türkçe kalır** (EF eşlemesiyle; migration
YOK). **Ekran metinleri Türkçe kalır** (JSX'teki görünen etiketler/mesajlar string sabittir).
JSON wire İngilizce olur — backend + frontend birlikte döndüğü için kırılma yoktur.

Bu dosya, geçişin TEK referansıdır; tüm makine/oturumlar buna uyar (yanlış çeviriyi 300 dosyada
dönmemek için).

## DURUM (2026-08-09): TAMAMLANDI

Geçiş bitti; her adım build+test yeşil, ayrı commit. **Uygulama, ilk plandan daha BASİT/GÜVENLİ
oldu:** aşağıdaki "kolon/enum HasColumnName + dönüştürücü" mekanizmasına gerek kalmadı, çünkü:

- **Özellik/DTO alan adları + enum ÜYE değerleri TÜRKÇE bırakıldı** (İngilizce+eşleme yerine). Böylece
  DB kolonları/tabloları/saklanan değerler **doğal olarak** Türkçe kaldı — `HasColumnName`/enum
  dönüştürücü/rename-migration **hiç gerekmedi**. Şema hiç değişmedi.
- **JSON wire de Türkçe** (`ad/sunucu/mod`) — özellikler Türkçe olduğundan. Backend+frontend aynı
  Türkçe alan adlarını kullanır; kırılma yok.
- **İngilizce olan:** proje · namespace · klasör · sınıf/arayüz/record/enum TİP adları · metotlar ·
  dosya adları · frontend bileşen/sayfa/api/yardımcı **kimlikleri**. Kod okuma dili İngilizce.
- **Türkçe kalan:** DB (kolon/tablo/enum değeri) · JSON wire alanları · ekran metinleri ·
  validator/exception mesajları · route URL'leri (`/panolar`, `/raporlar`) · **kod içi yorumlar**
  (karar 2026-08-09: yorumlar Türkçe bırakılır, çevrilmez).
- **Wire-çakışan frontend yerelleri** (`tablolar/kolonlar/iliskiler/alanlar`) Türkçe bırakıldı:
  JSON alan adlarıyla aynı token oldukları için bilinçli istisna (çeviri wire'ı bozardı).
- **Tek migration:** yeni **`Panolar`** tablosu (S11 Dashboard özelliği) — Türkçe kolonlu; geçişle
  ilgisi yok, yeni özelliğin deposu.

Bu yüzden aşağıdaki "DB'yi Türkçe tutma mekanizması" bölümü **uygulanmadı** (gereksiz kaldı);
tarihsel plan olarak bırakıldı.

## DB'yi Türkçe tutma mekanizması (migration yok)

- **Kolon:** İngilizce özelliğe `HasColumnName("TurkceAd")` (yalnız adı değişen kolonlar; `Id`,
  `TenantId` zaten korunuyor). Örn. `Property(x => x.Name).HasColumnName("Ad")`.
- **Tablo:** her entity'ye açık `ToTable("TurkceAd")` (DbSet İngilizce olsa da tablo korunur).
- **Enum (string saklanan):** `HasConversion<string>()` yerine değer eşlemeli dönüştürücü —
  kod `Live`, DB `"Canli"`. Her enum için `ValueConverter<Enum,string>`.
- **Seed (`HasData`) veri değerleri Türkçe kalır** (ör. "Deneme A.Ş.").

## Proje adları

| Şu an | Yeni |
|---|---|
| Kds.Ortak | **Kds.Common** |
| Kds.Contracts | Kds.Contracts (değişmez) |
| Kds.Business | Kds.Business (değişmez) |
| Kds.Dal | **Kds.Data** |
| Kds.Calistirma | **Kds.Execution** |
| Kds.Api | Kds.Api (değişmez) |
| Kds.Tests | Kds.Tests (değişmez) |

## Modül / namespace

| Türkçe | English |
|---|---|
| Baglantilar / Baglanti | Connections / Connection |
| Haritalar / VeriHaritasi | DataMaps / DataMap |
| Sorgular / Sorgu | Queries / Query |
| SorguModeli | QueryModel |
| Raporlar / Rapor | Reports / Report |
| Kullanicilar / Kullanici | Users / User |
| SistemDurumu | SystemStatus |
| Kimlik | Identity |
| Denetim | Audit |

## Fiiller (operasyon adları)

| Türkçe | English | | Türkçe | English |
|---|---|---|---|---|
| Ekle | Add | | Calistir | Run |
| Guncelle | Update | | Uret | Generate |
| Sil | Delete | | Kaydet | Save |
| Listele | List | | Test | Test |
| Getir | Get | | Sagla | Provision |
| Ara | Search | | Favori | Favorite |

## Sonekler / rol adları

| Türkçe | English |
|---|---|
| ...Komutu / ...Sorgusu | ...Request (marker `IKomut`→`ICommand`, `ISorgu`→`IQuery`) |
| ...Dogrulayici | ...Validator |
| ...Handler | ...Handler (değişmez) |
| ...Ozeti | ...Summary |
| ...Detay | ...Detail |
| ...Sonucu | ...Result |
| ...Eslemesi | ...Mapping |
| ...Uretici | ...Generator |
| ...Yurutucu | ...Executor |
| ...Bekcisi | ...Guard |
| ...Denetci | ...Prober |
| DogrulamaDavranisi | ValidationBehavior |

## Ortak alan/tip adları

| Türkçe | English | | Türkçe | English |
|---|---|---|---|---|
| Ad | Name | | Sema | Schema |
| Sunucu | Server | | Tablo | Table |
| Veritabani | Database | | Kolon | Column |
| KullaniciAdi | Username | | Iliski | Relationship |
| Sifre | Password | | Rol | Role |
| Mod | Mode | | Olcu | Measure |
| DostcaAd | DisplayName | | Boyut | Dimension |
| HamAd | RawName | | Kaynak | Source |
| VeriTipi | DataType | | Hedef | Target |
| OlusturmaTarihi | CreatedAt | | GuncellemeTarihi | UpdatedAt |

## Enum'lar (kod İngilizce · DB Türkçe saklanır)

| Enum (kod) | Üyeler (kod → DB) |
|---|---|
| VeriAlmaModu → **DataMode** | Live→"Canli", Ingest→"IceAlim" |
| KolonRolu → **ColumnRole** | Dimension→"Boyut", Measure→"Olcu", Key→"Anahtar" |
| RaporPaylasimi → **ReportSharing** | Private→"Ozel", Team→"Ekip" |
| GorsellestirmeTuru → **VisualizationType** | Table→"Tablo", Bar→"Cubuk", Line→"Cizgi", Pie→"Pasta", ScoreCard→"SayiKarti" |
| TenantDurumu → **TenantStatus** | Active→"Aktif" |

## Sıralama (her adım build + test yeşil, ayrı commit)

1. **Kds.Common** (eski Kds.Ortak): ICommand/IQuery/ValidationBehavior.
2. **Contracts çekirdeği**: QueryModel + DataMap/Schema DTO'ları + Result tipleri.
3. **Kds.Data** (eski Kds.Dal): entity'ler İngilizce + `HasColumnName`/`ToTable`/enum dönüştürücü (DB korunur).
4. **Kds.Execution** (eski Kds.Calistirma): Generator/Executor/Guard/Prober.
5. **Business modülleri** (dikey dilimle birlikte): Connections, Reports, Users, SystemStatus, Queries, DataMaps.
6. **Api** controller'ları.
7. **Frontend**: api/*.ts alan adları + fonksiyon/değişken/tip adları (ekran metinleri Türkçe kalır).
8. Docs/roadmap güncellemesi.

Not: Baglantilar + Kds.Ortak bu oturumda Türkçe kuruldu; geriye dönük İngilizceye çevrilir.
