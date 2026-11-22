using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using WinTool.App.Views;

namespace WinTool.App.Infrastructure;

/// <summary>
/// 托盘图标（方案 §1 的"隐藏后仍可唤出"、§3.1）：左键单击唤出主窗口，
/// 右键菜单提供"打开 / 隐藏到悬浮球 / 退出"。退出入口只在这里和悬浮球菜单里出现。
/// </summary>
internal sealed class TrayHost : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayHost(Action show, Action hideToBubble, Action exit)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "WinTool — 待办 / 账号密码 / 随笔",
            Icon = TrayIcon.Create(),
        };

        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("打开主窗口", show));
        menu.Items.Add(MenuItem("隐藏到悬浮球", hideToBubble));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("退出", exit));
        _icon.ContextMenu = menu;

        _icon.TrayLeftMouseUp += (_, _) => show();
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    private static MenuItem MenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>
    /// 托盘气泡通知。首次运行用它告知数据位置：不阻塞、不抢焦点，
    /// 比模态 MessageBox 更合适（用户可能在跑全屏程序）。
    /// </summary>
    public void ShowInfo(string title, string message) =>
        _icon.TrayIcon.ShowNotification(title, message, H.NotifyIcon.Core.NotificationIcon.Info);

    public void Dispose() => _icon.Dispose();
}
