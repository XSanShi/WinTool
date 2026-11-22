using WinTool.Core.Model;

namespace WinTool.Core.Todo;

/// <summary>
/// 待办领域规则（纯函数，无 IO）。对应方案 §5.2–§5.4。
/// 这里的每个方法都不越界去做"未确认"的决定：不排序完成项（D8-1）、不清理（D2 由用户触发）。
/// </summary>
public static class TodoRules
{
    /// <summary>空白输入不建条目（方案 §5.2 第 2 步）。</summary>
    public static bool IsPlaceholder(string? text) => string.IsNullOrWhiteSpace(text);

    /// <summary>落库前统一处理：去首尾空白，换行统一为 \n。</summary>
    public static string NormalizeText(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    public static TodoItem Create(string? text, DateTimeOffset now, IReadOnlyList<TodoItem> existing)
    {
        if (IsPlaceholder(text))
        {
            throw new ArgumentException("条目文本为空，不应创建。", nameof(text));
        }

        return new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = NormalizeText(text),
            Done = false,
            CreatedAt = now,
            CompletedAt = null,
            ArchivedAt = null,
            Order = existing.Count,
        };
    }

    /// <summary>
    /// 勾选/取消勾选（方案 §5.3）。返回是否发生变化。
    /// 取消勾选时是否清空 completedAt 属 D2e 附属未决项；当前实现选择"清空"，
    /// 由参数控制，便于 D2e 定稿后一处切换。
    /// </summary>
    public static bool SetDone(TodoItem item, bool done, DateTimeOffset now, bool clearCompletedAtOnUndo = true)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Done == done)
        {
            return false;
        }

        item.Done = done;
        item.CompletedAt = done ? now : (clearCompletedAtOnUndo ? null : item.CompletedAt);
        return true;
    }

    /// <summary>order 维持连续整数，不出现空洞（借鉴 PaperTodo TodoRules.NormalizeOrders）。</summary>
    public static void NormalizeOrders(IReadOnlyList<TodoItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }
    }

    /// <summary>清理候选 = 全部已完成条目。D2 已确认"程序不自动清理"，因此没有任何自动触发点。</summary>
    public static List<TodoItem> SelectCompletedForCleanup(IEnumerable<TodoItem> items) =>
        items.Where(i => i.Done).ToList();

    /// <summary>未完成数量（角标/计数用）。</summary>
    public static int CountRemaining(IEnumerable<TodoItem> items) => items.Count(i => !i.Done);
}
