using WinTool.Core.Archive;
using WinTool.Core.Model;

namespace WinTool.Core.Todo;

public sealed record CleanupResult(
    bool Success,
    int ArchivedCount,
    int SkippedAsAlreadyArchived,
    int RemovedCount,
    string? FilePath,
    string? Error,
    bool ExternalChangeDetected = false);

/// <summary>
/// 清理已完成条目（方案 §5.4 / §5.5.3）。整个类只为一件事存在：把「写先于删」这条不可协商的不变式
/// 落成一个不可能被绕过的方法。
/// </summary>
public sealed class TodoCleanupService
{
    private readonly TodoArchiveWriter _archive;
    private readonly TodoStore _store;
    private readonly Func<DateTimeOffset> _clock;

    public TodoCleanupService(TodoArchiveWriter archive, TodoStore store, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(store);
        _archive = archive;
        _store = store;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    /// <summary>
    /// 清理全部已完成条目。顺序不可调整：留底成功（含 flush）→ 才允许从 todos.json 移除。
    /// 任一步失败，条目原样保留，并返回可展示的错误。
    /// </summary>
    public CleanupResult CleanupCompleted(TodoDocument doc, string todosPath)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentException.ThrowIfNullOrWhiteSpace(todosPath);

        var completed = TodoRules.SelectCompletedForCleanup(doc.Items);
        if (completed.Count == 0)
        {
            return new CleanupResult(true, 0, 0, 0, null, null);
        }

        var now = _clock();
        var outcome = _archive.Write(new TodoArchiveRequest(completed, now));
        if (!outcome.Success)
        {
            // 写失败 → 一条都不删（这是"写先于删"的失败分支，不是尽力而为）。
            return new CleanupResult(false, 0, 0, 0, outcome.FilePath, outcome.Error, outcome.ExternalChangeDetected);
        }

        var ids = completed.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var snapshot = doc.Items.ToList();

        doc.Items.RemoveAll(i => ids.Contains(i.Id));
        TodoRules.NormalizeOrders(doc.Items);

        try
        {
            _store.Save(todosPath, doc, now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 留底已落盘，待办没存上：回滚内存状态，让界面显示"还是这些条目"。重试是安全的（留底幂等）。
            doc.Items.Clear();
            doc.Items.AddRange(snapshot);
            TodoRules.NormalizeOrders(doc.Items);
            return new CleanupResult(
                false,
                0,
                0,
                0,
                outcome.FilePath,
                $"留底已写入，但待办文件保存失败，条目保持原样可重试：{ex.Message}");
        }

        return new CleanupResult(true, outcome.WrittenCount, outcome.SkippedCount, completed.Count, outcome.FilePath, null);
    }
}
