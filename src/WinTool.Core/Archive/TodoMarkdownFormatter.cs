using System.Globalization;
using System.Text;
using WinTool.Core.Model;

namespace WinTool.Core.Archive;

/// <summary>
/// 待办 → Markdown 文本（方案 §5.5.5 / §5.5.7）。转义集合照抄 PaperTodo
/// <c>TodoClipboardFormatter.cs:9-89</c>：反斜杠、反引号、* _ { } [ ] &lt; &gt; ~ | &amp; 恒转义；
/// 行首 # &gt; + - = 空格 额外转义；形如 1. / 1) 的序号分隔符也转义。
/// 纯函数，无 IO —— 便于单测覆盖"文本里带 Markdown 控制字符"这类真会遇到的输入。
/// </summary>
public static class TodoMarkdownFormatter
{
    private const string Indent = "    "; // 续行 4 空格，避免多行文本打断列表结构

    public static string FormatSegmentHeader(DateTimeOffset when) =>
        "## " + when.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>把一批条目格式化为一个批次段（无前置空行，不含段头）。</summary>
    public static string FormatItems(IEnumerable<TodoItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(FormatItem(item));
        }

        return sb.ToString();
    }

    /// <summary>单条：<c>- [x] 文本（HH:mm 完成，id: …）</c>；多行按 4 空格续行。</summary>
    public static string FormatItem(TodoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lines = Normalize(item.Text).Split('\n');
        var sb = new StringBuilder();
        sb.Append("- [").Append(item.Done ? 'x' : ' ').Append("] ");
        sb.Append(EscapeLine(lines[0]));

        // 完成时间与 id 一起写出：时间给人看，id 供幂等扫描（方案 §5.5.5）。
        if (item.CompletedAt is { } completed)
        {
            sb.Append('（').Append(completed.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture))
              .Append(" 完成，id: ").Append(item.Id).Append('）');
        }
        else
        {
            sb.Append("（id: ").Append(item.Id).Append('）');
        }

        for (var i = 1; i < lines.Length; i++)
        {
            sb.Append('\n').Append(Indent).Append(EscapeLine(lines[i]));
        }

        return sb.ToString();
    }

    /// <summary>单行转义。</summary>
    public static string EscapeLine(string? line)
    {
        line ??= string.Empty;
        if (line.Length == 0)
        {
            return line;
        }

        var body = new StringBuilder(line.Length + 8);
        foreach (var c in line)
        {
            if (IsAlwaysEscaped(c))
            {
                body.Append('\\');
            }

            body.Append(c);
        }

        // 行首若已因恒转义带上了反斜杠（例如 ">" → "\>"），不再重复加前缀。
        var text = body.ToString();
        if (NeedsLeadingEscape(line) && !text.StartsWith('\\'))
        {
            text = "\\" + text;
        }

        return text;
    }

    private static string Normalize(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

    private static bool NeedsLeadingEscape(string line)
    {
        var first = line[0];
        if (first is '#' or '>' or '+' or '-' or '=' or ' ')
        {
            return true;
        }

        // 有序列表分隔符：数字串后紧跟 '.' 或 ')'，且其后为空白或行尾
        var i = 0;
        while (i < line.Length && char.IsAsciiDigit(line[i]))
        {
            i++;
        }

        if (i == 0 || i >= line.Length)
        {
            return false;
        }

        if (line[i] is not ('.' or ')'))
        {
            return false;
        }

        return i + 1 >= line.Length || line[i + 1] is ' ' or '\t';
    }

    private static bool IsAlwaysEscaped(char c) =>
        c is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '<' or '>' or '~' or '|' or '&';
}
