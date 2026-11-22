using System.Runtime.InteropServices;
using System.Windows;

namespace WinTool.App.Infrastructure;

/// <summary>
/// 取显示器工作区（已排除任务栏）。方案 §8.5 第 2 条：不要自己按屏幕尺寸猜任务栏，
/// 一律问系统要 rcWork；多显示器时按窗口/坐标所属的那块屏算（D3c 未定前先按此实现）。
/// 返回值单位是物理像素。
/// </summary>
internal static partial class NativeMonitor
{
    private const uint MonitorDefaultToNearest = 2;

    public static Rect GetWorkAreaPixelsAt(int x, int y)
    {
        var point = new POINT { X = x, Y = y };
        var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        return GetWorkArea(monitor);
    }

    public static Rect GetWorkAreaPixelsForWindow(IntPtr hwnd)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        return GetWorkArea(monitor);
    }

    private static Rect GetWorkArea(IntPtr monitor)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            // 拿不到就退回主屏工作区（WPF 的 WorkArea 已是 DIP，这里换算成像素保持单位一致）。
            var work = SystemParameters.WorkArea;
            return new Rect(work.X, work.Y, work.Width, work.Height);
        }

        var r = info.rcWork;
        return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
