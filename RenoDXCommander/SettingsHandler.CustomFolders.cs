// SettingsHandler.CustomFolders.cs -- "Custom game folders" card at the top of Settings › Tools & Maintenance:
// folders RHI scans for DRM-free / non-store games (MainViewModel.CustomFolders.cs). Lists the folders with
// Remove, plus Add folder, Scan now, Review (when games are waiting) and Forget ignored games.
// Built in C# (XAML constraint), inserted the first time Settings opens.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private StackPanel? _customFoldersList;
    private TextBlock? _customFoldersStatus;
    private Button? _customFoldersReviewBtn;
    private Button? _customFoldersForgetBtn;
    private Button? _customFoldersScanBtn;
    private bool _customFoldersCardFailed;

    private void EnsureCustomFoldersCard()
    {
        if (_customFoldersList != null) { RefreshCustomFoldersCard(); return; }
        if (_customFoldersCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Tools") { section = sp; break; }
            if (section == null)
            {
                _customFoldersCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureCustomFoldersCard] Tools section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 10 };
            card.Child = body;

            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = Loc.GetString("CustomFolders.Card.Title"), Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("CustomFolders.Card.Description"),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            body.Children.Add(head);

            _customFoldersList = new StackPanel { Spacing = 6 };
            body.Children.Add(_customFoldersList);

            var buttons = new WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            var addBtn = MakeCustomFoldersButton(Loc.GetString("CustomFolders.Button.AddFolder"), primary: true,
                Loc.GetString("CustomFolders.Tooltip.AddFolder"));
            addBtn.Click += async (s, e) =>
            {
                try { await AddCustomFolderAsync(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Add failed — {ex.Message}"); }
            };
            buttons.Children.Add(addBtn);

            _customFoldersScanBtn = MakeCustomFoldersButton(Loc.GetString("CustomFolders.Button.ScanNow"), primary: false,
                Loc.GetString("CustomFolders.Tooltip.ScanNow"));
            _customFoldersScanBtn.Click += async (s, e) =>
            {
                try { await ScanCustomFoldersNowAsync(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Scan failed — {ex.Message}"); }
            };
            buttons.Children.Add(_customFoldersScanBtn);

            _customFoldersReviewBtn = MakeCustomFoldersButton(Loc.GetString("CustomFolders.Button.Review"), primary: false,
                Loc.GetString("CustomFolders.Tooltip.Review"));
            _customFoldersReviewBtn.Click += async (s, e) =>
            {
                try { await _window.ShowCustomGamesReviewAsync(); RefreshCustomFoldersCard(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Review failed — {ex.Message}"); }
            };
            buttons.Children.Add(_customFoldersReviewBtn);

            _customFoldersForgetBtn = MakeCustomFoldersButton(Loc.GetString("CustomFolders.Button.ForgetIgnored"), primary: false,
                Loc.GetString("CustomFolders.Tooltip.ForgetIgnored"));
            _customFoldersForgetBtn.Click += (s, e) =>
            {
                try
                {
                    ViewModel.ClearIgnoredCustomGameFolders();
                    SetCustomFoldersStatus(Loc.GetString("CustomFolders.Status.IgnoredReset"), error: false);
                    RefreshCustomFoldersCard();
                }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Forget ignored failed — {ex.Message}"); }
            };
            buttons.Children.Add(_customFoldersForgetBtn);
            body.Children.Add(buttons);

            _customFoldersStatus = new TextBlock
            {
                Style = FindStyle("SettingsNoteStyle"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            body.Children.Add(_customFoldersStatus);

            // After the heading, before the Tools card
            section.Children.Insert(Math.Min(1, section.Children.Count), card);
            ViewModel.PendingCustomGamesChanged += () => { if (_customFoldersList != null) RefreshCustomFoldersCard(); };
            RefreshCustomFoldersCard();
        }
        catch (Exception ex)
        {
            _customFoldersCardFailed = true;
            CrashReporter.Log($"[SettingsHandler.EnsureCustomFoldersCard] Failed — {ex.Message}");
        }
    }

    private static Button MakeCustomFoldersButton(string text, bool primary, string tooltip)
    {
        var btn = new Button { Content = text, Style = FindStyle(primary ? "SettingsPrimaryButtonStyle" : "SettingsButtonStyle") };
        var key = primary ? "SettingsButtonBlueBrush" : ResourceKeys.SettingsLabelBrush;
        if (Application.Current.Resources.TryGetValue(key, out var v) && v is Brush brush)
        {
            btn.Foreground = brush;
            // Keep the text colour on hover / press (WinUI hover colour sticks otherwise)
            btn.Resources["ButtonForegroundPointerOver"] = brush;
            btn.Resources["ButtonForegroundPressed"] = brush;
        }
        ToolTipService.SetToolTip(btn, tooltip);
        return btn;
    }

    private void RefreshCustomFoldersCard()
    {
        if (_customFoldersList == null) return;
        try
        {
            _customFoldersList.Children.Clear();
            var folders = ViewModel.CustomGameFolders;
            if (folders.Count == 0)
            {
                _customFoldersList.Children.Add(new TextBlock
                {
                    Text = Loc.GetString("CustomFolders.Empty"),
                    Style = FindStyle("SettingsNoteStyle"),
                });
            }
            foreach (var folder in folders)
            {
                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = folder,
                    Style = FindStyle("SettingsLabelStyle"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsTextSelectionEnabled = true,
                };
                ToolTipService.SetToolTip(label, folder);
                row.Children.Add(label);
                var remove = MakeCustomFoldersButton(Loc.GetString("Dialog.Remove"), primary: false,
                    Loc.GetString("CustomFolders.Tooltip.Remove"));
                var captured = folder;
                remove.Click += (s, e) =>
                {
                    try
                    {
                        ViewModel.RemoveCustomGameFolder(captured);
                        SetCustomFoldersStatus(Loc.GetString("CustomFolders.Status.Removed", captured), error: false);
                        RefreshCustomFoldersCard();
                    }
                    catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Remove '{captured}' failed — {ex.Message}"); }
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                _customFoldersList.Children.Add(row);
            }

            int pending = ViewModel.PendingCustomGames.Count;
            if (_customFoldersReviewBtn != null)
            {
                _customFoldersReviewBtn.Content = pending == 1 ? Loc.GetString("CustomFolders.Button.ReviewOne") : Loc.GetString("CustomFolders.Button.ReviewMany", pending);
                _customFoldersReviewBtn.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            int ignored = ViewModel.IgnoredCustomGameFolders.Count;
            if (_customFoldersForgetBtn != null)
            {
                _customFoldersForgetBtn.Content = Loc.GetString("CustomFolders.Button.ForgetIgnoredCount", ignored);
                _customFoldersForgetBtn.Visibility = ignored > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_customFoldersScanBtn != null)
                _customFoldersScanBtn.IsEnabled = folders.Count > 0;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.RefreshCustomFoldersCard] {ex.Message}");
        }
    }

    private void SetCustomFoldersStatus(string text, bool error)
    {
        if (_customFoldersStatus == null) return;
        _customFoldersStatus.Text = text;
        _customFoldersStatus.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextSecondaryBrush);
        _customFoldersStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task AddCustomFolderAsync()
    {
        var folder = await _window.PickFolderAsync(ViewModel.CustomGameFolders.LastOrDefault());
        if (string.IsNullOrEmpty(folder)) return;
        var error = ViewModel.AddCustomGameFolder(folder);
        if (error != null)
        {
            SetCustomFoldersStatus(error, error: true);
            return;
        }
        RefreshCustomFoldersCard();
        SetCustomFoldersStatus(Loc.GetString("CustomFolders.Status.FolderAdded", CustomFolderScanService.NormalizePath(folder)), error: false);
        await ScanCustomFoldersNowAsync();
    }

    private async Task ScanCustomFoldersNowAsync()
    {
        if (ViewModel.CustomGameFolders.Count == 0)
        {
            SetCustomFoldersStatus(Loc.GetString("CustomFolders.Status.AddFolderFirst"), error: false);
            return;
        }
        if (_customFoldersScanBtn != null) { _customFoldersScanBtn.IsEnabled = false; _customFoldersScanBtn.Content = Loc.GetString("Status.Scanning"); }
        IReadOnlyList<Models.CustomGameCandidate>? found;
        try
        {
            found = await ViewModel.ScanCustomFoldersAsync("ScanNow");
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            if (_customFoldersScanBtn != null) { _customFoldersScanBtn.Content = Loc.GetString("CustomFolders.Button.ScanNow"); _customFoldersScanBtn.IsEnabled = true; }
        }
        if (_window.IsShuttingDown) return;
        if (found == null)
        {
            SetCustomFoldersStatus(Loc.GetString("CustomFolders.Status.ScanRunning"), error: false);
            return;
        }
        if (found.Count == 0)
        {
            SetCustomFoldersStatus(Loc.GetString("CustomFolders.Status.NoNewGames"), error: false);
            RefreshCustomFoldersCard();
            return;
        }
        SetCustomFoldersStatus(found.Count == 1 ? Loc.GetString("CustomFolders.Status.FoundOne") : Loc.GetString("CustomFolders.Status.FoundMany", found.Count), error: false);
        RefreshCustomFoldersCard();
        // The user asked for this scan, so the review opens straight away
        await _window.ShowCustomGamesReviewAsync();
        RefreshCustomFoldersCard();
    }
}
