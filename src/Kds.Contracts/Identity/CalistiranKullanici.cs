namespace Kds.Contracts.Identity;

/// <summary>Sorguyu çalıştıran kimlik — satır-düzeyi güvenliğin (RLS) kuralları KİME uygulayacağını
/// buradan bilir.
///
/// <para><b>Neden Contracts'ta:</b> zamanlanmış işler (abonelik · uyarı) arka planda, HTTP isteği
/// olmadan koşar; oradaki koşum, işi kuran kullanıcının kimliğini <b>taşımak</b> zorundadır. Kimlik
/// bir sözleşme parçası hâline geldiği için tip Business'tan Contracts'a taşındı (20 Ağu 2026):
/// istek nesneleri (RunQueryModelRequest) bunu içerebilsin diye.</para>
///
/// <para><b>Anlık görüntü (snapshot) mantığı:</b> arka plan koşumunda Keycloak'a gidip kullanıcının
/// güncel rollerini sormak yerine, iş kurulurken token'dan okunan rol/iddia kümesi işle birlikte
/// saklanır. Böylece koşum dış servise bağımlı olmaz. Bedeli bilinçlidir: kullanıcının rolü
/// sonradan değişirse iş eski kimlikle koşar — bu yüzden anlık görüntü, iş her kaydedildiğinde
/// tazelenir ve dokümanda açıkça yazılıdır.</para></summary>
public sealed record CalistiranKullanici(
    IReadOnlyCollection<string> Roller,
    string? KullaniciAdi,
    string? EPosta,
    IReadOnlyDictionary<string, string> Iddialar);
