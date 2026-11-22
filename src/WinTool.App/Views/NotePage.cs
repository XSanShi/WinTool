using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinTool.App.Infrastructure;
using WinTool.Core.Model;
using WinTool.Core.Notes;

namespace WinTool.App.Views;

/// <summary>
/// 便签页（方案 §7）。当前只落 notes.json；只有显式保存或切换条目时才写库。
/// </summary>
internal sealed class NotePage : UserControl
{
    private readonly AppServices _services;
    private readonly ListBox _list = new();
    private readonly TextBox _title = Ui.Input();
    private readonly TextBox _content = Ui.Input(multiline: true, acceptsReturn: true);
    private readonly TextBlock _status = Ui.Muted(string.Empty);
    private readonly TextBlock _updated = Ui.Muted(string.Empty);
    private readonly TextBlock _listCount = new();
    private readonly ScrollViewer _listScroll;
    private readonly Grid _listHost = new();
    private readonly Grid _detailHost = new();
    private readonly Grid _editorArea = new();
    private readonly Border _editorFooter;

    private NoteItem? _current;
    private bool _loading;

    public NotePage(AppServices services)
    {
        _services = services;

        _title.BorderThickness = new Thickness(0);
        _title.Background = Brushes.Transparent;
        _title.Padding = new Thickness(0);
        _title.FontSize = 20;
        _title.FontWeight = FontWeights.Bold;
        _title.MinHeight = 38;

        _content.Background = Ui.Brush("Brush.SurfaceSecondary");
        _content.BorderBrush = Ui.Brush("Brush.Border");
        _content.Padding = new Thickness(15, 13, 15, 13);
        _content.FontSize = 13;
        _content.MinHeight = 160;
        _content.AcceptsTab = true;

        _listScroll = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        _editorFooter = BuildEditorFooter();
        Content = BuildLayout();
        Refresh();
    }

    private UIElement BuildLayout()
    {
        _list.ItemContainerStyle = Ui.Style("RowItem");
        _list.Background = Brushes.Transparent;
        _list.BorderThickness = new Thickness(0);
        _list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _list.Margin = new Thickness(0, 3, 0, 5);
        _list.SelectionChanged += (_, _) => OnSelectionChanged();

        AutomationProperties.SetAutomationId(_list, "NoteList");
        AutomationProperties.SetAutomationId(_title, "NoteTitle");
        AutomationProperties.SetAutomationId(_content, "NoteContent");

        var headerText = Ui.Stack(3,
            Ui.IosTitle("便签"),
            Ui.Muted("随手记下想法，稍后再慢慢整理。"));
        var localChip = Ui.Chip("本地文件", Ui.Brush("Brush.Fill"), Ui.Brush("Brush.TextMuted"));
        localChip.VerticalAlignment = VerticalAlignment.Center;
        var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(headerText, 0);
        Grid.SetColumn(localChip, 1);
        header.Children.Add(headerText);
        header.Children.Add(localChip);

        var listCard = BuildListCard();
        var editorCard = BuildEditorCard();
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(284) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(listCard, 0);
        Grid.SetColumn(editorCard, 1);
        editorCard.Margin = new Thickness(14, 0, 0, 0);
        body.Children.Add(listCard);
        body.Children.Add(editorCard);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        Grid.SetRow(body, 1);
        root.Children.Add(header);
        root.Children.Add(body);
        return root;
    }

    private Border BuildListCard()
    {
        var newButton = Ui.CircleIconButton(Icons.Plus, "新建便签", (_, _) => CreateNew(), size: 16);
        AutomationProperties.SetAutomationId(newButton, "NoteNew");

        var title = new TextBlock { Text = "所有便签", Style = Ui.Style("Text.CardTitle") };
        var titleRow = Ui.Row(7, Ui.StatusDot(Ui.Brush("Brush.Warning")), title);
        _listCount.FontSize = 10.5;
        _listCount.Foreground = Ui.Brush("Brush.TextMuted");
        _listCount.Margin = new Thickness(0, 2, 0, 0);

        var heading = new StackPanel { Children = { titleRow, _listCount } };
        var listHeader = new Grid { Margin = new Thickness(20, 16, 16, 12) };
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(heading, 0);
        Grid.SetColumn(newButton, 1);
        listHeader.Children.Add(heading);
        listHeader.Children.Add(newButton);

        var empty = Ui.EmptyState(
            Icons.Note,
            "还没有便签",
            "点击右上角的 +，写下第一条随手记。");
        empty.Margin = new Thickness(20);
        _listHost.Children.Add(_listScroll);
        _listHost.Children.Add(empty);
        _listHost.MinHeight = 220;

        var divider = Ui.Hairline();
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(listHeader, 0);
        Grid.SetRow(divider, 1);
        Grid.SetRow(_listHost, 2);
        content.Children.Add(listHeader);
        content.Children.Add(divider);
        content.Children.Add(_listHost);

        return new Border
        {
            Style = Ui.Style("Card"),
            Padding = new Thickness(0),
            Child = content,
        };
    }

    private Border BuildEditorCard()
    {
        var heading = Ui.Stack(2,
            Ui.Row(7,
                Ui.StatusDot(Ui.Brush("Brush.Accent")),
                new TextBlock { Text = "便签内容", Style = Ui.Style("Text.CardTitle") }),
            Ui.Muted("本地文件 · notes.json"));

        var editHint = Ui.Chip("切换便签时保存", Ui.Brush("Brush.Fill"), Ui.Brush("Brush.TextMuted"));
        editHint.VerticalAlignment = VerticalAlignment.Center;

        var editorHeader = new Grid { Margin = new Thickness(20, 16, 18, 12) };
        editorHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        editorHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(heading, 0);
        Grid.SetColumn(editHint, 1);
        editorHeader.Children.Add(heading);
        editorHeader.Children.Add(editHint);

        _editorArea.Margin = new Thickness(20, 12, 20, 12);
        _editorArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _editorArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _editorArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _editorArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_title, 0);
        Grid.SetRow(_updated, 1);
        var divider = Ui.Hairline();
        divider.Margin = new Thickness(0, 10, 0, 10);
        Grid.SetRow(divider, 2);
        Grid.SetRow(_content, 3);
        _content.Margin = new Thickness(0, 2, 0, 0);
        _editorArea.Children.Add(_title);
        _editorArea.Children.Add(_updated);
        _editorArea.Children.Add(divider);
        _editorArea.Children.Add(_content);

        var empty = Ui.EmptyState(
            Icons.Note,
            "选择一条便签",
            "在左侧选择已有便签，或点击 + 新建一条。");
        _detailHost.Children.Add(_editorArea);
        _detailHost.Children.Add(empty);

        var headerDivider = Ui.Hairline();
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(editorHeader, 0);
        Grid.SetRow(headerDivider, 1);
        Grid.SetRow(_detailHost, 2);
        var footerDivider = Ui.Hairline();
        Grid.SetRow(footerDivider, 3);
        Grid.SetRow(_editorFooter, 4);
        content.Children.Add(editorHeader);
        content.Children.Add(headerDivider);
        content.Children.Add(_detailHost);
        content.Children.Add(footerDivider);
        content.Children.Add(_editorFooter);

        return new Border
        {
            Style = Ui.Style("Card"),
            Padding = new Thickness(0),
            Child = content,
        };
    }

    private Border BuildEditorFooter()
    {
        var saveButton = Ui.Primary("保存", (_, _) => SaveCurrent());
        saveButton.Content = Ui.Row(6,
            Ui.Icon(Icons.Check, 13, Ui.Brush("Brush.Page")),
            new TextBlock
            {
                Text = "保存",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.Brush("Brush.Page"),
            });
        saveButton.Padding = new Thickness(15, 7, 16, 7);
        AutomationProperties.SetAutomationId(saveButton, "NoteSave");

        var deleteButton = Ui.Base("删除", (_, _) => DeleteCurrent());
        deleteButton.Style = Ui.Style("Btn.Danger");
        deleteButton.Content = Ui.Row(6,
            Ui.Icon(Icons.Trash, 13, Ui.Brush("Brush.Danger")),
            new TextBlock
            {
                Text = "删除",
                FontSize = 12,
                Foreground = Ui.Brush("Brush.Danger"),
            });
        deleteButton.Padding = new Thickness(12, 7, 13, 7);
        deleteButton.Margin = new Thickness(8, 0, 0, 0);
        AutomationProperties.SetAutomationId(deleteButton, "NoteDelete");

        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(saveButton);
        buttons.Children.Add(deleteButton);
        _status.Margin = new Thickness(0, 7, 0, 0);

        return new Border
        {
            Padding = new Thickness(20, 10, 18, 12),
            Child = Ui.Stack(0, buttons, _status),
        };
    }

    private List<NoteItem> OrderedNotes() =>
        _services.NoteDoc.Items.OrderByDescending(n => n.UpdatedAt).ToList();

    private void OnSelectionChanged()
    {
        if (_loading)
        {
            return;
        }

        var index = _list.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        SaveCurrent(silent: true);
        var notes = OrderedNotes();
        if (index < notes.Count)
        {
            LoadIntoEditor(notes[index]);
        }
    }

    private void LoadIntoEditor(NoteItem note)
    {
        _loading = true;
        _current = note;
        _title.Text = note.Title;
        _content.Text = note.Content;
        _loading = false;
        _updated.Text = "最后修改 " + note.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        SetStatus("修改后记得保存。", isError: false);
        UpdateEditorState();
    }

    private void CreateNew()
    {
        SaveCurrent(silent: true);

        var now = DateTimeOffset.Now;
        var note = new NoteItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "新便签",
            Content = string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _services.NoteDoc.Items.Add(note);
        _services.SaveNotes();
        Refresh();
        SelectNote(note.Id);
        LoadIntoEditor(note);
        _content.Focus();
    }

    private void SaveCurrent(bool silent = false)
    {
        if (_current is null || _loading)
        {
            return;
        }

        var content = _content.Text.Replace("\r\n", "\n");
        var title = _title.Text.Trim();

        if (title.Length == 0 && content.Trim().Length == 0)
        {
            if (!silent)
            {
                SetStatus("空白便签不保存：写点内容或起个标题。", isError: true);
            }

            return;
        }

        if (title.Length == 0)
        {
            title = NoteStore.Create(string.Empty, content, DateTimeOffset.Now, Array.Empty<NoteItem>()).Title;
            _title.Text = title;
        }

        _current.Title = title;
        _current.Content = content;
        _current.UpdatedAt = DateTimeOffset.Now;

        try
        {
            _services.SaveNotes();
            _updated.Text = "最后修改 " + _current.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            if (!silent)
            {
                SetStatus("已保存到本地 notes.json。", isError: false);
                Refresh();
            }
            else
            {
                RefreshListOnly();
            }
        }
        catch (System.IO.IOException ex)
        {
            SetStatus("保存失败：" + ex.Message, isError: true);
        }
    }

    private void DeleteCurrent()
    {
        if (_current is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            Window.GetWindow(this)!,
            $"删除「{_current.Title}」？此操作不可撤销。",
            "删除便签",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        _services.NoteDoc.Items.Remove(_current);
        _current = null;
        _title.Clear();
        _content.Clear();
        _services.SaveNotes();
        Refresh();
        UpdateEditorState();
    }

    private void RefreshListOnly()
    {
        var selectedId = _current?.Id;
        _loading = true;
        _list.Items.Clear();
        var notes = OrderedNotes();
        for (var i = 0; i < notes.Count; i++)
        {
            _list.Items.Add(BuildListRow(notes[i], i == notes.Count - 1));
        }

        _loading = false;
        _listCount.Text = notes.Count == 0 ? "暂无便签" : $"{notes.Count} 条便签";
        _listScroll.Visibility = notes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _listHost.Children[1].Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectNote(selectedId);
    }

    private void SelectNote(string? id)
    {
        if (id is null)
        {
            return;
        }

        var notes = OrderedNotes();
        var index = notes.FindIndex(n => n.Id == id);
        if (index >= 0)
        {
            _loading = true;
            _list.SelectedIndex = index;
            _loading = false;
        }
    }

    private static UIElement BuildListRow(NoteItem note, bool isLast)
    {
        var badge = Ui.InitialBadge(note.Title, Ui.Brush("Brush.WarningSoft"), Ui.Brush("Brush.Warning"), 34);
        var text = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = note.Title,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Ui.Brush("Brush.Text"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text = note.UpdatedAt.ToLocalTime().ToString("MM-dd HH:mm"),
                    FontSize = 10.5,
                    Foreground = Ui.Brush("Brush.TextMuted"),
                    Margin = new Thickness(0, 2, 0, 0),
                },
            },
        };

        var content = new Grid { Margin = new Thickness(9, 7, 9, 7) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(badge, 0);
        Grid.SetColumn(text, 1);
        var chevron = Ui.Icon(Icons.ChevronRight, 11, Ui.Brush("Brush.TextTertiary"));
        Grid.SetColumn(chevron, 2);
        text.Margin = new Thickness(9, 0, 6, 0);
        content.Children.Add(badge);
        content.Children.Add(text);
        content.Children.Add(chevron);

        var rowBody = new Border { Padding = new Thickness(0, 1, 0, 1), Child = content };
        return isLast ? rowBody : Ui.Stack(0, rowBody, Ui.Hairline(52));
    }

    private void UpdateEditorState()
    {
        var hasSelection = _current is not null;
        _editorArea.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        _detailHost.Children[1].Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        _editorFooter.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetStatus(string text, bool isError)
    {
        _status.Text = text;
        _status.Foreground = isError ? Ui.Brush("Brush.Danger") : Ui.Brush("Brush.TextMuted");
    }

    public void Refresh()
    {
        RefreshListOnly();
        if (_current is null && _services.NoteDoc.Items.Count > 0)
        {
            var first = OrderedNotes()[0];
            SelectNote(first.Id);
            LoadIntoEditor(first);
        }

        if (_services.NoteDoc.Items.Count == 0)
        {
            SetStatus("还没有便签。", isError: false);
        }

        UpdateEditorState();
    }
}
