using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using WinTool.App.Infrastructure;

namespace WinTool.App.Views;

/// <summary>
/// 主窗口：深色玻璃工作台 + 顶部胶囊导航。关闭=隐藏到悬浮球，
/// 退出只在托盘/热键。这里不使用参考项目的屏幕顶部刘海。
/// </summary>
internal sealed class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    private readonly ListBox _nav = new();
    private readonly ContentControl _content = new();
    private readonly TodoPage _todoPage;
    private readonly CredentialPage _credentialPage;
    private readonly NotePage _notePage;
    private readonly SettingsPage _settingsPage;
    private readonly TextBlock _hotkeyHint = new();
    private readonly Path[] _navIcons = new Path[4];

    private bool _allowExit;

    public MainWindow(AppServices services)
    {
        Title = "WinTool";
        Width = 1080;
        Height = 700;
        MinWidth = 940;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ui.Brush("Brush.Page");
        Foreground = Ui.Brush("Brush.Text");
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

        _todoPage = new TodoPage(services);
        _credentialPage = new CredentialPage(services);
        _notePage = new NotePage(services);
        _settingsPage = new SettingsPage(services);

        Content = BuildLayout();
        ShowPage();

        SourceInitialized += (_, _) => ApplyDarkWindowChrome();
        Closing += (_, e) =>
        {
            if (_allowExit)
            {
                return;
            }

            e.Cancel = true;
            HideToBubbleRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>用户点了窗口的关闭按钮：请求收起为悬浮球。</summary>
    public event EventHandler? HideToBubbleRequested;

    private UIElement BuildLayout()
    {
        BuildNav();
        var header = BuildTopBar();

        _content.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _content.VerticalContentAlignment = VerticalAlignment.Stretch;

        var shell = new Grid { Margin = new Thickness(24, 14, 24, 22) };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        Grid.SetRow(_content, 1);
        shell.Children.Add(header);
        shell.Children.Add(_content);

        var root = new Grid { Background = Ui.Brush("Brush.Page") };
        root.Children.Add(BuildAtmosphere());
        root.Children.Add(shell);
        return root;
    }

    private static UIElement BuildAtmosphere()
    {
        var layer = new Grid
        {
            IsHitTestVisible = false,
            ClipToBounds = true,
        };

        layer.Children.Add(new Ellipse
        {
            Width = 760,
            Height = 540,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-280, -300, 0, 0),
            Fill = Glow(Color.FromArgb(0x26, 0x43, 0x8C, 0xFF)),
        });
        layer.Children.Add(new Ellipse
        {
            Width = 560,
            Height = 420,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -280, -190, 0),
            Fill = Glow(Color.FromArgb(0x12, 0xFF, 0x93, 0x52)),
        });
        return layer;
    }

    private static RadialGradientBrush Glow(Color color) => new()
    {
        GradientStops = new GradientStopCollection
        {
            new(color, 0),
            new(Color.FromArgb(0, color.R, color.G, color.B), 1),
        },
    };

    private UIElement BuildTopBar()
    {
        var brandMark = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(9),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush(
                Color.FromRgb(0x31, 0x31, 0x37),
                Color.FromRgb(0x13, 0x13, 0x16),
                45),
            Child = Ui.Icon(Icons.Check, 15, Ui.Brush("Brush.Text")),
            VerticalAlignment = VerticalAlignment.Center,
            Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Shadow.Button"],
        };

        var brand = Ui.Row(9,
            brandMark,
            Ui.Stack(0,
                new TextBlock
                {
                    Text = "WinTool",
                    FontSize = 13.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = Ui.Brush("Brush.Text"),
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = "本地工作台",
                    FontSize = 10.5,
                    Foreground = Ui.Brush("Brush.TextTertiary"),
                    VerticalAlignment = VerticalAlignment.Center,
                }));
        brand.VerticalAlignment = VerticalAlignment.Center;

        var navBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x18, 0x18, 0x1B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(2),
            Child = _nav,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _hotkeyHint.FontSize = 11;
        _hotkeyHint.Foreground = Ui.Brush("Brush.TextMuted");
        _hotkeyHint.VerticalAlignment = VerticalAlignment.Center;
        _hotkeyHint.Margin = new Thickness(6, 0, 0, 0);

        var hotkeyChip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(17),
            Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    Ui.Icon(Icons.Keyboard, 12, Ui.Brush("Brush.TextTertiary")),
                    _hotkeyHint,
                },
            },
        };

        var hideButton = new Button
        {
            Content = Ui.Row(5,
                Ui.Icon(Icons.ChevronDown, 12, Ui.Brush("Brush.TextMuted")),
                new TextBlock
                {
                    Text = "收起",
                    FontSize = 11.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = Ui.Brush("Brush.Text"),
                }),
            Style = Ui.Style("Btn.Base"),
            Padding = new Thickness(10, 5, 10, 5),
            MinHeight = 32,
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "关闭后仍在托盘与悬浮球上，随时可以唤回。",
        };
        hideButton.Click += (_, _) => HideToBubbleRequested?.Invoke(this, EventArgs.Empty);
        AutomationProperties.SetAutomationId(hideButton, "HideToBubble");

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { hotkeyChip, hideButton },
        };

        var header = new Grid { Height = 42, Margin = new Thickness(0, 0, 0, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(brand, 0);
        Grid.SetColumn(navBorder, 1);
        Grid.SetColumn(actions, 2);
        header.Children.Add(brand);
        header.Children.Add(navBorder);
        header.Children.Add(actions);
        return header;
    }

    private void BuildNav()
    {
        _nav.ItemContainerStyle = Ui.Style("NavItem");
        _nav.ItemsPanel = (ItemsPanelTemplate)Application.Current.Resources["NavItemsPanel"];
        _nav.BorderThickness = new Thickness(0);
        _nav.Background = Brushes.Transparent;
        _nav.Margin = new Thickness(0);
        _nav.Items.Add(BuildNavItem(0, "待办"));
        _nav.Items.Add(BuildNavItem(1, "账号密码"));
        _nav.Items.Add(BuildNavItem(2, "便签"));
        _nav.Items.Add(BuildNavItem(3, "设置"));
        _nav.SelectedIndex = 0;
        AutomationProperties.SetAutomationId(_nav, "NavList");
        _nav.SelectionChanged += (_, _) =>
        {
            PaintNavIcons();
            ShowPage();
        };
        PaintNavIcons();
    }

    private ListBoxItem BuildNavItem(int index, string label)
    {
        var icon = Ui.Icon(NavIcon(index), 14.5);
        _navIcons[index] = icon;
        var text = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            FontWeight = FontWeights.Medium,
            Foreground = Ui.Brush("Brush.TextMuted"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        return new ListBoxItem
        {
            Style = Ui.Style("NavItem"),
            Content = Ui.Row(6, icon, text),
        };
    }

    private static Geometry NavIcon(int index) => index switch
    {
        1 => Icons.Key,
        2 => Icons.Note,
        3 => Icons.Settings,
        _ => Icons.Todo,
    };

    private void PaintNavIcons()
    {
        for (var i = 0; i < _nav.Items.Count; i++)
        {
            if (_nav.Items[i] is not ListBoxItem item || item.Content is not StackPanel row)
            {
                continue;
            }

            var selected = i == _nav.SelectedIndex;
            _navIcons[i].Stroke = selected ? Ui.Brush("Brush.Text") : Ui.Brush("Brush.TextTertiary");
            if (row.Children.Count > 1 && row.Children[1] is TextBlock label)
            {
                label.Foreground = selected ? Ui.Brush("Brush.Text") : Ui.Brush("Brush.TextMuted");
                label.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Medium;
            }
        }
    }

    public void SetHotkeyStatus(string text)
    {
        _hotkeyHint.Text = text;
        _settingsPage.SetHotkeyStatus(text.Contains("未生效", StringComparison.Ordinal)
            ? "快捷键注册未生效，可使用悬浮球唤出。"
            : "随时唤出或收起主窗口。");
    }

    private void ShowPage()
    {
        switch (_nav.SelectedIndex)
        {
            case 1:
                _content.Content = _credentialPage;
                _credentialPage.Refresh();
                break;
            case 2:
                _content.Content = _notePage;
                _notePage.Refresh();
                break;
            case 3:
                _content.Content = _settingsPage;
                _settingsPage.Refresh();
                break;
            default:
                _content.Content = _todoPage;
                _todoPage.Refresh();
                break;
        }
    }

    /// <summary>托盘/热键唤出：显示并刷新当前页（留底目录可能在设置页被改过）。</summary>
    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        ShowPage();
    }

    /// <summary>真正退出前放开 Closing 拦截。</summary>
    public void AllowExit() => _allowExit = true;

    private void ApplyDarkWindowChrome()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var dark = 1;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));

        // Windows 11 honors these; Windows 10 ignores them and keeps the dark system caption.
        var caption = 0x00060505;
        var text = 0x00F8F7F7;
        _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmTextColor, ref text, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
