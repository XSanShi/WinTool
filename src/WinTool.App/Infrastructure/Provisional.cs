namespace WinTool.App.Infrastructure;

/// <summary>
/// 本文件集中存放"方案里还没确认、但程序必须有个具体数字才能跑起来"的取值。
/// 每个常量都标了对应的待确认项编号；确认后**只改这里**，不要散落到各处。
/// 这些不是设计决定，是占位实现（方案 §9 待确认清单 D3a–D3e / D7）。
/// </summary>
internal static class Provisional
{
    // ── D3a 待确认：悬浮球尺寸 ──────────────────────────────────────────
    public const double BubbleSize = 52;

    // ── D3b 待确认：贴边位置（左右）/ 距屏边间距 ──────────────────────────
    public const double BubbleEdgeMargin = 12;

    // ── D3e 待确认：唤出主窗口的全局热键 ─────────────────────────────────
    public const string HotkeyDisplay = "Ctrl+Alt+W";
    public const uint HotkeyModifiers = 0x0002 | 0x0001; // MOD_CONTROL | MOD_ALT
    public const uint HotkeyVirtualKey = 0x57;           // 'W'

    // ── D7 待确认：数据根目录（当前用"程序目录\data"，不可写时回退 %LOCALAPPDATA%）──
    public const string DataDirectoryName = "data";

    // ── D2b 已确认方向、D2b-2 未定：留底目录默认在程序目录下新建 ────────────
    public const string DefaultArchiveDirectoryName = "待办留底";

    public const string LocalAppDataFolderName = "WinTool";
}
