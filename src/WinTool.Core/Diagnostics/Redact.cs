using WinTool.Core.Model;

namespace WinTool.Core.Diagnostics;

/// <summary>
/// 日志脱敏（方案 §6.2.3 第 1 条）：明文方案下，日志/异常里出现 password、username、notes 原文
/// 就等于把 vault 抄进了另一份更容易被收集的文件。这里提供唯一出口，禁止调用方直接 ToString()。
/// </summary>
public static class Redact
{
    public const string Mask = "***";

    public static string Value(string? value) => string.IsNullOrEmpty(value) ? "(empty)" : Mask;

    public static string Secret(string? secret, int lengthHint = -1) =>
        string.IsNullOrEmpty(secret) ? "(empty)" : $"{Mask}(len={(lengthHint >= 0 ? lengthHint : secret.Length)})";

    /// <summary>可安全写入日志的一行描述：标题可见（它不是凭据），其余全部打码。</summary>
    public static string Describe(CredentialRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return $"credential id={row.Id} title=\"{row.Title}\" username={Value(row.Username)} password={Secret(row.Password)} notes={Value(row.Notes)}";
    }
}
