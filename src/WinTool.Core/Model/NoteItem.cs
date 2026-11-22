namespace WinTool.Core.Model;

/// <summary>
/// 随笔的领域模型（方案 §7.4）：D5 未确认前只做"单文件 / 多文件共用"的模型，不写磁盘布局常量。
/// </summary>
public sealed class NoteItem
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>纯 Markdown 文本 —— 数据源就是它本身，不是某个富文本模型序列化的结果。</summary>
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 随笔集合。D5（是否每篇落一个 .md）未确认，因此这里只定义内存/单文件载体，
/// 不定义"每篇一个文件"的命名规则；确认后若改布局，改动集中在 NoteStore。
/// </summary>
public sealed class NoteDocument
{
    public int Version { get; set; } = 1;

    public List<NoteItem> Items { get; set; } = [];

    public DateTimeOffset? UpdatedAt { get; set; }
}
