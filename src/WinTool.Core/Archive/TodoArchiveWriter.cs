using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WinTool.Core.Model;
using WinTool.Core.Storage;

namespace WinTool.Core.Archive;

/// <summary>留底目录与命名（D2b/D2d）。</summary>
public sealed class TodoArchiveOptions
{
    /// <summary>留底目录。D2b：用户可调整，默认在程序安装目录下新建一个目录（默认值由 App 层给出）。</summary>
    public required string Directory { get; init; }

    /// <summary>文件名模板，{0} = yyyy-MM。命名〔拟议·未确认〕，集中在此一处便于改。</summary>
    public string FileNamePattern { get; init; } = "待办留底-{0}.md";
}

/// <summary>一次清理请求。</summary>
public sealed record TodoArchiveRequest(IReadOnlyList<TodoItem> Items, DateTimeOffset CleanedAt);

/// <summary>一次留底结果。<see cref="Success"/> 为 false 时调用方必须禁止删除条码（写先于删）。</summary>
public sealed class TodoArchiveOutcome
{
    public bool Success { get; init; }

    public string? FilePath { get; init; }

    /// <summary>本次真正写进文件的新条目数。</summary>
    public int WrittenCount { get; init; }

    /// <summary>因"文件里已有该 id"而跳过的条目数（幂等）。</summary>
    public int SkippedCount { get; init; }

    /// <summary>失败原因，可直接展示给用户（不得包含任何账号密码字段值）。</summary>
    public string? Error { get; init; }

    /// <summary>true = 目标文件在读取后被外部改动，本次不写、不删。</summary>
    public bool ExternalChangeDetected { get; init; }

    public static TodoArchiveOutcome Failure(string error, string? path = null, bool externalChange = false) => new()
    {
        Success = false,
        Error = error,
        FilePath = path,
        ExternalChangeDetected = externalChange,
    };
}

/// <summary>
/// 留底写入器：D2c（月内追加）+ D2d（按月分包、永久保留）。
/// 关键实现约束：
/// 1) 不用 FileMode.Append —— 半写会在唯一历史凭据里留下半行（方案 §5.5.2），一律读改写 + 原子替换；
/// 2) 只写当月文件，旧月文件永不修改（方案 §5.5.3 第 2 条）；
/// 3) 幂等按 id 判定，先扫当月、未命中再扫上月（方案 §5.5.4）；
/// 4) 写前比对基线，用户正在编辑则不覆盖（方案 §5.5.3 第 1 条）。
/// </summary>
public sealed partial class TodoArchiveWriter
{
    private readonly TodoArchiveOptions _options;

    public TodoArchiveWriter(TodoArchiveOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Directory);
        _options = options;
    }

    public TodoArchiveOptions Options => _options;

    public TodoArchiveOutcome Write(TodoArchiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items.Count == 0)
        {
            return new TodoArchiveOutcome { Success = true, WrittenCount = 0, SkippedCount = 0 };
        }

        if (!AtomicFile.CanWriteToDirectory(_options.Directory))
        {
            return TodoArchiveOutcome.Failure($"留底目录不可写：{_options.Directory}");
        }

        var month = request.CleanedAt.ToLocalTime().ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var path = Path.Combine(_options.Directory, string.Format(CultureInfo.InvariantCulture, _options.FileNamePattern, month));

        string existing;
        FileBaseline? baseline;
        try
        {
            existing = AtomicFile.ReadAllTextOrNull(path) ?? string.Empty;
            baseline = AtomicFile.CaptureBaseline(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return TodoArchiveOutcome.Failure($"读取留底文件失败：{ex.Message}", path);
        }

        var archivedIds = ExtractIds(existing);
        var pending = request.Items.Where(i => !archivedIds.Contains(i.Id)).ToList();
        var skipped = request.Items.Count - pending.Count;

        // 当月未命中 → 再扫上月，覆盖"上月末清理、本月初撤销后重清"（方案 §5.5.4）。
        if (pending.Count > 0 && pending.Count == request.Items.Count)
        {
            var previous = ExtractIdsAt(PreviousMonthFile(request.CleanedAt), out _);
            if (previous.Count > 0)
            {
                pending = pending.Where(i => !previous.Contains(i.Id)).ToList();
                skipped = request.Items.Count - pending.Count;
            }
        }

        if (pending.Count == 0)
        {
            // 全部已留底：不写文件，但视为成功（调用方可以安全删除条目）。
            return new TodoArchiveOutcome { Success = true, FilePath = path, WrittenCount = 0, SkippedCount = skipped };
        }

        var segment = BuildSegment(pending, request.CleanedAt);
        var newContent = string.IsNullOrWhiteSpace(existing)
            ? segment + Environment.NewLine
            : existing.TrimEnd('\r', '\n') + "\n\n" + segment + Environment.NewLine;

        try
        {
            if (baseline is { } b && !AtomicFile.MatchesBaseline(path, b))
            {
                return TodoArchiveOutcome.Failure("留底文件在处理期间被其他程序修改，本次未写入、未删除任何条目。请关闭编辑器后重试。", path, externalChange: true);
            }

            AtomicFile.WriteAllText(path, newContent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return TodoArchiveOutcome.Failure($"写入留底失败：{ex.Message}", path);
        }

        return new TodoArchiveOutcome
        {
            Success = true,
            FilePath = path,
            WrittenCount = pending.Count,
            SkippedCount = skipped,
        };
    }

    /// <summary>月内一个批次：段头 + 空行 + 条目。</summary>
    public static string BuildSegment(IReadOnlyList<TodoItem> items, DateTimeOffset cleanedAt) =>
        TodoMarkdownFormatter.FormatSegmentHeader(cleanedAt) + "\n\n" + TodoMarkdownFormatter.FormatItems(items);

    private HashSet<string> ExtractIdsAt(string path, out string content)
    {
        content = AtomicFile.ReadAllTextOrNull(path) ?? string.Empty;
        return ExtractIds(content);
    }

    private string PreviousMonthFile(DateTimeOffset cleanedAt) =>
        Path.Combine(
            _options.Directory,
            string.Format(
                CultureInfo.InvariantCulture,
                _options.FileNamePattern,
                cleanedAt.ToLocalTime().AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture)));

    private static HashSet<string> ExtractIds(string content)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(content))
        {
            return set;
        }

        foreach (Match m in IdPattern().Matches(content))
        {
            set.Add(m.Groups[1].Value);
        }

        return set;
    }

    /// <summary>留底文件里 <c>id: &lt;32 位十六进制&gt;</c> 的提取。放松到 16 位以上，兼容用户手改过的短 id。</summary>
    [GeneratedRegex(@"\[\s*[xX ]\s*\].*?id:\s*([0-9a-fA-F]{16,40})", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
