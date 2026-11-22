using System.Security.Cryptography;
using WinTool.Core.Archive;
using WinTool.Core.Credential;
using WinTool.Core.Diagnostics;
using WinTool.Core.Model;
using WinTool.Core.Storage;
using WinTool.Core.Todo;
using Xunit;

namespace WinTool.Core.Tests;

/// <summary>临时目录：每个测试独立，结束删除。测试若失败也尽量清理。</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wintool-test-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal static class TestClock
{
    /// <summary>构造当前测试机器所在时区的本地墙钟时间，避免 UTC CI 与 +08:00 开发机断言不一致。</summary>
    public static DateTimeOffset Local(int year, int month, int day, int hour, int minute, int second = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}

public sealed class AtomicFileTests
{
    [Fact]
    public void 写入后可读回且不留临时文件()
    {
        using var dir = new TempDir();
        var path = dir.Combine("todos.json");

        AtomicFile.WriteAllText(path, "{\"a\":1}");

        Assert.Equal("{\"a\":1}", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp-*"));
        Assert.NotNull(AtomicFile.ReadAllTextOrNull(path));
    }

    [Fact]
    public void 文件不存在时读取返回null而不是空串()
    {
        using var dir = new TempDir();
        Assert.Null(AtomicFile.ReadAllTextOrNull(dir.Combine("missing.json")));
    }

    [Fact]
    public void 基线在文件被外部改动后不匹配()
    {
        using var dir = new TempDir();
        var path = dir.Combine("archive.md");
        File.WriteAllText(path, "v1");

        var baseline = AtomicFile.CaptureBaseline(path);
        Assert.NotNull(baseline);
        Assert.True(AtomicFile.MatchesBaseline(path, baseline!.Value));

        File.WriteAllText(path, "v1 changed externally");
        Assert.False(AtomicFile.MatchesBaseline(path, baseline.Value));
    }

    [Fact]
    public void 目标路径是目录时判定不可写()
    {
        using var dir = new TempDir();
        var filePath = dir.Combine("todos.json");
        File.WriteAllText(filePath, "x");
        Assert.False(AtomicFile.CanWriteToDirectory(filePath));
        Assert.True(AtomicFile.CanWriteToDirectory(dir.Combine("new-sub-dir")));
    }
}

public sealed class TodoMarkdownFormatterTests
{
    private static TodoItem Item(string text, string id = "0123456789abcdef0123456789abcdef", DateTimeOffset? completed = null) => new()
    {
        Id = id,
        Text = text,
        Done = true,
        CreatedAt = TestClock.Local(2026, 11, 1, 9, 0),
        CompletedAt = completed,
    };

    [Fact]
    public void Markdown控制字符一律转义()
    {
        var line = TodoMarkdownFormatter.EscapeLine("a*b_c[d]e<f>g~h|i&j`k{l}m\\n");
        Assert.Equal("a\\*b\\_c\\[d\\]e\\<f\\>g\\~h\\|i\\&j\\`k\\{l\\}m\\\\n", line);
    }

    [Theory]
    [InlineData("# 标题", "\\# 标题")]
    [InlineData("- 列表", "\\- 列表")]
    [InlineData("> 引用", "\\> 引用")]
    [InlineData("+ 加号", "\\+ 加号")]
    [InlineData("1. 有序", "\\1. 有序")]
    [InlineData("12) 有序", "\\12) 有序")]
    [InlineData("正常文本", "正常文本")]
    [InlineData("2026.11.14 版本", "2026.11.14 版本")]
    public void 行首特殊符号按规则处理(string input, string expected)
    {
        Assert.Equal(expected, TodoMarkdownFormatter.EscapeLine(input));
    }

    [Fact]
    public void 多行文本按四空格续行且不打断列表()
    {
        var text = TodoMarkdownFormatter.FormatItem(Item("第一行\n第二行", completed: TestClock.Local(2026, 11, 14, 19, 40)));

        var lines = text.Split('\n');
        Assert.StartsWith("- [x] 第一行（19:40 完成，id: 0123456789abcdef0123456789abcdef）", lines[0]);
        Assert.Equal("    第二行", lines[1]);
    }

    [Fact]
    public void 未完成条目用空复选框()
    {
        var item = Item("买牛奶");
        item.Done = false;
        Assert.StartsWith("- [ ] 买牛奶", TodoMarkdownFormatter.FormatItem(item));
    }

    [Fact]
    public void 段头使用本地时间到分钟()
    {
        var header = TodoMarkdownFormatter.FormatSegmentHeader(TestClock.Local(2026, 11, 14, 18, 57, 33));
        Assert.Equal("## 2026-11-14 18:57", header);
    }
}

public sealed class TodoRulesTests
{
    [Fact]
    public void 空白输入不建条目()
    {
        var existing = new List<TodoItem>();
        Assert.Throws<ArgumentException>(() => TodoRules.Create("   ", DateTimeOffset.Now, existing));
        Assert.True(TodoRules.IsPlaceholder("\t\n"));
    }

    [Fact]
    public void 新建条目落在末尾且未完成()
    {
        var now = TestClock.Local(2026, 11, 14, 10, 0);
        var list = new List<TodoItem>();
        list.Add(TodoRules.Create("第一条", now, list));
        list.Add(TodoRules.Create("第二条", now, list));

        Assert.Equal([0, 1], list.Select(i => i.Order));
        Assert.All(list, i => Assert.False(i.Done));
        Assert.All(list, i => Assert.Null(i.CompletedAt));
        Assert.Equal(32, list[0].Id.Length);
    }

    [Fact]
    public void 勾选记录完成时刻取消勾选默认清空()
    {
        var now = TestClock.Local(2026, 11, 14, 10, 0);
        var item = TodoRules.Create("x", now, []);

        Assert.True(TodoRules.SetDone(item, true, now.AddHours(1)));
        Assert.Equal(now.AddHours(1), item.CompletedAt);

        Assert.True(TodoRules.SetDone(item, false, now.AddHours(2)));
        Assert.Null(item.CompletedAt);

        // 幂等：重复勾选不改变状态
        Assert.True(TodoRules.SetDone(item, true, now.AddHours(3)));
        Assert.False(TodoRules.SetDone(item, true, now.AddHours(4)));
        Assert.Equal(now.AddHours(3), item.CompletedAt);
    }

    [Fact]
    public void 取消勾选可配置保留完成时刻()
    {
        var now = TestClock.Local(2026, 11, 14, 10, 0);
        var item = TodoRules.Create("x", now, []);
        TodoRules.SetDone(item, true, now);
        TodoRules.SetDone(item, false, now.AddMinutes(5), clearCompletedAtOnUndo: false);
        Assert.Equal(now, item.CompletedAt);
    }

    [Fact]
    public void 顺序规范化消除空洞()
    {
        var list = new List<TodoItem>
        {
            new() { Id = "a", Order = 7 },
            new() { Id = "b", Order = 9 },
        };
        TodoRules.NormalizeOrders(list);
        Assert.Equal([0, 1], list.Select(i => i.Order));
    }
}

public sealed class TodoArchiveWriterTests
{
    private static TodoItem Done(string id, string text, DateTimeOffset completed) => new()
    {
        Id = id,
        Text = text,
        Done = true,
        CreatedAt = completed.AddHours(-1),
        CompletedAt = completed,
    };

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [Fact]
    public void 首次清理创建当月文件并写入段头()
    {
        using var dir = new TempDir();
        var writer = new TodoArchiveWriter(new TodoArchiveOptions { Directory = dir.Path });
        var at = TestClock.Local(2026, 11, 14, 18, 57);

        var outcome = writer.Write(new TodoArchiveRequest([Done("a1", "买牛奶", at)], at));

        Assert.True(outcome.Success);
        Assert.Equal(1, outcome.WrittenCount);
        Assert.Equal(dir.Combine("待办留底-2026-11.md"), outcome.FilePath);
        var text = File.ReadAllText(outcome.FilePath!);
        Assert.StartsWith("## 2026-11-14 18:57", text);
        Assert.Contains("- [x] 买牛奶（18:57 完成，id: a1）", text);
    }

    [Fact]
    public void 同月第二次清理追加不覆盖()
    {
        using var dir = new TempDir();
        var writer = new TodoArchiveWriter(new TodoArchiveOptions { Directory = dir.Path });
        var at1 = TestClock.Local(2026, 11, 14, 18, 57);
        var at2 = at1.AddHours(3);

        writer.Write(new TodoArchiveRequest([Done("a1", "第一条", at1)], at1));
        var outcome = writer.Write(new TodoArchiveRequest([Done("a2", "第二条", at2)], at2));

        Assert.True(outcome.Success);
        Assert.Single(Directory.GetFiles(dir.Path, "*.md"));
        var text = File.ReadAllText(outcome.FilePath!);
        Assert.True(text.IndexOf("第一条", StringComparison.Ordinal) < text.IndexOf("第二条", StringComparison.Ordinal));
        Assert.Equal(2, text.Split("## ").Length - 1);
    }

    [Fact]
    public void 同一条目重复清理不重复写入()
    {
        using var dir = new TempDir();
        var writer = new TodoArchiveWriter(new TodoArchiveOptions { Directory = dir.Path });
        var at = TestClock.Local(2026, 11, 14, 18, 57);
        var item = Done("0123456789abcdef0123456789abcdef", "买牛奶", at);

        writer.Write(new TodoArchiveRequest([item], at));
        var second = writer.Write(new TodoArchiveRequest([item], at.AddMinutes(5)));

        Assert.True(second.Success);
        Assert.Equal(0, second.WrittenCount);
        Assert.Equal(1, second.SkippedCount);
        Assert.Single(File.ReadAllLines(second.FilePath!), l => l.Contains("id: 0123456789abcdef0123456789abcdef", StringComparison.Ordinal));
    }

    [Fact]
    public void 跨月新建文件且上月文件字节不变()
    {
        using var dir = new TempDir();
        var writer = new TodoArchiveWriter(new TodoArchiveOptions { Directory = dir.Path });
        var nov = TestClock.Local(2026, 11, 30, 23, 10);
        var dec = TestClock.Local(2026, 12, 1, 0, 5);

        var first = writer.Write(new TodoArchiveRequest([Done("a1", "十一月", nov)], nov));
        var novHash = Hash(first.FilePath!);
        var novMtime = File.GetLastWriteTimeUtc(first.FilePath!);

        var second = writer.Write(new TodoArchiveRequest([Done("b1", "十二月", dec)], dec));

        Assert.True(second.Success);
        Assert.EndsWith("待办留底-2026-12.md", second.FilePath);
        Assert.Equal(novHash, Hash(first.FilePath!));
        Assert.Equal(novMtime, File.GetLastWriteTimeUtc(first.FilePath!));
        Assert.False(File.ReadAllText(second.FilePath!).Contains("十一月", StringComparison.Ordinal));
    }

    [Fact]
    public void 上月已留底的条目不再写入当月_跨月幂等()
    {
        using var dir = new TempDir();
        var writer = new TodoArchiveWriter(new TodoArchiveOptions { Directory = dir.Path });
        var nov = TestClock.Local(2026, 11, 30, 23, 10);
        var dec = TestClock.Local(2026, 12, 1, 9, 0);
        var item = Done("fedcba9876543210fedcba9876543210", "上月的条目", nov);

        writer.Write(new TodoArchiveRequest([item], nov));
        var outcome = writer.Write(new TodoArchiveRequest([item], dec));

        Assert.True(outcome.Success);
        Assert.Equal(0, outcome.WrittenCount);
        Assert.Equal(1, outcome.SkippedCount);
        Assert.False(File.Exists(outcome.FilePath!));
    }

    [Fact]
    public void 目录不可写时失败且不产生文件()
    {
        using var dir = new TempDir();
        var blocked = dir.Combine("blocked");
        File.WriteAllText(blocked, "我是一个文件，不是目录");
        var writer = new TodoArchiveWriter(new TodoArchiveOptions { Directory = blocked });
        var at = DateTimeOffset.Now;

        var outcome = writer.Write(new TodoArchiveRequest([Done("a1", "x", at)], at));

        Assert.False(outcome.Success);
        Assert.NotNull(outcome.Error);
        Assert.Equal("我是一个文件，不是目录", File.ReadAllText(blocked));
    }

    [Fact]
    public void 条目为空时不碰文件系统()
    {
        using var dir = new TempDir();
        var writer = new TodoArchiveWriter(new TodoArchiveOptions { Directory = dir.Combine("never-created") });

        var outcome = writer.Write(new TodoArchiveRequest([], DateTimeOffset.Now));

        Assert.True(outcome.Success);
        Assert.False(Directory.Exists(dir.Combine("never-created")));
    }
}

public sealed class TodoCleanupServiceTests
{
    private static TodoItem Done(string id, string text) => new()
    {
        Id = id,
        Text = text,
        Done = true,
        CreatedAt = DateTimeOffset.Parse("2026-11-14T10:00:00+08:00"),
        CompletedAt = DateTimeOffset.Parse("2026-11-14T11:00:00+08:00"),
    };

    [Fact]
    public void 清理后留底存在且条目被移除()
    {
        using var dir = new TempDir();
        var todosPath = dir.Combine("todos.json");
        var archive = new TodoArchiveWriter(new TodoArchiveOptions { Directory = dir.Combine("archive") });
        var service = new TodoCleanupService(archive, new TodoStore());

        var doc = new TodoDocument();
        doc.Items.Add(Done("a1", "买牛奶"));
        doc.Items.Add(new TodoItem { Id = "b1", Text = "未完成", Order = 1 });

        var result = service.CleanupCompleted(doc, todosPath);

        Assert.True(result.Success);
        Assert.Equal(1, result.RemovedCount);
        Assert.Single(doc.Items);
        Assert.Equal("b1", doc.Items[0].Id);
        Assert.True(File.Exists(result.FilePath));

        var reloaded = new TodoStore().Load(todosPath);
        Assert.Single(reloaded.Items);
        Assert.Equal("b1", reloaded.Items[0].Id);
    }

    [Fact]
    public void 留底失败时一条都不删且待办文件不变()
    {
        using var dir = new TempDir();
        var todosPath = dir.Combine("todos.json");
        var blocked = dir.Combine("blocked");
        File.WriteAllText(blocked, "占位");
        var service = new TodoCleanupService(
            new TodoArchiveWriter(new TodoArchiveOptions { Directory = blocked }),
            new TodoStore());

        var doc = new TodoDocument();
        doc.Items.Add(Done("a1", "买牛奶"));
        doc.Items.Add(Done("a2", "交周报"));
        new TodoStore().Save(todosPath, doc, DateTimeOffset.Now);
        var before = File.ReadAllText(todosPath);

        var result = service.CleanupCompleted(doc, todosPath);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(2, doc.Items.Count);
        Assert.Equal(before, File.ReadAllText(todosPath));
    }

    [Fact]
    public void 没有已完成条目时不写任何文件()
    {
        using var dir = new TempDir();
        var todosPath = dir.Combine("todos.json");
        var archiveDir = dir.Combine("archive");
        var service = new TodoCleanupService(new TodoArchiveWriter(new TodoArchiveOptions { Directory = archiveDir }), new TodoStore());

        var doc = new TodoDocument();
        doc.Items.Add(new TodoItem { Id = "b1", Text = "未完成" });

        var result = service.CleanupCompleted(doc, todosPath);

        Assert.True(result.Success);
        Assert.Equal(0, result.RemovedCount);
        Assert.False(Directory.Exists(archiveDir));
        Assert.False(File.Exists(todosPath));
    }
}

public sealed class CredentialStoreTests
{
    [Fact]
    public void 明文往返且文件是可直接阅读的JSON()
    {
        using var dir = new TempDir();
        var path = dir.Combine("vault.json");
        var store = new PlaintextJsonCredentialStore(path);
        var doc = new CredentialDocument();
        doc.Rows.Add(new CredentialRow
        {
            Id = "r1",
            Title = "示例站点",
            Username = "me@example.com",
            Password = "p@ssw0rd",
            Url = "https://example.com",
            Notes = "恢复码 1234",
            CreatedAt = DateTimeOffset.Parse("2026-11-14T10:00:00+08:00"),
            UpdatedAt = DateTimeOffset.Parse("2026-11-14T10:00:00+08:00"),
        });

        store.Save(doc);
        var raw = File.ReadAllText(path);
        Assert.Contains("\"password\": \"p@ssw0rd\"", raw); // 明文是 D1a 的预期行为，不是缺陷
        Assert.DoesNotContain("base64", raw, StringComparison.OrdinalIgnoreCase);

        var loaded = new PlaintextJsonCredentialStore(path).Load();
        Assert.Single(loaded.Rows);
        Assert.Equal("p@ssw0rd", loaded.Rows[0].Password);
    }

    [Fact]
    public void 文件缺失时返回空集合()
    {
        using var dir = new TempDir();
        var loaded = new PlaintextJsonCredentialStore(dir.Combine("vault.json")).Load();
        Assert.Empty(loaded.Rows);
    }
}

public sealed class RedactTests
{
    [Fact]
    public void 描述里不出现账号密码原文()
    {
        var row = new CredentialRow
        {
            Id = "r1",
            Title = "示例站点",
            Username = "me@example.com",
            Password = "p@ssw0rd",
            Notes = "恢复码 1234",
        };

        var text = Redact.Describe(row);

        Assert.DoesNotContain("p@ssw0rd", text, StringComparison.Ordinal);
        Assert.DoesNotContain("me@example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1234", text, StringComparison.Ordinal);
        Assert.Contains("示例站点", text, StringComparison.Ordinal);
    }
}
