# Satır-Düzeyi Güvenlik (RLS)

> Kaynak: roadmap araştırma kuyruğu "Satır-düzeyi güvenlik — **v1 önerisi** (rakip analizi
> Kademe 1)". Uygulandı: 17–18 Ağu 2026.

## 1. Problem

Kiracı (tenant) yalıtımı zaten var: bir müşterinin verisi diğerine görünmez. Eksik olan, **kiracının
İÇİNDE** kullanıcı/rol bazlı daraltmaydı:

> "Bayi kullanıcısı yalnız **kendi servisinin** taleplerini görsün."

## 2. Nereye uygulanır — ve neden orası

Analizde belirleyici bulgu: `QueryController` zaten `[Authorize(Roles = admin,gelistirici)]`.
Yani **`musteri` rolü tasarımcıya da elle SQL'e de erişemiyor**; kayıtlı rapor ve pano çalıştırıyor.
Dolayısıyla RLS'in doğru yeri **sorgu üretimi** — kullanıcının dokunamadığı katman.

Tek boğaz noktası: `RunQueryModelHandler`. Her model koşumu oradan geçtiği için

- tasarımcı "Çalıştır",
- kayıtlı rapor,
- pano widget'ı,
- abonelik e-postası

**aynı anda** kapsama girer. Kuralı tek noktada uygulamak, yarın eklenecek bir koşum yolunun
yanlışlıkla filtresiz kalmasını da önler.

## 3. Kural modeli

`SatirKurallari` tablosu (`RowRule`): kural bir **veri haritası TABLOSUNU** kısıtlar.

| Alan | Anlamı |
|---|---|
| `VeriHaritasiId` + `HaritaTabloId` | hangi haritanın hangi tablosu |
| `Kolon` + `Op` | kısıtlama hangi kolon üzerinden, hangi karşılaştırmayla |
| `DegerKaynagi` + `Deger` | değer nereden: **token iddiası** (nitelik adı) · sabit değer · kullanıcı adı · e-posta |
| `Roller` | boş = admin dışında herkes; dolu = yalnız o roller |
| `Aktif` | pasife alınabilir (silmeden) |
| `Ad` | hata mesajlarında görünür — hangi kuralın engellediği anlaşılsın |

**Değer neden token'dan?** Kullanıcı nitelikleri için ayrı bir tablo tutmak ikinci bir doğruluk
kaynağı yaratırdı. Kimlik sağlayıcı (Keycloak) zaten kullanıcı niteliklerini tutuyor; kural
`servis_kodu` gibi bir **iddia adı** söyler, değeri token'dan okunur.

## 4. Güvenlik kararları

**(a) Kaçış yolu: VEYA'lı kullanıcı filtresi.** Kullanıcı filtresi `A VEYA B` iken kuralı düz `AND`
ile eklemek `A OR (B AND kural)` üretir; VEYA'nın sol dalı kuralı **atlar**. Bu yüzden kullanıcı
filtresinin **tamamı paranteze** alınır: `(kullanıcı filtresi) AND kural`.

**(b) Kaçış yolu: UNION.** Birleşime kuralsız ikinci bir sorgu eklenebilirdi. Kurallar birleşim
**dallarının her birine** uygulanır.

**(c) Kaçış yolu: modelden geçmeyen SQL.** Ham SQL'e kural enjekte edilemez (o bir metin, model
değil). İki kapı kapatıldı:
1. `POST /api/sorgu/ham` — kurala tabi kullanıcı elle SQL çalıştıramaz.
2. **Ham SQL ile kaydedilmiş rapor** — kurala tabi kullanıcı bu raporu koşamaz.
Mesajlar çözümü söyler ("raporu tasarımcıyla yeniden kurun ya da bu kullanıcılara kapatın").

**(d) Fail-closed.** Kural uygulanacakken değeri çözülemiyorsa (token'da beklenen iddia yok, kuralda
değer boş) sorgu **çalıştırılmaz** — HTTP **403**. Sessizce filtresiz koşmak, kullanıcının her şeyi
görmesi ve kimsenin fark etmemesi demekti; sızıntının en sinsi biçimi budur.

**(e) Kuralın tablosu sorguda yoksa** kural atlanır: o tablodan zaten satır dönmüyor.

**(f) Admin muaf.** Kuralları tanımlayan kişi verinin bütününü görmek zorundadır. Bu bilinçlidir ve
ekranda da yazılıdır.

**(g) Yanlış kural erken yakalanır.** Kural kaydedilirken tablo-harita tutarlılığı ve kolonun
varlığı doğrulanır; yoksa hata kaydetme anında verilir (çalıştırma anında sorguyu tamamen
durdurmasındansa).

## 5. Yönetim ekranı

**Yönetim → Satır Güvenliği (RLS)**, yalnız admin (`/api/satir-kurallari` de öyle: kuralı görebilen,
hangi verinin kimden saklandığını da görür).

Ekranda kural cümle gibi okunur: *"KdsDemo · **Talepler** tablosunda `ServisId = Kullanıcının kimlik
bilgisinden (servis_kodu)`"* + kimlere uygulandığı. Üstteki kutu davranışı açıkça anlatır: kuralın
ne zaman devreye girdiği, admin muafiyeti, fail-closed ve ham SQL kapıları.

## 6. Sınırlar (bilerek)

- Kural **tek kolon = değer(ler)** biçimindedir; serbest SQL ifadesi kabul edilmez (enjeksiyon
  yüzeyi ve doğrulanamaz kural riski). Karmaşık kapsam gerekiyorsa haritada bir görünüm (view)
  tanımlayıp onu kısıtlamak doğru yoldur.
- Kural **tablo düzeyindedir**; kolon maskeleme (belirli kolonu gizleme) kapsam dışıdır.
- Ham SQL yolunda kural uygulanamadığı için **yasaklanmıştır** — kısıtlama sessizce delinmesin.

## 7. Kimlik sağlayıcı yapılandırması (kural değeri token'dan gelsin)

Kural "kullanıcının kimlik bilgisinden" diyorsa, o nitelik **token'a girmelidir**. Keycloak'ta iki
adım gerekir — ikisi de `infra/kds-realm.json` içinde tanımlıdır (compose bunu içe alır):

1. **Kullanıcı niteliği:** kullanıcıda `servis_kodu` gibi bir nitelik (Users → Attributes).
   Çok değerli kapsam için virgülle: `12, 34`.
2. **Protocol mapper:** niteliği access token'a taşıyan eşleyici — realm'deki `tenant-id`
   eşleyicisiyle **aynı desendir** (`oidc-usermodel-attribute-mapper`).

```jsonc
{
  "name": "servis-kodu",
  "protocol": "openid-connect",
  "protocolMapper": "oidc-usermodel-attribute-mapper",
  "config": {
    "user.attribute": "servis_kodu",
    "claim.name": "servis_kodu",
    "jsonType.label": "String",
    "access.token.claim": "true",
    "id.token.claim": "true",
    "userinfo.token.claim": "true",
    "multivalued": "false"
  }
}
```

**Yeni bir kapsam alanı eklerken** (ör. `bolge_kodu`) yapılacaklar: kullanıcılara niteliği yaz →
realm'e aynı desende bir mapper ekle → kuralda "Kullanıcının kimlik bilgisinden" seçip nitelik adını
gir. KDS tarafında **kod değişikliği gerekmez**.

> Nitelik token'a girmezse kural fail-closed davranır: sorgu çalışmaz ve mesaj hangi kuralın hangi
> bilgiyi aradığını söyler. Bu, yapılandırma eksiğini sessiz veri sızıntısına yeğler.

### 7.1 Üçüncü adım: `unmanagedAttributePolicy` (atlanırsa nitelik SESSİZCE kaybolur)

Keycloak 24+ **declarative user profile** kullanır: yalnız tanımlı nitelikler yönetilir, geri kalanı
"unmanaged" sayılır ve varsayılan politikada **atılır**. Belirti sinsidir — Admin API'ye niteliği
yazarsınız, HTTP **204** döner, sonra kullanıcıyı okuduğunuzda nitelik **yoktur**; hata da uyarı da
verilmez. Bu yüzden realm'de açık:

```json
"components": {
  "org.keycloak.userprofile.UserProfileProvider": [
    { "providerId": "declarative-user-profile",
      "config": { "kc.user.profile.config": ["{… \"unmanagedAttributePolicy\": \"ENABLED\"}"] } }
  ]
}
```

Realm dosyasıyla **içe alınan** kullanıcıların nitelikleri bu ayardan etkilenmez; kayıp yalnız
**sonradan** (Admin API / yönetim ekranı) eklenen kullanıcılarda yaşanır — yani üretimde açılan
gerçek müşteri hesaplarında. `bayi` demo kullanıcısı bu yüzden hem realm dosyasında hem de canlı
realm'de doğrulandı.

### 7.2 İki tuzak (ikisi de canlı denemede yakalandı)

**(a) Realm JSON'una yorum eklenmez.** Açıklama niyetiyle bir nesneye fazladan alan (ör. `"_not"`)
koymak içe alımı **tamamen** düşürür; Keycloak bilinmeyen alanı reddeder ve hata mesajı sebebi
söylemez:

```
400 {"errorMessage":"unable to read contents from stream"}
```

Konteyner ilk açılışta realm'i içe alamaz. Açıklama gerekiyorsa **bu dokümana** yazılır.

**(b) Kullanıcıyı kısmi gösterimle güncellemeyin.** Admin API'de `PUT users/{id}` gövdeyi
**değiştirmez, değiştirir** — yalnız `{"attributes": …}` göndermek `firstName`/`lastName`/`email`
alanlarını siler. User profile bunları zorunlu tuttuğu için kullanıcı ilk girişte profil doldurmaya
zorlanır ve token alınamaz:

```
400 {"error":"invalid_grant","error_description":"Account is not fully set up"}
```

Doğrusu: önce `GET`, gösterimi **üzerine yamala**, sonra `PUT`.

### 7.3 Uçtan uca doğrulama (18 Ağu 2026)

Canlı sistemde `bayi` kullanıcısı (`servis_kodu: 1632`, rol `musteri`) ile aynı kayıtlı rapor iki
kimlikle koşuldu:

| Koşum | Sonuç |
|---|---|
| **admin** (muaf) | 50 servis satırı |
| **bayi** (kurala tabi) | **yalnız** `1632` (tek satır) |
| bayi + kural olmayan bir iddia istiyor | **403** — fail-closed, sorgu çalıştırılmadı |
| bayi + ham SQL ile kurulmuş rapor | **403** — ham SQL kapısı |

Ayrıca realm dosyası **sıfırdan yeni bir realm'e içe alınarak** sınandı: `unmanagedAttributePolicy`
geldi, `servis_kodu` token'da çıktı. Böylece "yalnız bu makinede elle ayarlandığı için çalışıyor"
ihtimali elendi.
