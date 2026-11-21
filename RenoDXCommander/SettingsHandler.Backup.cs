// SettingsHandler.Backup.cs -- "Backup and restore" card in Settings › Tools & Maintenance (after Game library).
// Backup folder, automatic backups, Back up now, Restore from a backup..., Open folder, last backup line.
// Built in C# (XAML constraint). Making backups: MainViewModel.Backup.cs; restoring: BackupRestoreHelper (staged,
// applied on the next start) and MainWindow.Restore.cs (install review). Plan: docs/backup-restore-plan.md.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private TextBlock? _backupFolderText;
    private TextBlock? _backupLastText;
    private TextBlock? _backupStatus;
    private ComboBox? _autoBackupCombo;
    private Button? _backupNowBtn;
    private Button? _backupOpenBtn;
    private bool _backupCardFailed;
    private bool _backupComboInit;

    private void EnsureBackupCard()
    {
        if (_backupFolderText != null) { RefreshBackupCard(); return; }
        if (_backupCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Tools") { section = sp; break; }
            if (section == null)
            {
                _backupCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureBackupCard] Tools section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 10 };
            card.Child = body;

            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = Loc.GetString("Backup.Section.Title"), Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Backup.Section.Description"),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            body.Children.Add(head);

            // Backup folder
            var folderRow = new Grid { ColumnSpacing = 10 };
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _backupFolderText = new TextBlock
            {
                Style = FindStyle("SettingsLabelStyle"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                IsTextSelectionEnabled = true,
            };
            folderRow.Children.Add(_backupFolderText);
            var chooseBtn = MakeCustomFoldersButton(Loc.GetString("Dialog.ChooseFolder"), primary: false,
                Loc.GetString("Backup.Folder.Tooltip"));
            chooseBtn.Click += async (s, e) =>
            {
                try { await ChooseBackupFolderAsync(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.Backup] Choose folder failed — {ex.Message}"); }
            };
            Grid.SetColumn(chooseBtn, 1);
            folderRow.Children.Add(chooseBtn);
            body.Children.Add(folderRow);

            // Automatic backups
            var autoRow = new Grid { ColumnSpacing = 10 };
            autoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            autoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
            autoRow.Children.Add(new TextBlock { Text = Loc.GetString("Backup.Auto.Label"), Style = FindStyle("SettingsLabelStyle"), VerticalAlignment = VerticalAlignment.Center });
            _autoBackupCombo = new ComboBox { Style = FindStyle("SettingsComboStyle"), HorizontalAlignment = HorizontalAlignment.Stretch };
            _autoBackupCombo.Items.Add(Loc.GetString("Option.Off"));
            _autoBackupCombo.Items.Add(Loc.GetString("Option.On"));
            ToolTipService.SetToolTip(_autoBackupCombo,
                Loc.GetString("Backup.Auto.Tooltip"));
            _autoBackupCombo.SelectionChanged += (s, e) =>
            {
                if (_backupComboInit) return;
                bool on = _autoBackupCombo.SelectedIndex == 1;
                if (on && string.IsNullOrWhiteSpace(ViewModel.Settings.BackupFolder))
                {
                    SetBackupStatus(Loc.GetString("Backup.Error.NoFolder"), error: true);
                    _backupComboInit = true; _autoBackupCombo.SelectedIndex = 0; _backupComboInit = false;
                    return;
                }
                ViewModel.Settings.AutoBackup = on;
                ViewModel.SaveSettingsPublic();
                if (on) _ = ViewModel.MaybeAutoBackupAsync("turned on");
            };
            Grid.SetColumn(_autoBackupCombo, 1);
            autoRow.Children.Add(_autoBackupCombo);
            body.Children.Add(autoRow);

            _backupLastText = new TextBlock { Style = FindStyle("SettingsHelpStyle"), TextWrapping = TextWrapping.Wrap };
            body.Children.Add(_backupLastText);

            var buttons = new WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            _backupNowBtn = MakeCustomFoldersButton(Loc.GetString("Backup.Now.Button"), primary: true, Loc.GetString("Backup.Now.Tooltip"));
            _backupNowBtn.Click += async (s, e) =>
            {
                try { await BackUpNowAsync(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.Backup] Back up now failed — {ex.Message}"); }
            };
            buttons.Children.Add(_backupNowBtn);
            var restoreBtn = MakeCustomFoldersButton(Loc.GetString("Backup.Restore.Button"), primary: false,
                Loc.GetString("Backup.Restore.Tooltip"));
            restoreBtn.Click += async (s, e) =>
            {
                try { await RestoreFromBackupAsync(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.Backup] Restore failed — {ex.Message}"); }
            };
            buttons.Children.Add(restoreBtn);
            _backupOpenBtn = MakeCustomFoldersButton(Loc.GetString("Detail.OpenFolder"), primary: false, Loc.GetString("Backup.Open.Tooltip"));
            _backupOpenBtn.Click += (s, e) =>
            {
                try
                {
                    var folder = ViewModel.Settings.BackupFolder;
                    if (Directory.Exists(folder))
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
                }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.Backup] Open folder failed — {ex.Message}"); }
            };
            buttons.Children.Add(_backupOpenBtn);
            body.Children.Add(buttons);

            _backupStatus = new TextBlock
            {
                Style = FindStyle("SettingsNoteStyle"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            body.Children.Add(_backupStatus);

            body.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Backup.Section.NotIncluded"),
                Style = FindStyle("SettingsHelpStyle"),
                TextWrapping = TextWrapping.Wrap,
            });

            // After the Game library card (heading at 0, Game library at 1 when it was added)
            int at = 1;
            for (int i = 0; i < section.Children.Count; i++)
                if (section.Children[i] is Border { Child: Panel p } && _customFoldersList != null && p.Children.Contains(_customFoldersList)) { at = i + 1; break; }
            section.Children.Insert(Math.Min(at, section.Children.Count), card);
            ViewModel.BackupWritten += () => { if (_backupFolderText != null) RefreshBackupCard(); };
            RefreshBackupCard();
        }
        catch (Exception ex)
        {
            _backupCardFailed = true;
            _backupFolderText = null;
            CrashReporter.Log($"[SettingsHandler.EnsureBackupCard] Failed — {ex.Message}");
        }
    }

    private void RefreshBackupCard()
    {
        if (_backupFolderText == null) return;
        try
        {
            var folder = ViewModel.Settings.BackupFolder;
            bool hasFolder = !string.IsNullOrWhiteSpace(folder);
            _backupFolderText.Text = hasFolder ? folder : Loc.GetString("Backup.Folder.None");
            ToolTipService.SetToolTip(_backupFolderText, hasFolder ? folder : null);
            if (_autoBackupCombo != null)
            {
                _backupComboInit = true;
                _autoBackupCombo.SelectedIndex = ViewModel.Settings.AutoBackup ? 1 : 0;
                _backupComboInit = false;
            }
            if (_backupNowBtn != null) _backupNowBtn.IsEnabled = hasFolder;
            if (_backupOpenBtn != null) _backupOpenBtn.IsEnabled = hasFolder && Directory.Exists(folder);
            if (_backupLastText != null)
            {
                _backupLastText.Text = DateTime.TryParse(ViewModel.Settings.LastBackupUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var last)
                    ? Loc.GetString("Backup.Last.Format", $"{last.ToLocalTime():d MMMM yyyy, HH:mm}")
                    : Loc.GetString("Backup.Last.None");
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.RefreshBackupCard] {ex.Message}");
        }
    }

    private void SetBackupStatus(string text, bool error)
    {
        if (_backupStatus == null) return;
        _backupStatus.Text = text;
        _backupStatus.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextSecondaryBrush);
        _backupStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task ChooseBackupFolderAsync()
    {
        var current = ViewModel.Settings.BackupFolder;
        var folder = await _window.PickFolderAsync(string.IsNullOrWhiteSpace(current) ? null : current);
        if (string.IsNullOrEmpty(folder)) return;
        bool first = string.IsNullOrWhiteSpace(current);
        ViewModel.Settings.BackupFolder = folder;
        if (first) ViewModel.Settings.AutoBackup = true; // choosing a folder is the opt-in
        ViewModel.SaveSettingsPublic();
        RefreshBackupCard();
        if (folder.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows)[..3], StringComparison.OrdinalIgnoreCase)
            && !folder.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            SetBackupStatus(Loc.GetString("Backup.Warning.SystemDrive"), error: false);
        else
            SetBackupStatus(first ? Loc.GetString("Backup.Status.AutoOn") : "", error: false);
        if (first) await BackUpNowAsync();
    }

    private async Task BackUpNowAsync()
    {
        var folder = ViewModel.Settings.BackupFolder;
        if (string.IsNullOrWhiteSpace(folder)) { SetBackupStatus(Loc.GetString("Backup.Error.NoFolder"), error: true); return; }
        if (_backupNowBtn != null) { _backupNowBtn.IsEnabled = false; _backupNowBtn.Content = Loc.GetString("Backup.Now.BackingUp"); }
        try
        {
            var (path, error) = await ViewModel.CreateBackupAsync(folder, onlyIfChanged: false, "manual");
            if (error != null) SetBackupStatus(error, error: true);
            else if (path != null) SetBackupStatus(Loc.GetString("Dialog.Rdx.Saved", Path.GetFileName(path)), error: false);
        }
        finally
        {
            if (_backupNowBtn != null) { _backupNowBtn.Content = Loc.GetString("Backup.Now.Button"); _backupNowBtn.IsEnabled = true; }
            RefreshBackupCard();
        }
    }

    private async Task RestoreFromBackupAsync()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        var path = await BackupRestoreHelper.PickBackupFileAsync(hwnd, ViewModel.Settings.BackupFolder);
        if (string.IsNullOrEmpty(path)) return;
        Services.BackupManifest manifest;
        try { manifest = await Task.Run(() => BackupArchive.ReadManifest(path)); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            SetBackupStatus(ex is InvalidDataException ? ex.Message : Loc.GetString("Backup.Error.ReadFile", ex.Message), error: true);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Backup.Restore.DialogTitle"),
            Content = new TextBlock
            {
                Text = BackupRestoreHelper.Describe(manifest) + "\n\n"
                     + Loc.GetString("Backup.Restore.DialogContent"),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = Loc.GetString("Backup.Restore.ConfirmButton"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _window.Content?.XamlRoot,
        };
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            await Task.Run(() => BackupRestoreHelper.StageRestore(path));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            SetBackupStatus(Loc.GetString("Backup.Error.RestoreFailed", ex.Message), error: true);
            return;
        }
        BackupRestoreHelper.RestartRhi(_window);
    }
}
