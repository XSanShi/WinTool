using System.Text.Json;
using System.Text.Json.Serialization;
using WinTool.Core.Model;
using WinTool.Core.Storage;

namespace WinTool.Core.Todo;

/// <summary>todos.json 读写。字段名用 camelCase（方案 §5.1 的 jsonc 示例即为 camelCase）。</summary>
public sealed class TodoStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReadCommentHandling = JsonCommentHandling.Skip, // 容忍用户手改时留下的注释
        AllowTrailingCommas = true,
    };

    public TodoDocument Load(string path)
    {
        var text = AtomicFile.ReadAllTextOrNull(path);
        if (text is null || string.IsNullOrWhiteSpace(text))
        {
            return new TodoDocument();
        }

        var doc = JsonSerializer.Deserialize<TodoDocument>(text, JsonOptions) ?? new TodoDocument();
        doc.Items ??= [];
        // 手改后可能残留重复 id：保留首个，其余丢弃，避免留底幂等键被污染。
        var seen = new HashSet<string>(StringComparer.Ordinal);
        doc.Items = doc.Items.Where(i => i is not null && !string.IsNullOrEmpty(i.Id) && seen.Add(i.Id)).ToList();
        TodoRules.NormalizeOrders(doc.Items);
        return doc;
    }

    public void Save(string path, TodoDocument doc, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(doc);
        doc.Version = 1;
        doc.UpdatedAt = now;
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        AtomicFile.WriteAllText(path, json);
    }

    public string Serialize(TodoDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return JsonSerializer.Serialize(doc, JsonOptions);
    }
}
