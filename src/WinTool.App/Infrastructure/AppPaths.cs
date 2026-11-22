using System.IO;
using WinTool.Core.Storage;

namespace WinTool.App.Infrastructure;

/// <summary>
/// 路径解析。方案 §4.1 数据根（D7 待确认）与 §5.5.1 留底目录（D2b 默认在程序目录下新建，
/// 用户可改；D2b-2 回退策略未定 → 这里先做"不可写就回退 %LOCALAPPDATA% 并在界面上写明"）。
/// </summary>
internal sealed record AppPaths(
    string DataRoot,
    string ArchiveRoot,
    bool DataRootIsFallback,
    bool ArchiveRootIsFallback)
{
    public string TodosPath => Path.Combine(DataRoot, "todos.json");

    public string VaultPath => Path.Combine(DataRoot, "vault.json");

    public string NotesPath => Path.Combine(DataRoot, "notes.json");

    public string SettingsPath => Path.Combine(DataRoot, "settings.json");

    public static AppPaths Resolve(string? archiveDirectoryFromSettings)
    {
        var programDir = AppContext.BaseDirectory;

        // 数据根：优先程序目录（绿色目录，随程序走），不可写再回退。D7 定案后改这里。
        var dataInProgram = Path.Combine(programDir, Provisional.DataDirectoryName);
        var dataRoot = dataInProgram;
        var dataFallback = false;
        if (!AtomicFile.CanWriteToDirectory(dataInProgram))
        {
            dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Provisional.LocalAppDataFolderName,
                Provisional.DataDirectoryName);
            dataFallback = true;
        }

        Directory.CreateDirectory(dataRoot);

        // 留底目录：用户手填优先；否则程序目录下新建（D2b 已确认的方向）。
        var archiveRoot = string.Empty;
        var archiveFallback = false;

        if (!string.IsNullOrWhiteSpace(archiveDirectoryFromSettings) &&
            AtomicFile.CanWriteToDirectory(archiveDirectoryFromSettings!))
        {
            archiveRoot = Path.GetFullPath(archiveDirectoryFromSettings!);
        }
        else
        {
            var archiveInProgram = Path.Combine(programDir, Provisional.DefaultArchiveDirectoryName);
            if (AtomicFile.CanWriteToDirectory(archiveInProgram))
            {
                archiveRoot = archiveInProgram;
            }
            else
            {
                archiveRoot = Path.Combine(dataRoot, Provisional.DefaultArchiveDirectoryName);
                archiveFallback = true;
            }
        }

        return new AppPaths(dataRoot, archiveRoot, dataFallback, archiveFallback);
    }
}
