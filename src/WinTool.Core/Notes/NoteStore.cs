using System.Text.Json;
using System.Text.Json.Serialization;
using WinTool.Core.Model;
using WinTool.Core.Storage;

namespace WinTool.Core.Notes;

/// <summary>
/// 随笔存储。当前载体是单个 <c>notes.json</c>（D5 未确认前的拟议布局，确认"每篇一个 .md"
/// 之后再改这里；UI 层只依赖 NoteStore，不直接碰路径）。
/// </summary>
public sealed class NoteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public NoteDocument Load(string path)
    {
        var text = AtomicFile.ReadAllTextOrNull(path);
        if (text is null || string.IsNullOrWhiteSpace(text))
        {
            return new NoteDocument();
        }

        var doc = JsonSerializer.Deserialize<NoteDocument>(text, JsonOptions) ?? new NoteDocument();
        doc.Items ??= [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        doc.Items = doc.Items
            .Where(i => i is not null && !string.IsNullOrEmpty(i.Id) && seen.Add(i.Id))
            .ToList();
        return doc;
    }

    public void Save(string path, NoteDocument document, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Version = 1;
        document.UpdatedAt = now;
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
    }

    /// <summary>新建一篇：标题留空时用正文首行兜底，全部为空则拒绝。</summary>
    public static NoteItem Create(string title, string content, DateTimeOffset now, IReadOnlyCollection<NoteItem> existing)
    {
        var normalizedTitle = (title ?? string.Empty).Trim();
        var normalizedContent = (content ?? string.Empty).Replace("\r\n", "\n").TrimEnd();

        if (normalizedTitle.Length == 0 && normalizedContent.Trim().Length == 0)
        {
            throw new ArgumentException("标题与正文不能同时为空。", nameof(content));
        }

        if (normalizedTitle.Length == 0)
        {
            var firstLine = normalizedContent.Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "未命名";
            normalizedTitle = firstLine.Trim();
            if (normalizedTitle.Length > 60)
            {
                normalizedTitle = normalizedTitle[..60];
            }
        }

        return new NoteItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = normalizedTitle,
            Content = normalizedContent,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
