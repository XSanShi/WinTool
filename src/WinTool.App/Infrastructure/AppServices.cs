using System.IO;
using WinTool.Core.Archive;
using WinTool.Core.Credential;
using WinTool.Core.Model;
using WinTool.Core.Notes;
using WinTool.Core.Todo;

namespace WinTool.App.Infrastructure;

/// <summary>
/// 手工装配的依赖容器（方案 §8.4：不引入 DI 框架）。数据只在这里读写一次，
/// UI 层改完内存模型后调 SaveXxx，不各自拼路径。
/// </summary>
internal sealed class AppServices
{
    private AppServices(AppPaths paths, AppSettings settings, SettingsStore settingsStore)
    {
        Paths = paths;
        Settings = settings;
        SettingsStore = settingsStore;
        TodoStore = new TodoStore();
        Notes = new NoteStore();
        CredentialStore = new PlaintextJsonCredentialStore(paths.VaultPath);
        ArchiveWriter = new TodoArchiveWriter(new TodoArchiveOptions { Directory = paths.ArchiveRoot });
        CleanupService = new TodoCleanupService(ArchiveWriter, TodoStore);
    }

    public AppPaths Paths { get; private set; }

    public AppSettings Settings { get; }

    public SettingsStore SettingsStore { get; }

    public TodoStore TodoStore { get; }

    public TodoDocument Todos { get; private set; } = new();

    public TodoArchiveWriter ArchiveWriter { get; private set; }

    public TodoCleanupService CleanupService { get; private set; }

    public PlaintextJsonCredentialStore CredentialStore { get; }

    public CredentialDocument CredentialDoc { get; private set; } = new();

    public NoteStore Notes { get; }

    public NoteDocument NoteDoc { get; private set; } = new();

    /// <summary>vault 的 ACL 收紧结果（D1a 补偿动作第 3 条）：失败只提示，不阻断。</summary>
    public string? VaultAclWarning { get; private set; }

    public static AppServices Create()
    {
        var settingsStore = new SettingsStore(Path.Combine(ResolveBootstrapDataRoot(), "settings.json"));
        var settings = settingsStore.Load();

        // 设置里存的是"用户想用的留底目录"；解析完再回写一次规范化后的真实路径。
        var paths = AppPaths.Resolve(settings.ArchiveDirectory);
        var services = new AppServices(paths, settings, settingsStore);

        // 设置文件可能位于回退后的数据根，此时用新的数据根重建一次（保持单一真相）。
        if (!string.Equals(Path.GetDirectoryName(settingsStore.Path), paths.DataRoot, StringComparison.OrdinalIgnoreCase))
        {
            settingsStore = new SettingsStore(Path.Combine(paths.DataRoot, "settings.json"));
            settings = settingsStore.Load();
            paths = AppPaths.Resolve(settings.ArchiveDirectory);
            services = new AppServices(paths, settings, settingsStore);
        }

        services.Todos = services.TodoStore.Load(paths.TodosPath);
        services.CredentialDoc = services.CredentialStore.Load();
        services.NoteDoc = services.Notes.Load(paths.NotesPath);
        services.RefreshVaultAcl();
        return services;
    }

    private static string ResolveBootstrapDataRoot() => AppPaths.Resolve(null).DataRoot;

    public void SaveTodos() => TodoStore.Save(Paths.TodosPath, Todos, DateTimeOffset.Now);

    public void SaveNotes() => Notes.Save(Paths.NotesPath, NoteDoc, DateTimeOffset.Now);

    public void SaveCredentials()
    {
        CredentialStore.Save(CredentialDoc);
        RefreshVaultAcl();
    }

    private void RefreshVaultAcl()
    {
        var (outcome, error) = VaultAcl.Tighten(Paths.VaultPath);
        VaultAclWarning = outcome switch
        {
            AclOutcome.Applied => null,
            AclOutcome.NotCreated => null,
            _ => $"无法收紧 vault.json 的访问权限（{error}）。文件仍按明文保存，请自行确认所在目录的权限。",
        };
    }

    /// <summary>改留底目录：写设置 + 重建写入器（方案 §5.5.1）。</summary>
    public void SetArchiveDirectory(string? directory)
    {
        Settings.ArchiveDirectory = string.IsNullOrWhiteSpace(directory) ? null : directory.Trim();
        SettingsStore.Save(Settings);
        Paths = AppPaths.Resolve(Settings.ArchiveDirectory);
        ArchiveWriter = new TodoArchiveWriter(new TodoArchiveOptions { Directory = Paths.ArchiveRoot });
        CleanupService = new TodoCleanupService(ArchiveWriter, TodoStore);
    }
}
