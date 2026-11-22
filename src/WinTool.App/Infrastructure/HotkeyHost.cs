using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WinTool.App.Infrastructure;

/// <summary>
/// 全局热键（方案 §8.5 第 5 条）：用 RegisterHotKey + 一个 message-only 窗口收 WM_HOTKEY。
/// 注册失败（错误码 1409 = 已被别的程序占用）不阻断启动，只把结果交给调用方去提示。
/// </summary>
internal sealed partial class HotkeyHost : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x5754; // 'WT'
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly HwndSource _source;
    private bool _registered;

    public HotkeyHost()
    {
        var parameters = new HwndSourceParameters("WinTool.HotkeySink")
        {
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE：只收消息，不占屏幕
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>按下热键时触发（已在 UI 线程上）。</summary>
    public event EventHandler? Pressed;

    /// <summary>注册结果：成功返回 null，失败返回给人看的原因。</summary>
    public string? Register(uint modifiers, uint virtualKey)
    {
        if (_registered)
        {
            return null;
        }

        if (RegisterHotKey(_source.Handle, HotkeyId, modifiers, virtualKey))
        {
            _registered = true;
            return null;
        }

        var error = Marshal.GetLastWin32Error();
        return error == ErrorHotkeyAlreadyRegistered
            ? "该快捷键已被其他程序占用，请换一个（方案 D3e 未定前不改默认值）。"
            : $"注册全局热键失败（Win32 错误码 {error}）。";
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            _ = UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
