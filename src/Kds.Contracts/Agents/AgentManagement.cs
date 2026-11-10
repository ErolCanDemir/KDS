using Kds.Common.Cqrs;

namespace Kds.Contracts.Agents;

/// <summary>S45 — ajan liste kartı. <see cref="SonGorulme"/> ajanın son iş yoklaması; ekran
/// "çevrimiçi" çipini bundan çizer (yoklama aralığının ~2 katı içinde görüldüyse).</summary>
public sealed record AgentDto(
    Guid Id,
    string Ad,
    DateTimeOffset? SonGorulme,
    DateTimeOffset OlusturmaTarihi,
    /// <summary>"gecerli" | "iptal"</summary>
    string Durum,
    /// <summary>Bu ajana bağlı kaynak bağlantısı sayısı (silmeden önce görünür olsun).</summary>
    int BagliBaglantiSayisi);

/// <summary>Yeni ajan kaydı + anahtarı. Anahtar YALNIZ bu yanıtta döner, saklanmaz
/// (Gömme'deki parola deseni).</summary>
public sealed record CreateAgentRequest(string Ad) : ICommand<CreateAgentResult>;

public sealed record CreateAgentResult(
    Guid Id,
    string Anahtar,
    /// <summary>Ajanın makinesine konacak örnek <c>ajan.json</c> — anahtar ve adres yerleşik.</summary>
    string OrnekYapilandirma);

public sealed record ListAgentsRequest : IQuery<IReadOnlyList<AgentDto>>;

/// <summary>Ajanı İPTAL eder — silmez (Gömme deseni: sızıntı sonrası "ne zaman kapatıldı"
/// denetlenebilsin). İptal edilen ajanın anahtarı bir sonraki istekte reddedilir.</summary>
public sealed record RevokeAgentRequest(Guid Id) : ICommand<bool>;
