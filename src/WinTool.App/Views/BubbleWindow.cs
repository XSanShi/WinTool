using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WinTool.App.Infrastructure;

namespace WinTool.App.Views;

/// <summary>
/// 贴边悬浮球（方案 §3.3）。独立窗口只 Hide/Show，位置使用物理像素。
/// </summary>
internal sealed partial class BubbleWindow : Window
{
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private static readonly IntPtr HwndTop = IntPtr.Zero;

    private readonly TextBlock _count = new()
    {
        Foreground = Brushes.White,
        FontSize = 16,
        FontWeight = FontWeights.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly Border _statusDot = new()
    {
        Width = 9,
        Height = 9,
        CornerRadius = new CornerRadius(4.5),
        Background = new SolidColorBrush(Color.FromRgb(0x30, 0xD9, 0x78)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x15)),
        BorderThickness = new Thickness(2),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 7, 7, 0),
        IsHitTestVisible = false,
    };

    private readonly Path _check = new()
    {
        Data = Icons.Check,
        Width = 22,
        Height = 22,
        Stretch = Stretch.Uniform,
        Stroke = Brushes.White,
        StrokeThickness = 2.4,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    private readonly Border _shell;
    private readonly ScaleTransform _scale = new(1, 1);

    private int _left;
    private int _top;
    private bool _positioned;
    private bool _dragging;
    private Point _dragStart;
    private int _dragStartLeft;
    private int _dragStartTop;

    public BubbleWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = Provisional.BubbleSize;
        Height = Provisional.BubbleSize;
        Cursor = Cursors.Hand;
        ToolTip = "打开 WinTool";

        _shell = BuildShell();
        _shell.RenderTransform = _scale;
        _shell.RenderTransformOrigin = new Point(0.5, 0.5);
        Content = _shell;

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        MouseEnter += (_, _) => AnimateScale(1.045, 120);
        MouseLeave += (_, _) =>
        {
            if (!IsMouseCaptured)
            {
                AnimateScale(1.0, 150);
            }
        };

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                ApplyPosition();
            }
        };
    }

    /// <summary>单击（非拖拽）时触发：唤出主窗口。</summary>
    public event EventHandler? Clicked;

    private Border BuildShell()
    {
        var highlight = new Ellipse
        {
            Width = 28,
            Height = 13,
            Fill = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8, 7, 0, 0),
            IsHitTestVisible = false,
        };

        var content = new Grid { Children = { highlight } };
        content.Children.Add(_count);
        content.Children.Add(_check);
        content.Children.Add(_statusDot);

        var inner = new Border
        {
            CornerRadius = new CornerRadius((Provisional.BubbleSize - 8) / 2),
            Background = new SolidColorBrush(Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Child = content,
        };

        return new Border
        {
            CornerRadius = new CornerRadius(Provisional.BubbleSize / 2),
            Background = new LinearGradientBrush(
                Color.FromRgb(0x36, 0x36, 0x3D),
                Color.FromRgb(0x10, 0x10, 0x13),
                45),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2.5),
            Effect = new DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 6,
                Direction = 270,
                Opacity = 0.58,
                Color = Colors.Black,
            },
            Child = inner,
        };
    }

    public void UpdateCount(int remaining)
    {
        var hasItems = remaining > 0;
        _count.Text = remaining > 99 ? "99+" : remaining.ToString();
        _count.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        _check.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        _statusDot.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>显示并把球放到贴边默认位置（首次）或上次位置。</summary>
    public void ShowBubble()
    {
        if (!_positioned)
        {
            MoveToDefaultPosition();
            _positioned = true;
        }

        Show();
        ApplyPosition();
    }

    private (int Width, int Height) PixelSize()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return (
            (int)Math.Round(Provisional.BubbleSize * dpi.DpiScaleX),
            (int)Math.Round(Provisional.BubbleSize * dpi.DpiScaleY));
    }

    private void MoveToDefaultPosition()
    {
        var work = NativeMonitor.GetWorkAreaPixelsAt(1, 1);
        var (width, height) = PixelSize();
        var margin = (int)Math.Round(Provisional.BubbleEdgeMargin * VisualTreeHelper.GetDpi(this).DpiScaleX);

        _left = (int)Math.Round(work.Right - width - margin);
        _top = (int)Math.Round(work.Top + ((work.Height - height) / 2));
    }

    private void ApplyPosition()
    {
        if (!_positioned)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        _ = SetWindowPos(hwnd, HwndTop, _left, _top, 0, 0, SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder);
    }

    private void MoveTo(int left, int top)
    {
        _left = left;
        _top = top;
        ApplyPosition();
    }

    private void AnimateScale(double target, int milliseconds)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd,
        };

        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = PointToScreen(e.GetPosition(this));
        _dragStartLeft = _left;
        _dragStartTop = _top;
        _dragging = false;
        CaptureMouse();
        AnimateScale(0.94, 90);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;

        if (!_dragging && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
        {
            _dragging = true;
        }

        if (_dragging)
        {
            MoveTo(_dragStartLeft + (int)Math.Round(dx), _dragStartTop + (int)Math.Round(dy));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        AnimateScale(IsMouseOver ? 1.045 : 1.0, 120);

        if (_dragging)
        {
            SnapToNearestEdge();
            _dragging = false;
            return;
        }

        Clicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>松手后吸附到所在显示器最近的一条竖边，并把纵向夹回工作区。</summary>
    private void SnapToNearestEdge()
    {
        var (width, height) = PixelSize();
        var centerX = _left + (width / 2);
        var centerY = _top + (height / 2);
        var work = NativeMonitor.GetWorkAreaPixelsAt(centerX, centerY);
        var dpi = VisualTreeHelper.GetDpi(this);
        var margin = (int)Math.Round(Provisional.BubbleEdgeMargin * dpi.DpiScaleX);

        var leftEdge = (int)Math.Round(work.Left) + margin;
        var rightEdge = (int)Math.Round(work.Right) - width - margin;
        var targetLeft = Math.Abs(centerX - leftEdge) <= Math.Abs(centerX - rightEdge) ? leftEdge : rightEdge;

        var minTop = (int)Math.Round(work.Top) + margin;
        var maxTop = (int)Math.Round(work.Bottom) - height - margin;
        if (maxTop < minTop)
        {
            maxTop = minTop;
        }

        var targetTop = Math.Min(Math.Max(_top, minTop), maxTop);
        MoveTo(targetLeft, targetTop);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
