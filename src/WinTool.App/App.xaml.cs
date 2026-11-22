using System.IO;
using System.Windows;
using System.Windows.Threading;
using WinTool.App.Infrastructure;
using WinTool.App.Views;
using WinTool.Core.Todo;

namespace WinTool.App;

/// <summary>
/// 组合根：单实例 → 路径/数据 → 主窗口 + 悬浮球 + 托盘 + 全局热键。
/// 方案 §8.5：所有窗口都是独立 Window，隐藏只 Hide()，退出只有一条路径（托盘"退出"）。
/// </summary>
public partial class App : Application
{
    private SingleInstance? _instance;
    private AppServices? _services;
    private MainWindow? _main;
    private BubbleWindow? _bubble;
    private TrayHost? _tray;
    private HotkeyHost? _hotkey;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!SingleInstance.TryAcquire(OnActivateRequest, out _instance))
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            _services = AppServices.Create();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException)
        {
            MessageBox.Show(
                "初始化数据目录失败：\n" + ex.Message,
                "Win_Tool",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        _main = new MainWindow(_services);
        _main.HideToBubbleRequested += (_, _) => HideToBubble();

        _bubble = new BubbleWindow();
        _bubble.Clicked += (_, _) => ShowMain();

        _tray = new TrayHost(ShowMain, HideToBubble, ExitApp);

        _hotkey = new HotkeyHost();
        _hotkey.Pressed += (_, _) => ToggleMain();
        var hotkeyError = _hotkey.Register(Provisional.HotkeyModifiers, Provisional.HotkeyVirtualKey);
        _main.SetHotkeyStatus(hotkeyError is null
            ? $"热键 {Provisional.HotkeyDisplay}"
            : $"热键 {Provisional.HotkeyDisplay} 未生效");

        ShowMain();
        ShowFirstRunNoticeIfNeeded();
    }

    /// <summary>
    /// 首次运行告知"文件落在哪"（只提示一次，写回 settings.json 的 WelcomeShown）。
    /// 用托盘气泡而不是模态框：不阻塞启动、不抢全屏程序的焦点。
    /// </summary>
    private void ShowFirstRunNoticeIfNeeded()
    {
        if (_services is null || _tray is null || _services.Settings.WelcomeShown)
        {
            return;
        }

        var paths = _services.Paths;
        var message = "数据根：" + paths.DataRoot + Environment.NewLine + "留底目录：" + paths.ArchiveRoot;
        if (paths.DataRootIsFallback)
        {
            message += Environment.NewLine + "（程序目录不可写，已回退到上面位置）";
        }

        message += Environment.NewLine + $"唤出/收起：{Provisional.HotkeyDisplay} 或点屏幕边缘的悬浮球";

        try
        {
            _tray.ShowInfo("WinTool 已启动", message);
        }
        catch (Exception)
        {
            // 通知失败不影响使用（例如系统关闭了通知）。
        }

        _services.Settings.WelcomeShown = true;
        try
        {
            _services.SettingsStore.Save(_services.Settings);
        }
        catch (IOException)
        {
            // 只是提示标记，写不进去下次再提示一次，不影响使用。
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "程序遇到未处理的异常：\n" + e.Exception.Message,
            "Win_Tool",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnActivateRequest()
    {
        // 命名管道回调在后台线程上，窗口操作必须回到 UI 线程。
        _ = Dispatcher.BeginInvoke(ShowMain, DispatcherPriority.Normal);
    }

    private void ShowMain()
    {
        if (_main is null || _exiting)
        {
            return;
        }

        _bubble?.Hide();
        _main.ShowAndActivate();
    }

    private void HideToBubble()
    {
        if (_main is null || _services is null || _exiting)
        {
            return;
        }

        _main.Hide();
        _bubble?.UpdateCount(TodoRules.CountRemaining(_services.Todos.Items));
        _bubble?.ShowBubble();
    }

    private void ToggleMain()
    {
        if (_main is null)
        {
            return;
        }

        if (_main.IsVisible && _main.IsActive)
        {
            HideToBubble();
        }
        else
        {
            ShowMain();
        }
    }

    private void ExitApp()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _main?.AllowExit();
        _hotkey?.Dispose();
        _tray?.Dispose();
        _instance?.Dispose();
        _main?.Close();
        _bubble?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
