namespace WinTool.Core.Model;

/// <summary>账号密码记录（方案 §6.1）。字段全部为明文，见 §6.2。</summary>
public sealed class CredentialRow
{
    public string Id { get; set; } = string.Empty;

    /// <summary>站点名 / 标题（列表主显示）。</summary>
    public string Title { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    /// <summary>密码原文。禁止进日志、禁止 ToString() 外泄。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>只存文本，不自动打开（避免引入浏览器依赖）。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>备注 / 二级验证恢复码等。</summary>
    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>vault.json 的根对象。</summary>
public sealed class CredentialDocument
{
    public int Version { get; set; } = 1;

    public List<CredentialRow> Rows { get; set; } = [];
}
