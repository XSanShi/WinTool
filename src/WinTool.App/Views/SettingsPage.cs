using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WinTool.App.Infrastructure;
using WinTool.Core.Storage;

namespace WinTool.App.Views;

/// <summary>
/// 设置页：数据根、待办留底、vault 状态、热键和关于信息。
/// 技术性细节收进脚注与 tooltip，主界面只保留可操作的设置项。
/// </summary>
internal sealed class SettingsPage : UserControl
{
    private readonly AppServices _services;
    private readonly TextBlock _dataRoot = PathText();
    private readonly TextBlock _archivePath = PathText();
    private readonly TextBlock _archiveNote = Ui.Footnote(string.Empty);
    private readonly TextBlock _vaultNote = PathText();
    private readonly TextBlock _aclNote = Ui.Footnote(string.Empty);
    private readonly TextBlock _hotkeyNote = Ui.Muted(string.Empty);
    private readonly TextBlock _status = Ui.Muted(string.Empty);

    public SettingsPage(AppServices services)
    {
        _services = services;
        Content = BuildLayout();
        Refresh();
    }

    private static TextBlock PathText() => new()
    {
        FontSize = 11.5,
        Foreground = Ui.Brush("Brush.TextMuted"),
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private UIElement BuildLayout()
    {
        var title = Ui.Stack(3,
            Ui.IosTitle("设置"),
            Ui.Muted("所有数据都留在这台电脑上。"));
        var privacyChip = Ui.Chip("无账号 · 无联网", Ui.Brush("Brush.SuccessSoft"), Ui.Brush("Brush.Success"));
        privacyChip.VerticalAlignment = VerticalAlignment.Center;

        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(title, 0);
        Grid.SetColumn(privacyChip, 1);
        header.Children.Add(title);
        header.Children.Add(privacyChip);

        var storageGroup = BuildStorageGroup();
        var applicationGroup = BuildApplicationGroup();

        var storageLabel = Ui.SectionLabel("存储与安全");
        storageGroup.Margin = new Thickness(0, 7, 0, 0);
        var applicationLabel = Ui.SectionLabel("应用");
        applicationGroup.Margin = new Thickness(0, 7, 0, 0);
        _status.Margin = new Thickness(3, 10, 0, 0);

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(storageLabel, 0);
        Grid.SetRow(storageGroup, 1);
        Grid.SetRow(_status, 2);
        left.Children.Add(storageLabel);
        left.Children.Add(storageGroup);
        left.Children.Add(_status);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(applicationLabel, 0);
        Grid.SetRow(applicationGroup, 1);
        right.Children.Add(applicationLabel);
        right.Children.Add(applicationGroup);

        var columns = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.28, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        columns.Children.Add(left);
        columns.Children.Add(right);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        Grid.SetRow(columns, 1);
        root.Children.Add(header);
        root.Children.Add(columns);
        return root;
    }

    private Border BuildStorageGroup()
    {
        var openData = SmallButton("打开", () => OpenFolder(_services.Paths.DataRoot));
        var dataRow = SettingRow(
            Icons.Folder,
            Ui.Brush("Brush.AccentSoft"),
            Ui.Brush("Brush.Accent"),
            "数据目录",
            _dataRoot,
            openData);

        var chooseArchive = SmallButton("选择位置…", ChooseArchiveDirectory);
        var resetArchive = SmallButton("恢复默认", ResetArchiveDirectory);
        var openArchive = SmallButton("打开", () => OpenFolder(_services.Paths.ArchiveRoot));
        var archiveActions = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        archiveActions.Children.Add(chooseArchive);
        resetArchive.Margin = new Thickness(7, 0, 0, 0);
        openArchive.Margin = new Thickness(7, 0, 0, 0);
        archiveActions.Children.Add(resetArchive);
        archiveActions.Children.Add(openArchive);

        var archiveBody = Ui.Stack(2, _archivePath, _archiveNote, archiveActions);
        var archiveRow = SettingRow(
            Icons.Archive,
            Ui.Brush("Brush.WarningSoft"),
            Ui.Brush("Brush.Warning"),
            "待办留底",
            archiveBody);

        var openVault = SmallButton("打开文件夹", () => OpenFolder(Path.GetDirectoryName(_services.Paths.VaultPath)));
        var vaultBody = Ui.Stack(2, _vaultNote, _aclNote);
        var vaultRow = SettingRow(
            Icons.ShieldCheck,
            Ui.Brush("Brush.SuccessSoft"),
            Ui.Brush("Brush.Success"),
            "账号密码库",
            vaultBody,
            openVault);

        var group = new StackPanel();
        group.Children.Add(dataRow);
        group.Children.Add(Ui.Hairline(62));
        group.Children.Add(archiveRow);
        group.Children.Add(Ui.Hairline(62));
        group.Children.Add(vaultRow);

        return new Border
        {
            Style = Ui.Style("Card"),
            Padding = new Thickness(0),
            Child = group,
        };
    }

    private Border BuildApplicationGroup()
    {
        var hotkeyBody = Ui.Stack(2, _hotkeyNote);
        var keycap = new Border
        {
            Background = Ui.Brush("Brush.SurfaceSecondary"),
            BorderBrush = Ui.Brush("Brush.Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(9, 4, 9, 4),
            Child = new TextBlock
            {
                Text = Provisional.HotkeyDisplay,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.Brush("Brush.TextMuted"),
            },
            VerticalAlignment = VerticalAlignment.Center,
        };
        var hotkeyRow = SettingRow(
            Icons.Keyboard,
            Ui.Brush("Brush.AccentSoft"),
            Ui.Brush("Brush.Accent"),
            "全局热键",
            hotkeyBody,
            keycap);

        var aboutBody = Ui.Stack(2,
            new TextBlock
            {
                Text = "WinTool 0.1.0 · 本地优先",
                FontSize = 12,
                Foreground = Ui.Brush("Brush.TextMuted"),
            },
            Ui.Footnote("待办、账号密码和便签都保存在本机文件中；写入采用临时文件与原子替换。"));
        var aboutRow = SettingRow(
            Icons.Info,
            Ui.Brush("Brush.Fill"),
            Ui.Brush("Brush.TextMuted"),
            "关于",
            aboutBody,
            null);

        var group = new StackPanel();
        group.Children.Add(hotkeyRow);
        group.Children.Add(Ui.Hairline(62));
        group.Children.Add(aboutRow);

        return new Border
        {
            Style = Ui.Style("Card"),
            Padding = new Thickness(0),
            Child = group,
        };
    }

    private static Border SettingRow(
        Geometry icon,
        Brush iconBackground,
        Brush iconForeground,
        string title,
        UIElement content,
        UIElement? trailing = null)
    {
        var tile = Ui.IconTile(icon, iconBackground, iconForeground, 34);
        var heading = new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ui.Brush("Brush.Text"),
        };
        var body = Ui.Stack(3, heading, content);
        body.Margin = new Thickness(11, 0, 12, 0);

        var grid = new Grid { Margin = new Thickness(16, 12, 16, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(tile, 0);
        Grid.SetColumn(body, 1);
        grid.Children.Add(tile);
        grid.Children.Add(body);
        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            if (trailing is FrameworkElement fe)
            {
                fe.VerticalAlignment = VerticalAlignment.Center;
            }

            grid.Children.Add(trailing);
        }

        return new Border
        {
            Padding = new Thickness(0),
            Child = grid,
        };
    }

    private static Button SmallButton(string text, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Style = Ui.Style("Btn.Base"),
            FontSize = 11.5,
            Padding = new Thickness(9, 5, 9, 5),
            MinHeight = 28,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    public void SetHotkeyStatus(string text) => _hotkeyNote.Text = text;

    private void ChooseArchiveDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择待办留底目录",
            InitialDirectory = Directory.Exists(_services.Paths.ArchiveRoot)
                ? _services.Paths.ArchiveRoot
                : _services.Paths.DataRoot,
            Multiselect = false,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        ApplyArchiveDirectory(dialog.FolderName);
    }

    private void ResetArchiveDirectory() => ApplyArchiveDirectory(null);

    private void ApplyArchiveDirectory(string? directory)
    {
        var probe = directory ?? AppPaths.Resolve(null).ArchiveRoot;
        if (!AtomicFile.CanWriteToDirectory(probe))
        {
            MessageBox.Show(
                Window.GetWindow(this)!,
                $"这个目录当前不可写，无法作为留底目录：\n{probe}\n\n换一个可写目录再试。",
                "留底目录不可写",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            _services.SetArchiveDirectory(directory);
            Refresh();
            SetStatus("留底目录已更新。", isError: false);
        }
        catch (IOException ex)
        {
            SetStatus("保存设置失败：" + ex.Message, isError: true);
        }
    }

    private void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            SetStatus("目录还不存在：" + path, isError: true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            SetStatus("打开失败：" + ex.Message, isError: true);
        }
    }

    private void SetStatus(string text, bool isError)
    {
        _status.Text = text;
        _status.Foreground = isError ? Ui.Brush("Brush.Danger") : Ui.Brush("Brush.TextMuted");
    }

    public void Refresh()
    {
        _dataRoot.Text = _services.Paths.DataRoot
                         + (_services.Paths.DataRootIsFallback ? "（回退值）" : string.Empty);
        _dataRoot.ToolTip = _dataRoot.Text;

        _archivePath.Text = _services.Paths.ArchiveRoot
                            + (_services.Paths.ArchiveRootIsFallback ? "（回退值）" : string.Empty);
        _archivePath.ToolTip = _archivePath.Text;
        _archiveNote.Text = "清理已完成待办时写入 Markdown。";

        _vaultNote.Text = _services.Paths.VaultPath;
        _vaultNote.ToolTip = _vaultNote.Text;
        _aclNote.Text = _services.VaultAclWarning is null
            ? "文件权限已收紧到当前用户；本地明文存储。"
            : _services.VaultAclWarning;
        _aclNote.Foreground = _services.VaultAclWarning is null
            ? Ui.Brush("Brush.TextMuted")
            : Ui.Brush("Brush.Danger");
    }
}
