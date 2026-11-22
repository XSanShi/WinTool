using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinTool.App.Views;

/// <summary>代码构建界面时的小工具，集中处理深色玻璃工作台的重复视觉。</summary>
internal static class Ui
{
    public static Style Style(string key) => (Style)Application.Current.Resources[key];

    public static TextBlock Title(string text) => new() { Text = text, Style = Style("Text.PageTitle") };

    /// <summary>页面级标题。</summary>
    public static TextBlock IosTitle(string text) => new() { Text = text, Style = Style("Text.PageTitle") };

    public static TextBlock Subtitle(string text) => new() { Text = text, Style = Style("Text.Subtitle") };

    public static TextBlock Body(string text) => new() { Text = text, Style = Style("Text.Body") };

    public static TextBlock Muted(string text) => new() { Text = text, Style = Style("Text.Muted") };

    public static TextBlock Footnote(string text) => new() { Text = text, Style = Style("Text.Footnote") };

    public static TextBlock SectionLabel(string text) => new() { Text = text, Style = Style("Text.SectionLabel") };

    public static Button Primary(string text, RoutedEventHandler onClick)
    {
        var button = new Button { Content = text, Style = Style("Btn.Primary") };
        button.Click += onClick;
        return button;
    }

    public static Button Base(string text, RoutedEventHandler onClick)
    {
        var button = new Button { Content = text, Style = Style("Btn.Base") };
        button.Click += onClick;
        return button;
    }

    public static Button Ghost(string text, RoutedEventHandler onClick)
    {
        var button = new Button { Content = text, Style = Style("Btn.Ghost") };
        button.Click += onClick;
        return button;
    }

    public static Button Link(string text, RoutedEventHandler onClick)
    {
        var button = new Button { Content = text, Style = Style("Btn.Link") };
        button.Click += onClick;
        return button;
    }

    public static Button IconButton(Geometry geometry, string toolTip, RoutedEventHandler onClick, bool accent = false, double size = 16)
    {
        var button = new Button
        {
            Style = Style(accent ? "Btn.IconAccent" : "Btn.Icon"),
            Content = Icon(geometry, size, accent ? Brushes.White : Brush("Brush.Text")),
            ToolTip = toolTip,
        };
        AutomationProperties.SetName(button, toolTip);
        button.Click += onClick;
        return button;
    }

    public static Button CircleIconButton(Geometry geometry, string toolTip, RoutedEventHandler onClick, double size = 16)
    {
        var button = new Button
        {
            Style = Style("Btn.CircleAccent"),
            Content = Icon(geometry, size, Brushes.White),
            ToolTip = toolTip,
        };
        AutomationProperties.SetName(button, toolTip);
        button.Click += onClick;
        return button;
    }

    /// <summary>细线图标，线宽贴近参考面板的克制风格。</summary>
    public static Path Icon(Geometry geometry, double size = 16, Brush? brush = null) => new()
    {
        Data = geometry,
        Width = size,
        Height = size,
        Stretch = Stretch.Uniform,
        Stroke = brush ?? Brush("Brush.Accent"),
        StrokeThickness = 1.65,
        Fill = Brushes.Transparent,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    /// <summary>深色状态 chip。</summary>
    public static Border Chip(string text, Brush? background = null, Brush? foreground = null)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground ?? Brush("Brush.TextMuted"),
        };
        return new Border
        {
            Background = background ?? Brush("Brush.Fill"),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = label,
        };
    }

    public static Border StatusDot(Brush brush, double size = 7) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = brush,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 1, 0, 0),
    };

    /// <summary>深色圆角图标底，用于设置行和分组列表。</summary>
    public static Border IconTile(Geometry geometry, Brush background, Brush foreground, double size = 34)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.3),
            Background = background,
            Child = Icon(geometry, size * 0.47, foreground),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>列表首字母徽标（账号密码 / 随笔共用）。</summary>
    public static Border InitialBadge(string text, Brush background, Brush foreground, double size = 36)
    {
        var initial = string.IsNullOrWhiteSpace(text)
            ? "?"
            : text.TrimStart()[..1].ToUpperInvariant();
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.3),
            Background = background,
            Child = new TextBlock
            {
                Text = initial,
                FontSize = size * 0.42,
                FontWeight = FontWeights.Bold,
                Foreground = foreground,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>空列表 / 空编辑器的统一提示。</summary>
    public static StackPanel EmptyState(Geometry icon, string title, string detail)
    {
        var tile = new Border
        {
            Width = 50,
            Height = 50,
            CornerRadius = new CornerRadius(15),
            Background = Brush("Brush.Fill"),
            BorderBrush = Brush("Brush.Border"),
            BorderThickness = new Thickness(1),
            Child = Icon(icon, 22, Brush("Brush.TextTertiary")),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 330,
            Children =
            {
                tile,
                new TextBlock
                {
                    Text = title,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("Brush.Text"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 13, 0, 4),
                },
                new TextBlock
                {
                    Text = detail,
                    FontSize = 11.5,
                    LineHeight = 18,
                    Foreground = Brush("Brush.TextMuted"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            },
        };
    }

    /// <summary>深色开关，开启态使用参考面板的绿色。</summary>
    public static ToggleButton IosToggle() => new() { Style = Style("IosToggle") };

    public static TextBox Input(string? text = null, bool multiline = false, bool acceptsReturn = false) => new()
    {
        Text = text ?? string.Empty,
        Style = Style("Input"),
        AcceptsReturn = acceptsReturn,
        TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
        MinHeight = multiline ? 120 : 36,
    };

    /// <summary>输入框 + 灰色占位文字；返回的 Grid 可直接放进卡片。</summary>
    public static Grid InputWithPlaceholder(TextBox box, string placeholder, Thickness? margin = null)
    {
        var hint = new TextBlock
        {
            Text = placeholder,
            FontSize = box.FontSize,
            Foreground = Brush("Brush.TextTertiary"),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Margin = new Thickness(2, 0, 0, 0),
        };

        void Sync()
        {
            hint.Visibility = box.Text.Length == 0 && !box.IsKeyboardFocusWithin
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        box.TextChanged += (_, _) => Sync();
        box.IsKeyboardFocusWithinChanged += (_, _) => Sync();
        Sync();

        var host = new Grid();
        host.Children.Add(box);
        host.Children.Add(hint);
        if (margin is not null)
        {
            host.Margin = margin.Value;
        }

        return host;
    }

    public static Border Card(UIElement child, double padding = 16) => new()
    {
        Style = Style("Card"),
        Padding = new Thickness(padding),
        Child = child,
    };

    public static Border Hairline(double leftInset = 0) => new()
    {
        Height = 1,
        Background = Brush("Brush.Separator"),
        Margin = new Thickness(leftInset, 0, 0, 0),
        SnapsToDevicePixels = true,
    };

    public static StackPanel Stack(double spacing, params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child is FrameworkElement fe && i < children.Length - 1)
            {
                fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top, fe.Margin.Right, spacing);
            }

            panel.Children.Add(child);
        }

        return panel;
    }

    public static StackPanel Row(double spacing, params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child is FrameworkElement fe)
            {
                if (i > 0)
                {
                    fe.Margin = new Thickness(spacing, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
                }

                if (fe.VerticalAlignment == VerticalAlignment.Stretch)
                {
                    fe.VerticalAlignment = VerticalAlignment.Center;
                }
            }

            panel.Children.Add(child);
        }

        return panel;
    }

    public static Border Separator() => new() { Style = Style("Separator") };

    public static SolidColorBrush Brush(string key) => (SolidColorBrush)Application.Current.Resources[key];
}
