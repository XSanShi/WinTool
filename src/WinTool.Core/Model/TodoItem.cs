namespace WinTool.Core.Model;

/// <summary>
/// 待办条目。字段与方案 §5.1 一一对应；此处不含 deadline（D4 未确认，禁止入模型）。
/// </summary>
public sealed class TodoItem
{
    /// <summary>稳定标识（32 位十六进制）。留底幂等依赖它，永不变更。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>单行标题；允许内含换行（留底时按续行缩进输出）。</summary>
    public string Text { get; set; } = string.Empty;

    public bool Done { get; set; }

    /// <summary>创建时刻。新建后不变。</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>完成时刻。D2 改为手动清理后，这是留底时间归属的唯一依据。</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>已成功写入留底的时刻；null = 尚未留底。清理成功后条目即被移除，此字段仅用于崩溃恢复与排障。</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>显示顺序，连续整数（借鉴 PaperTodo TodoRules.NormalizeOrders，TodoRules.cs:46-52）。</summary>
    public int Order { get; set; }
}

/// <summary>todos.json 的根对象。</summary>
public sealed class TodoDocument
{
    public int Version { get; set; } = 1;

    public List<TodoItem> Items { get; set; } = [];

    /// <summary>最近一次成功落盘的时刻（仅用于排障，不参与任何业务判断）。</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
