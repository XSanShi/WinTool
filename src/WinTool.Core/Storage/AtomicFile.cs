using System.Text;

namespace WinTool.Core.Storage;

/// <summary>
/// 原子写：临时文件 → 写满 → flush 到磁盘 → 长度校验 → 原子替换（方案 §4.4 第 1、2 条）。
/// 任何一步失败都不会留下半个目标文件；失败向上抛，由调用方决定是"阻止删除"还是"提示重试"。
/// </summary>
public static class AtomicFile
{
    private const int DefaultAttempts = 5;
    private const int BufferSize = 16 * 1024;

    /// <summary>UTF-8，不带 BOM。是否带 BOM 待 D2f 定稿，此处集中一处便于将来改。</summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteAllText(string path, string content) =>
        WriteAllBytes(path, Utf8NoBom.GetBytes(content), DefaultAttempts);

    public static void WriteAllBytes(string path, byte[] bytes, int attempts = DefaultAttempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(bytes);

        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Exception? last = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var tmp = full + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.None))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(flushToDisk: true); // 关键：只有真 flush 才能在掉电后保证"写先于删"成立
                }

                if (new FileInfo(tmp).Length != bytes.Length)
                {
                    throw new IOException($"临时文件长度校验失败（期望 {bytes.Length}）: {tmp}");
                }

                File.Move(tmp, full, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                last = ex;
                TryDelete(tmp);
                Thread.Sleep(100 * (attempt + 1));
            }
        }

        throw new IOException($"原子写入失败: {full}", last);
    }

    /// <summary>读取文本；文件不存在返回 null（而不是空串），让调用方能区分"空文件"与"没有文件"。</summary>
    public static string? ReadAllTextOrNull(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return File.ReadAllText(path, Encoding.UTF8);
    }

    /// <summary>写前基线：长度 + 最后写入时间，用于"用户正在编辑器改它 → 不覆盖"（方案 §5.5.3）。</summary>
    public static FileBaseline? CaptureBaseline(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? new FileBaseline(info.Length, info.LastWriteTimeUtc) : null;
    }

    public static bool MatchesBaseline(string path, FileBaseline baseline)
    {
        return CaptureBaseline(path) is { } current
            && current.Length == baseline.Length
            && current.LastWriteTimeUtc == baseline.LastWriteTimeUtc;
    }

    /// <summary>可写性探测：不创建目标文件，只验证"这个目录现在能不能落盘"。</summary>
    public static bool CanWriteToDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".writetest-" + Guid.NewGuid().ToString("N")[..8]);
            using (var fs = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.WriteByte(0);
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 清理失败不影响结论：临时文件残留由下次写入重新生成新名字，不覆盖目标。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>文件基线快照。</summary>
public readonly record struct FileBaseline(long Length, DateTime LastWriteTimeUtc);
