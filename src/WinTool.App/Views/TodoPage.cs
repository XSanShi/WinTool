using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinTool.App.Infrastructure;
using WinTool.Core.Model;
using WinTool.Core.Todo;

namespace WinTool.App.Views;

/// <summary>
/// 待办页（方案 §5）：新增 / 勾选完成 / 手动清理。
/// 完成只是标记 done + completedAt，清理时才写入 Markdown 留底。
/// </summary>
internal sealed class TodoPage : UserControl
{
    private readonly AppServices _services;
    private readonly ListBox _list = new();
    private readonly TextBox _input;
    private readonly TextBlock _status = Ui.Muted(string.Empty);
    private readonly TextBlock _count = new();
    private readonly TextBlock _summaryRemaining = new();
    private readonly TextBlock _summaryCompleted = new();
    private readonly TextBlock _summaryTotal = new();
    private readonly TextBlock _progressCaption = new();
    private readonly Border _progressTrack;
    private readonly Border _progressFill;
    private readonly Grid _emptyHost = new();
    private readonly ScrollViewer _scroll;
    private readonly Button _cleanupButton;

    private double _progress;

    public TodoPage(AppServices services)
    {
        _services = services;
        _input = Ui.Input();
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                AddTodo();
                e.Handled = true;
            }
        };

        _progressFill = new Border
        {
            Height = 5,
            CornerRadius = new CornerRadius(3),
            Background = Ui.Brush("Brush.Success"),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _progressTrack = new Border
        {
            Height = 5,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF)),
            ClipToBounds = true,
            Child = _progressFill,
        };
        _progressTrack.SizeChanged += (_, _) => UpdateProgressWidth();

        _scroll = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        _cleanupButton = Ui.Link("清理已完成", (_, _) => Cleanup());
        AutomationProperties.SetAutomationId(_cleanupButton, "TodoCleanup");

        Content = BuildLayout();
        Refresh();
    }

    private UIElement BuildLayout()
    {
        _list.ItemContainerStyle = Ui.Style("RowItem");
        _list.Background = Brushes.Transparent;
        _list.BorderThickness = new Thickness(0);
        _list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _list.Margin = new Thickness(0, 4, 0, 6);
        AutomationProperties.SetAutomationId(_list, "TodoList");

        var header = BuildPageHeader();
        var overview = BuildOverviewCard();
        var listCard = BuildListCard();

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(overview, 0);
        Grid.SetColumn(listCard, 1);
        listCard.Margin = new Thickness(14, 0, 0, 0);
        body.Children.Add(overview);
        body.Children.Add(listCard);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        Grid.SetRow(body, 1);
        root.Children.Add(header);
        root.Children.Add(body);
        return root;
    }

    private UIElement BuildPageHeader()
    {
        var titleBlock = Ui.Stack(3,
            Ui.IosTitle("待办"),
            Ui.Muted("把事情写下来，让大脑轻松一点。"));

        var cleanupContent = Ui.Row(6,
            Ui.Icon(Icons.Archive, 13, Ui.Brush("Brush.Accent")),
            new TextBlock
            {
                Text = "清理已完成",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.Brush("Brush.Accent"),
            });
        _cleanupButton.Content = cleanupContent;
        _cleanupButton.Padding = new Thickness(9, 6, 4, 6);
        _cleanupButton.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(titleBlock, 0);
        Grid.SetColumn(_cleanupButton, 1);
        grid.Children.Add(titleBlock);
        grid.Children.Add(_cleanupButton);
        return grid;
    }

    private Border BuildOverviewCard()
    {
        var heading = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = "今日概览",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Ui.Brush("Brush.Text"),
                },
                new TextBlock
                {
                    Text = DateTime.Now.ToString("M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN")),
                    FontSize = 10.5,
                    Foreground = Ui.Brush("Brush.TextTertiary"),
                    Margin = new Thickness(0, 3, 0, 0),
                },
            },
        };

        var headingRow = Ui.Row(8,
            Ui.StatusDot(Ui.Brush("Brush.Success")),
            heading);

        _summaryRemaining.FontSize = 58;
        _summaryRemaining.FontWeight = FontWeights.Bold;
        _summaryRemaining.Foreground = Ui.Brush("Brush.Text");
        _summaryRemaining.LineHeight = 62;
        _summaryRemaining.Margin = new Thickness(0, 24, 0, 0);

        var remainingCaption = new TextBlock
        {
            Text = "项待完成",
            FontSize = 12,
            Foreground = Ui.Brush("Brush.TextMuted"),
            Margin = new Thickness(0, 0, 0, 20),
        };

        _progressCaption.FontSize = 10.5;
        _progressCaption.Foreground = Ui.Brush("Brush.TextTertiary");
        _progressCaption.HorizontalAlignment = HorizontalAlignment.Right;

        var progressHeading = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        progressHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progressHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var progressLabel = new TextBlock
        {
            Text = "完成度",
            FontSize = 10.5,
            Foreground = Ui.Brush("Brush.TextTertiary"),
        };
        Grid.SetColumn(progressLabel, 0);
        Grid.SetColumn(_progressCaption, 1);
        progressHeading.Children.Add(progressLabel);
        progressHeading.Children.Add(_progressCaption);

        var stats = new Grid { Margin = new Thickness(0, 20, 0, 0) };
        stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var completed = BuildStat("已完成", _summaryCompleted);
        var total = BuildStat("全部", _summaryTotal);
        Grid.SetColumn(completed, 0);
        Grid.SetColumn(total, 1);
        stats.Children.Add(completed);
        stats.Children.Add(total);

        _status.Margin = new Thickness(0, 14, 0, 0);
        _status.FontSize = 10.5;
        _status.MaxHeight = 54;
        _status.ToolTip = "留底目录：" + _services.Paths.ArchiveRoot;

        var localNote = Ui.Row(6,
            Ui.Icon(Icons.Lock, 11, Ui.Brush("Brush.TextTertiary")),
            new TextBlock
            {
                Text = "本地保存 · Markdown 留底",
                FontSize = 10,
                Foreground = Ui.Brush("Brush.TextTertiary"),
            });

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(headingRow, 0);
        Grid.SetRow(_summaryRemaining, 1);
        Grid.SetRow(remainingCaption, 2);
        Grid.SetRow(progressHeading, 3);
        Grid.SetRow(_progressTrack, 4);
        Grid.SetRow(stats, 5);
        Grid.SetRow(_status, 7);
        Grid.SetRow(localNote, 8);
        _progressTrack.VerticalAlignment = VerticalAlignment.Top;
        _progressTrack.Margin = new Thickness(0, 0, 0, 2);
        localNote.Margin = new Thickness(0, 18, 0, 0);
        content.Children.Add(headingRow);
        content.Children.Add(_summaryRemaining);
        content.Children.Add(remainingCaption);
        content.Children.Add(progressHeading);
        content.Children.Add(_progressTrack);
        content.Children.Add(stats);
        content.Children.Add(_status);
        content.Children.Add(localNote);

        return new Border
        {
            Style = Ui.Style("Card"),
            Padding = new Thickness(20, 18, 20, 18),
            Child = content,
        };
    }

    private static UIElement BuildStat(string label, TextBlock value)
    {
        value.FontSize = 18;
        value.FontWeight = FontWeights.SemiBold;
        value.Foreground = Ui.Brush("Brush.Text");

        return new StackPanel
        {
            Children =
            {
                value,
                new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    Foreground = Ui.Brush("Brush.TextTertiary"),
                    Margin = new Thickness(0, 2, 0, 0),
                },
            },
        };
    }

    private Border BuildListCard()
    {
        var listTitle = new TextBlock { Text = "任务清单", Style = Ui.Style("Text.CardTitle") };
        var titleRow = Ui.Row(7, Ui.StatusDot(Ui.Brush("Brush.Accent")), listTitle);

        _count.FontSize = 10.5;
        _count.FontWeight = FontWeights.SemiBold;
        _count.Foreground = Ui.Brush("Brush.TextMuted");
        var countChip = new Border
        {
            Background = Ui.Brush("Brush.Fill"),
            BorderBrush = Ui.Brush("Brush.Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 8, 3),
            Child = _count,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var listHeader = new Grid { Margin = new Thickness(20, 16, 16, 12) };
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(titleRow, 0);
        Grid.SetColumn(countChip, 1);
        listHeader.Children.Add(titleRow);
        listHeader.Children.Add(countChip);

        _emptyHost.Children.Add(Ui.EmptyState(
            Icons.Todo,
            "还没有待办",
            "在下面输入一件事，按回车或点右侧的 + 添加第一项。"));
        _emptyHost.MinHeight = 150;

        var body = new Grid { MinHeight = 130 };
        body.Children.Add(_scroll);
        body.Children.Add(_emptyHost);

        var inputArea = BuildInputArea();

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var divider = Ui.Hairline();
        Grid.SetRow(listHeader, 0);
        Grid.SetRow(divider, 1);
        Grid.SetRow(body, 2);
        Grid.SetRow(inputArea, 3);
        content.Children.Add(listHeader);
        content.Children.Add(divider);
        content.Children.Add(body);
        content.Children.Add(inputArea);

        return new Border
        {
            Style = Ui.Style("Card"),
            Padding = new Thickness(0),
            Child = content,
        };
    }

    private Border BuildInputArea()
    {
        _input.BorderThickness = new Thickness(0);
        _input.Background = Brushes.Transparent;
        _input.Padding = new Thickness(4, 6, 4, 6);
        _input.MinHeight = 34;
        _input.FontSize = 13.5;
        AutomationProperties.SetAutomationId(_input, "TodoInput");

        var inputWithHint = Ui.InputWithPlaceholder(_input, "添加待办，按回车保存…");

        var addButton = Ui.CircleIconButton(Icons.Plus, "添加待办", (_, _) => AddTodo(), size: 16);
        addButton.Width = 32;
        addButton.Height = 32;
        addButton.MinHeight = 32;
        AutomationProperties.SetAutomationId(addButton, "TodoAdd");

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(inputWithHint, 0);
        Grid.SetColumn(addButton, 1);
        inputWithHint.Margin = new Thickness(7, 0, 8, 0);
        row.Children.Add(inputWithHint);
        row.Children.Add(addButton);

        var normalBorder = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
        var shell = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)),
            BorderBrush = normalBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(3),
            Margin = new Thickness(12, 10, 12, 13),
            Child = row,
        };

        _input.IsKeyboardFocusWithinChanged += (_, _) =>
        {
            shell.BorderBrush = _input.IsKeyboardFocusWithin
                ? Ui.Brush("Brush.Accent")
                : normalBorder;
        };

        return shell;
    }

    private void AddTodo()
    {
        var text = _input.Text;
        if (TodoRules.IsPlaceholder(text))
        {
            return;
        }

        try
        {
            var item = TodoRules.Create(text, DateTimeOffset.Now, _services.Todos.Items);
            _services.Todos.Items.Add(item);
            _services.SaveTodos();
            _input.Clear();
            Refresh();
        }
        catch (ArgumentException ex)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private void Toggle(TodoItem item, bool done)
    {
        if (item.Done == done)
        {
            return;
        }

        TodoRules.SetDone(item, done, DateTimeOffset.Now);
        _services.SaveTodos();
        Refresh();
    }

    private void CommitText(TodoItem item, TextBox box)
    {
        var next = TodoRules.NormalizeText(box.Text);
        if (next.Length == 0)
        {
            box.Text = item.Text;
            return;
        }

        if (next == item.Text)
        {
            return;
        }

        item.Text = next;
        _services.SaveTodos();
        Refresh();
    }

    private void Delete(TodoItem item)
    {
        _services.Todos.Items.Remove(item);
        TodoRules.NormalizeOrders(_services.Todos.Items);
        _services.SaveTodos();
        Refresh();
    }

    private void Cleanup()
    {
        var result = _services.CleanupService.CleanupCompleted(_services.Todos, _services.Paths.TodosPath);

        if (!result.Success)
        {
            SetStatus("清理失败：" + result.Error, isError: true);
            MessageBox.Show(
                Window.GetWindow(this)!,
                result.Error ?? "清理失败。",
                "清理已完成待办",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Refresh();
        SetStatus(
            result.RemovedCount == 0
                ? "没有已完成的条目需要清理。"
                : $"已清理 {result.RemovedCount} 项，Markdown 留底已写入。",
            isError: false);
        _status.ToolTip = result.FilePath;
    }

    public void Refresh()
    {
        _list.Items.Clear();

        var ordered = _services.Todos.Items
            .OrderBy(i => i.Done)
            .ThenBy(i => i.Order)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            _list.Items.Add(BuildRow(ordered[i]));
        }

        var remaining = TodoRules.CountRemaining(_services.Todos.Items);
        var completed = _services.Todos.Items.Count - remaining;
        _count.Text = $"{ordered.Count} 项";
        _summaryRemaining.Text = remaining.ToString();
        _summaryCompleted.Text = completed.ToString();
        _summaryTotal.Text = ordered.Count.ToString();
        _progress = ordered.Count == 0 ? 0 : completed / (double)ordered.Count;
        _progressCaption.Text = $"{Math.Round(_progress * 100):0}%";
        UpdateProgressWidth();
        _emptyHost.Visibility = ordered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _scroll.Visibility = ordered.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _cleanupButton.IsEnabled = completed > 0;

        SetStatus(
            ordered.Count == 0
                ? "从一件小事开始。"
                : $"{remaining} 项未完成 · {completed} 项已完成",
            isError: false);
        _status.ToolTip = "留底目录：" + _services.Paths.ArchiveRoot;
    }

    private void UpdateProgressWidth()
    {
        var available = Math.Max(0, _progressTrack.ActualWidth);
        _progressFill.Width = available * Math.Clamp(_progress, 0, 1);
    }

    private void SetStatus(string text, bool isError)
    {
        _status.Text = text;
        _status.Foreground = isError ? Ui.Brush("Brush.Danger") : Ui.Brush("Brush.TextMuted");
    }

    private UIElement BuildRow(TodoItem item)
    {
        var check = new CheckBox
        {
            IsChecked = item.Done,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Style = Ui.Style("IosCheck"),
        };
        check.Checked += (_, _) => Toggle(item, true);
        check.Unchecked += (_, _) => Toggle(item, false);

        var box = Ui.Input(item.Text);
        box.BorderThickness = new Thickness(0);
        box.Background = Brushes.Transparent;
        box.Padding = new Thickness(2, 5, 4, 5);
        box.MinHeight = 30;
        box.FontSize = 13;
        box.VerticalContentAlignment = VerticalAlignment.Center;
        if (item.Done)
        {
            box.TextDecorations = TextDecorations.Strikethrough;
            box.Foreground = Ui.Brush("Brush.TextTertiary");
        }

        box.LostFocus += (_, _) => CommitText(item, box);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitText(item, box);
                e.Handled = true;
            }
        };

        var delete = new Button
        {
            Content = Ui.Icon(Icons.Close, 12, Ui.Brush("Brush.TextTertiary")),
            Style = Ui.Style("Btn.Ghost"),
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            ToolTip = "删除这条待办（不会写留底）",
        };
        AutomationProperties.SetName(delete, "删除待办");
        ToolTipService.SetInitialShowDelay(delete, 500);
        delete.Click += (_, _) => Delete(item);

        var content = new Grid { Margin = new Thickness(9, 4, 6, 4), MinHeight = 38 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(check, 0);
        Grid.SetColumn(box, 1);
        Grid.SetColumn(delete, 2);
        content.Children.Add(check);
        content.Children.Add(box);
        content.Children.Add(delete);
        return content;
    }
}
