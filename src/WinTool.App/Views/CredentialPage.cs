using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using WinTool.App.Infrastructure;
using WinTool.Core.Model;

namespace WinTool.App.Views;

/// <summary>
/// 账号密码页（方案 §6，D1a 已确认本地明文）。
/// 密码默认遮罩；vault 的实际路径与打开入口保留在页面底部。
/// </summary>
internal sealed class CredentialPage : UserControl
{
    private readonly AppServices _services;
    private readonly ListBox _list = new();
    private readonly TextBox _title = Ui.Input();
    private readonly TextBox _username = Ui.Input();
    private readonly PasswordBox _passwordMasked = new();
    private readonly TextBox _passwordPlain = Ui.Input();
    private readonly TextBox _url = Ui.Input();
    private readonly TextBox _notes = Ui.Input(multiline: true, acceptsReturn: true);
    private readonly CheckBox _showPassword = new() { Style = Ui.Style("IosToggle") };
    private readonly TextBlock _status = Ui.Muted(string.Empty);
    private readonly TextBlock _listCount = new();
    private readonly TextBlock _footer = new();
    private readonly Grid _listHost = new();
    private readonly Grid _detailHost = new();
    private readonly ScrollViewer _listScroll;
    private readonly ScrollViewer _formScroll;
    private readonly Border _detailFooter;

    private CredentialRow? _current;
    private bool _loading;

    public CredentialPage(AppServices services)
    {
        _services = services;

        _passwordMasked.Style = Ui.Style("PasswordInput");
        _passwordMasked.MinHeight = 36;
        _showPassword.Checked += (_, _) => SyncPasswordVisibility(true);
        _showPassword.Unchecked += (_, _) => SyncPasswordVisibility(false);
        _passwordPlain.Visibility = Visibility.Collapsed;
        _notes.MinHeight = 78;

        FormInput.Configure(_title);
        FormInput.Configure(_username);
        FormInput.Configure(_url);

        _listScroll = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var form = BuildForm();
        _formScroll = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _detailFooter = BuildDetailFooter();

        Content = BuildLayout();
        Refresh();
    }

    private string CurrentPassword => _showPassword.IsChecked == true
        ? _passwordPlain.Text
        : _passwordMasked.Password;

    private void SyncPasswordVisibility(bool show)
    {
        if (show)
        {
            _passwordPlain.Text = _passwordMasked.Password;
        }
        else
        {
            _passwordMasked.Password = _passwordPlain.Text;
        }

        _passwordPlain.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _passwordMasked.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private UIElement BuildLayout()
    {
        _list.ItemContainerStyle = Ui.Style("RowItem");
        _list.Background = Brushes.Transparent;
        _list.BorderThickness = new Thickness(0);
        _list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _list.Margin = new Thickness(0, 3, 0, 5);
        _list.SelectionChanged += (_, _) => OnSelectionChanged();

        AutomationProperties.SetAutomationId(_list, "CredList");
        AutomationProperties.SetAutomationId(_title, "CredTitle");
        AutomationProperties.SetAutomationId(_username, "CredUsername");
        AutomationProperties.SetAutomationId(_passwordPlain, "CredPasswordPlain");
        AutomationProperties.SetAutomationId(_url, "CredUrl");
        AutomationProperties.SetAutomationId(_notes, "CredNotes");
        AutomationProperties.SetAutomationId(_showPassword, "CredShowPassword");

        var headerText = Ui.Stack(3,
            Ui.IosTitle("账号密码"),
            Ui.Muted("密码默认隐藏。所有项目保存在这台电脑上。"));
        var localChip = Ui.Chip("仅本机", Ui.Brush("Brush.SuccessSoft"), Ui.Brush("Brush.Success"));
        localChip.VerticalAlignment = VerticalAlignment.Center;
        var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(headerText, 0);
        Grid.SetColumn(localChip, 1);
        header.Children.Add(headerText);
        header.Children.Add(localChip);

        var listCard = BuildListCard();
        var detailCard = BuildDetailCard();
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(284) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(listCard, 0);
        Grid.SetColumn(detailCard, 1);
        detailCard.Margin = new Thickness(14, 0, 0, 0);
        body.Children.Add(listCard);
        body.Children.Add(detailCard);

        _footer.Style = Ui.Style("Text.Footnote");
        _footer.TextTrimming = TextTrimming.CharacterEllipsis;
        _footer.Cursor = System.Windows.Input.Cursors.Hand;
        _footer.MouseLeftButtonUp += (_, _) => OpenVaultFolder();
        _footer.Margin = new Thickness(4, 10, 0, 0);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(header, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(_footer, 2);
        root.Children.Add(header);
        root.Children.Add(body);
        root.Children.Add(_footer);
        return root;
    }

    private Border BuildListCard()
    {
        var newButton = Ui.CircleIconButton(Icons.Plus, "新建账号密码", (_, _) => CreateNew(), size: 16);
        AutomationProperties.SetAutomationId(newButton, "CredNew");

        var title = new TextBlock { Text = "所有项目", Style = Ui.Style("Text.CardTitle") };
        var titleRow = Ui.Row(7, Ui.StatusDot(Ui.Brush("Brush.Success")), title);
        _listCount.FontSize = 10.5;
        _listCount.Foreground = Ui.Brush("Brush.TextMuted");

        var heading = new StackPanel { Children = { titleRow, _listCount } };
        _listCount.Margin = new Thickness(0, 2, 0, 0);

        var listHeader = new Grid { Margin = new Thickness(20, 16, 16, 12) };
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(heading, 0);
        Grid.SetColumn(newButton, 1);
        listHeader.Children.Add(heading);
        listHeader.Children.Add(newButton);

        var empty = Ui.EmptyState(
            Icons.Key,
            "还没有项目",
            "点击右上角的 +，保存第一个账号密码。");
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

    private Border BuildDetailCard()
    {
        var heading = Ui.Stack(2,
            Ui.Row(7,
                Ui.StatusDot(Ui.Brush("Brush.Accent")),
                new TextBlock { Text = "详细信息", Style = Ui.Style("Text.CardTitle") }),
            Ui.Muted("修改后点保存生效"));
        var badge = Ui.Chip("本地保存", Ui.Brush("Brush.SuccessSoft"), Ui.Brush("Brush.Success"));
        badge.VerticalAlignment = VerticalAlignment.Center;

        var detailHeader = new Grid { Margin = new Thickness(20, 16, 18, 12) };
        detailHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        detailHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(heading, 0);
        Grid.SetColumn(badge, 1);
        detailHeader.Children.Add(heading);
        detailHeader.Children.Add(badge);

        var empty = Ui.EmptyState(
            Icons.Lock,
            "选择一个项目",
            "在左侧选择项目查看详情，或新建一个账号密码。");
        _detailHost.Children.Add(_formScroll);
        _detailHost.Children.Add(empty);

        var headerDivider = Ui.Hairline();
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(detailHeader, 0);
        Grid.SetRow(headerDivider, 1);
        Grid.SetRow(_detailHost, 2);
        var footerDivider = Ui.Hairline();
        Grid.SetRow(footerDivider, 3);
        Grid.SetRow(_detailFooter, 4);
        content.Children.Add(detailHeader);
        content.Children.Add(headerDivider);
        content.Children.Add(_detailHost);
        content.Children.Add(footerDivider);
        content.Children.Add(_detailFooter);

        return new Border
        {
            Style = Ui.Style("Card"),
            Padding = new Thickness(0),
            Child = content,
        };
    }

    private UIElement BuildForm()
    {
        var passwordToggle = Ui.Row(7,
            new TextBlock
            {
                Text = "显示密码",
                FontSize = 11.5,
                Foreground = Ui.Brush("Brush.TextMuted"),
                VerticalAlignment = VerticalAlignment.Center,
            },
            _showPassword);

        var passwordHeading = new Grid();
        passwordHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        passwordHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var passwordLabel = FieldLabel("密码");
        Grid.SetColumn(passwordLabel, 0);
        Grid.SetColumn(passwordToggle, 1);
        passwordHeading.Children.Add(passwordLabel);
        passwordHeading.Children.Add(passwordToggle);

        var passwordFields = new Grid();
        passwordFields.Children.Add(_passwordMasked);
        passwordFields.Children.Add(_passwordPlain);
        var passwordBlock = Ui.Stack(5, passwordHeading, passwordFields);

        var fields = Ui.Stack(0,
            FormRow("标题", _title),
            FormRow("用户名 / 账号", _username),
            FormArea(passwordBlock),
            FormRow("网址（可选）", _url),
            FormBlock("备注（可选，明文保存）", _notes, isLast: true));

        var body = new StackPanel();
        body.Children.Add(SectionCaption("基本信息"));
        body.Children.Add(fields);
        body.Margin = new Thickness(0, 2, 0, 8);
        return body;
    }

    private Border BuildDetailFooter()
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
        AutomationProperties.SetAutomationId(saveButton, "CredSave");

        var copyButton = Ui.Base("复制密码", (_, _) => CopyPassword());
        copyButton.Content = Ui.Row(6,
            Ui.Icon(Icons.Copy, 14, Ui.Brush("Brush.Text")),
            new TextBlock
            {
                Text = "复制密码",
                FontSize = 12,
                Foreground = Ui.Brush("Brush.Text"),
            });
        copyButton.Padding = new Thickness(12, 7, 13, 7);
        AutomationProperties.SetAutomationId(copyButton, "CredCopy");

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
        AutomationProperties.SetAutomationId(deleteButton, "CredDelete");

        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(saveButton);
        copyButton.Margin = new Thickness(8, 0, 0, 0);
        deleteButton.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(copyButton);
        buttons.Children.Add(deleteButton);

        _status.Margin = new Thickness(0, 7, 0, 0);
        return new Border
        {
            Padding = new Thickness(20, 10, 18, 12),
            Child = Ui.Stack(0, buttons, _status),
        };
    }

    private static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        FontSize = 10.5,
        FontWeight = FontWeights.SemiBold,
        Foreground = Ui.Brush("Brush.TextMuted"),
    };

    private static TextBlock SectionCaption(string text) => new()
    {
        Text = text,
        FontSize = 10.5,
        FontWeight = FontWeights.SemiBold,
        Foreground = Ui.Brush("Brush.TextTertiary"),
        Margin = new Thickness(20, 12, 0, 5),
    };

    private static UIElement FormRow(string label, UIElement input, bool isLast = false)
    {
        var content = new Border
        {
            Padding = new Thickness(20, 7, 20, 7),
            Child = Ui.Stack(5, FieldLabel(label), input),
        };
        return isLast ? content : Ui.Stack(0, content, Ui.Hairline(18));
    }

    private static UIElement FormBlock(string label, UIElement block, bool isLast = false)
    {
        var content = new Border
        {
            Padding = new Thickness(20, 7, 20, 7),
            Child = Ui.Stack(5, FieldLabel(label), block),
        };
        return isLast ? content : Ui.Stack(0, content, Ui.Hairline(18));
    }

    private static UIElement FormArea(UIElement block, bool isLast = false)
    {
        var content = new Border
        {
            Padding = new Thickness(20, 5, 20, 6),
            Child = block,
        };
        return isLast ? content : Ui.Stack(0, content, Ui.Hairline(18));
    }

    private void OpenVaultFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(_services.Paths.VaultPath);
            if (folder is not null && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
            }
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            SetStatus("打开文件夹失败：" + ex.Message, isError: true);
        }
    }

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
        var rows = OrderedRows();
        if (index >= rows.Count)
        {
            return;
        }

        LoadIntoForm(rows[index]);
    }

    private List<CredentialRow> OrderedRows() =>
        _services.CredentialDoc.Rows.OrderBy(r => r.Title, StringComparer.CurrentCulture).ToList();

    private void LoadIntoForm(CredentialRow row)
    {
        _loading = true;
        _current = row;
        _title.Text = row.Title;
        _username.Text = row.Username;
        _passwordMasked.Password = row.Password;
        _passwordPlain.Text = row.Password;
        _url.Text = row.Url ?? string.Empty;
        _notes.Text = row.Notes ?? string.Empty;
        _loading = false;
        SetStatus(
            row.UpdatedAt == default
                ? "新项目：填好后点保存。"
                : "最后修改：" + row.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            isError: false);
        UpdateDetailState();
    }

    private void CreateNew()
    {
        SaveCurrent(silent: true);

        var row = new CredentialRow
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "新项目",
            Username = string.Empty,
            Password = string.Empty,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
        };

        _services.CredentialDoc.Rows.Add(row);
        _services.SaveCredentials();
        Refresh();
        SelectRow(row.Id);
        LoadIntoForm(row);
        _title.Focus();
        _title.SelectAll();
    }

    private void SaveCurrent(bool silent = false)
    {
        if (_current is null || _loading)
        {
            return;
        }

        var title = _title.Text.Trim();
        if (title.Length == 0)
        {
            if (!silent)
            {
                SetStatus("标题不能为空。", isError: true);
            }

            return;
        }

        _current.Title = title;
        _current.Username = _username.Text;
        _current.Password = CurrentPassword;
        _current.Url = _url.Text.Trim();
        _current.Notes = _notes.Text;
        _current.UpdatedAt = DateTimeOffset.Now;

        try
        {
            _services.SaveCredentials();
            if (!silent)
            {
                SetStatus("已保存到本地 vault。", isError: false);
                Refresh();
            }
            else
            {
                RefreshListOnly();
            }
        }
        catch (IOException ex)
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
            "删除账号密码",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        _services.CredentialDoc.Rows.Remove(_current);
        _current = null;
        _services.SaveCredentials();
        _title.Clear();
        _username.Clear();
        _passwordMasked.Clear();
        _passwordPlain.Clear();
        _url.Clear();
        _notes.Clear();
        SetStatus("已删除。", isError: false);
        Refresh();
        UpdateDetailState();
    }

    private void CopyPassword()
    {
        var password = CurrentPassword;
        if (password.Length == 0)
        {
            SetStatus("当前没有密码可复制。", isError: true);
            return;
        }

        try
        {
            Clipboard.SetText(password);
            SetStatus("密码已复制到剪贴板。", isError: false);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            SetStatus("复制失败（剪贴板被其他程序占用）：" + ex.Message, isError: true);
        }
    }

    private void RefreshListOnly()
    {
        var selectedId = _current?.Id;
        _loading = true;
        _list.Items.Clear();
        var rows = OrderedRows();
        for (var i = 0; i < rows.Count; i++)
        {
            _list.Items.Add(BuildListRow(rows[i], i == rows.Count - 1));
        }

        _loading = false;
        _listCount.Text = rows.Count == 0 ? "暂无项目" : $"{rows.Count} 个项目";
        _listScroll.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _listHost.Children[1].Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectRow(selectedId);
    }

    private void SelectRow(string? id)
    {
        if (id is null)
        {
            return;
        }

        var rows = OrderedRows();
        var index = rows.FindIndex(r => r.Id == id);
        if (index >= 0)
        {
            _loading = true;
            _list.SelectedIndex = index;
            _loading = false;
        }
    }

    private static UIElement BuildListRow(CredentialRow row, bool isLast)
    {
        var (bg, fg) = BadgeColors(row.Title);
        var badge = Ui.InitialBadge(row.Title, bg, fg, 34);

        var text = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = row.Title,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Ui.Brush("Brush.Text"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(row.Username) ? "未填写用户名" : row.Username,
                    FontSize = 10.5,
                    Foreground = Ui.Brush("Brush.TextMuted"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
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

        var rowBody = new Border
        {
            Padding = new Thickness(0, 1, 0, 1),
            Child = content,
        };
        return isLast ? rowBody : Ui.Stack(0, rowBody, Ui.Hairline(52));
    }

    /// <summary>按标题首字母在三种 iOS 着色里轮换（蓝 / 绿 / 橙）。</summary>
    private static (SolidColorBrush Background, SolidColorBrush Foreground) BadgeColors(string title)
    {
        var c = char.ToUpperInvariant(string.IsNullOrWhiteSpace(title) ? '?' : title.TrimStart()[0]);
        return (c % 3) switch
        {
            1 => (Ui.Brush("Brush.AccentSoft"), Ui.Brush("Brush.Accent")),
            2 => (Ui.Brush("Brush.SuccessSoft"), Ui.Brush("Brush.Success")),
            _ => (Ui.Brush("Brush.WarningSoft"), Ui.Brush("Brush.Warning")),
        };
    }

    private void UpdateDetailState()
    {
        var hasSelection = _current is not null;
        _formScroll.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        _detailHost.Children[1].Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        _detailFooter.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetStatus(string text, bool isError)
    {
        _status.Text = text;
        _status.Foreground = isError ? Ui.Brush("Brush.Danger") : Ui.Brush("Brush.TextMuted");
    }

    public void Refresh()
    {
        RefreshListOnly();
        if (_current is null && _services.CredentialDoc.Rows.Count > 0)
        {
            var first = OrderedRows()[0];
            SelectRow(first.Id);
            LoadIntoForm(first);
        }

        var warning = _services.VaultAclWarning;
        _footer.Text = warning is null
            ? "vault.json · 明文保存在本机 · 点击打开所在文件夹"
            : "vault.json · " + warning;
        _footer.Foreground = warning is null
            ? Ui.Brush("Brush.TextMuted")
            : Ui.Brush("Brush.Danger");
        _footer.ToolTip = _services.Paths.VaultPath;
        UpdateDetailState();
    }

    /// <summary>统一表单输入的外框观感，避免重复设置。</summary>
    private static class FormInput
    {
        public static void Configure(TextBox box)
        {
            box.FontSize = 13;
            box.Padding = new Thickness(10, 7, 10, 7);
            box.MinHeight = 36;
        }
    }
}
