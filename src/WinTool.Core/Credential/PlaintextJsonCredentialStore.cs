using System.Text.Json;
using WinTool.Core.Model;
using WinTool.Core.Storage;

namespace WinTool.Core.Credential;

/// <summary>
/// 存储层接口（方案 §6.2.2 第 5 条）：把"格式"关在一个实现里，将来若改加密只需换实现。
/// </summary>
public interface ICredentialStore
{
    CredentialDocument Load();

    void Save(CredentialDocument document);
}

/// <summary>
/// D1a 已确认：本地明文 JSON。目标是人能用记事本打开、看懂、手改、修坏了能自己救回来，
/// 所以不做 base64、不做字段混淆、不加二进制头（方案 §6.2.1 / §6.2.4）。
/// </summary>
public sealed class PlaintextJsonCredentialStore : ICredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _path;

    public PlaintextJsonCredentialStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public string Path => _path;

    public CredentialDocument Load()
    {
        var text = AtomicFile.ReadAllTextOrNull(_path);
        if (text is null || string.IsNullOrWhiteSpace(text))
        {
            return new CredentialDocument();
        }

        var doc = JsonSerializer.Deserialize<CredentialDocument>(text, JsonOptions) ?? new CredentialDocument();
        doc.Rows ??= [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        doc.Rows = doc.Rows
            .Where(r => r is not null && !string.IsNullOrEmpty(r.Id) && seen.Add(r.Id))
            .ToList();
        return doc;
    }

    public void Save(CredentialDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Version = 1;
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(document, JsonOptions));
    }
}
