namespace Kds.Execution;

/// <summary>SELECT-only bekçisi (00-analiz §7): üretilen ya da elle yazılan SQL'in yalnız
/// TEK bir okuma (SELECT) ifadesi olduğunu AST üzerinden doğrular — string kontrolü DEĞİL.
/// Hem sorgu tasarımcısı üretimi hem ham SQL ekranı bu tek kapıdan geçer (çalıştırıcı zorlar).</summary>
public interface IQueryGuard
{
    GuardResult Inspect(string sql);
}

/// <summary>Bekçi kararı: geçerliyse <see cref="Gecerli"/>=true; değilse insana okunur sebep.</summary>
public readonly record struct GuardResult(bool Gecerli, string? Sebep)
{
    public static readonly GuardResult Gecer = new(true, null);
    public static GuardResult Ret(string sebep) => new(false, sebep);
}

/// <summary>Bekçi bir SQL'i reddettiğinde çalıştırıcı bunu fırlatır; icra hiç başlamaz.</summary>
public sealed class QuerySecurityException(string mesaj) : Exception(mesaj);
