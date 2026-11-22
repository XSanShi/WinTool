using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinTool.Core.Storage;

namespace WinTool.App.Infrastructure;

/// <summary>应用设置。字段只放"已经能确认语义"的项；未确认的开关不要提前加进来。</summary>
internal sealed class AppSettings
{
    public int Version { get; set; } = 1;

    /// <summary>留底目录（D2b 已确认：用户可调，默认程序目录下新建）。空 = 用默认。</summary>
    public string? ArchiveDirectory { get; set; }

    /// <summary>首次运行提示是否已显示过（只影响提示，不影响任何数据行为）。</summary>
    public bool WelcomeShown { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppJsonContext : JsonSerializerContext
{
}

internal sealed class SettingsStore(string path)
{
    public string Path { get; } = path;

    public AppSettings Load()
    {
        var text = AtomicFile.ReadAllTextOrNull(Path);
        if (text is null || string.IsNullOrWhiteSpace(text))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize(text, AppJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // 设置文件被改坏不该让程序打不开：退回默认值，界面上的路径仍然可见可修。
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        AtomicFile.WriteAllText(Path, JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings));
    }
}
