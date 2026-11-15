// SettingsHandler.SteamGridDb.cs -- the SteamGridDB card in Settings › Accounts & Data: the user's own API key for
// browsing community covers and banners in Spotlight (game menu › Artwork › Browse...). Built in C# (XAML
// constraint) and added after the Accounts section's existing card. The key lives in
// %LocalAppData%\RHI\steamgriddb_api.txt (SteamGridDbService), not settings.json.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private TextBox? _sgdbKeyBox;
    private TextBlock? _sgdbStatus;
    private Button? _sgdbSaveBtn;
    private Button? _sgdbClearBtn;
    private bool _sgdbCardFailed;
    private bool _sgdbValidating;

    /// <summary>Opens Settings at a section (e.g. "Accounts"), for links from elsewhere in RHI.</summary>
    internal void OpenAtSection(string key)
    {
        try
        {
            // Opening restores the last section, so point that at the wanted one first
            ViewModel.Settings.SettingsLastSection = key;
            SettingsButton_Click(_window, new RoutedEventArgs());
            _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => JumpToSection(key, animate: false, saveAsLast: true));
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.OpenAtSection] {key}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void EnsureSteamGridDbCard()
    {
        if (_sgdbKeyBox != null) { RefreshSteamGridDbStatus(); return; }
        if (_sgdbCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Accounts") { section = sp; break; }
            if (section == null)
            {
                _sgdbCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureSteamGridDbCard] Accounts section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 9 };
            card.Child = body;
            body.Children.Add(new TextBlock { Text = Loc.GetString("MainWindow.Shelf.Artwork"), Style = FindStyle("SettingsCardTitleStyle") });
            body.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.SteamGridDb.Description"),
                Style = FindStyle("SettingsNoteStyle"),
                TextWrapping = TextWrapping.Wrap,
            });

            // Setting row: label | [key box | Save]
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
            row.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.SteamGridDb.ApiKeyLabel"),
                Style = FindStyle("SettingsLabelStyle"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            var inner = new Grid { ColumnSpacing = 6 };
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _sgdbKeyBox = new TextBox { Style = FindStyle("SettingsTextBoxStyle"), IsSpellCheckEnabled = false };
            ToolTipService.SetToolTip(_sgdbKeyBox, Loc.GetString("Settings.SteamGridDb.ApiKeyBox.Tooltip"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_sgdbKeyBox, Loc.GetString("Settings.SteamGridDb.ApiKeyLabel"));
            _sgdbKeyBox.KeyDown += (s, e) =>
            {
                if (e.Key != Windows.System.VirtualKey.Enter) return;
                e.Handled = true;
                _ = SaveSteamGridDbKeyAsync(fromButton: false);
            };
            _sgdbKeyBox.LostFocus += (s, e) => _ = SaveSteamGridDbKeyAsync(fromButton: false);
            inner.Children.Add(_sgdbKeyBox);
            _sgdbSaveBtn = MakeSgdbButton(Loc.GetString("Common.Save"), "SettingsButtonStyle", ResourceKeys.SettingsLabelBrush, Loc.GetString("Settings.SteamGridDb.SaveButton.Tooltip"));
            _sgdbSaveBtn.Click += (s, e) => _ = SaveSteamGridDbKeyAsync(fromButton: true);
            Grid.SetColumn(_sgdbSaveBtn, 1);
            inner.Children.Add(_sgdbSaveBtn);
            Grid.SetColumn(inner, 1);
            row.Children.Add(inner);
            body.Children.Add(row);

            _sgdbStatus = new TextBlock { Style = FindStyle("SettingsNoteStyle"), TextWrapping = TextWrapping.Wrap };
            body.Children.Add(_sgdbStatus);

            var buttons = new WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            var link = new HyperlinkButton
            {
                Content = Loc.GetString("Settings.SteamGridDb.GetFreeApiKey"),
                NavigateUri = new Uri(SteamGridDbService.ApiKeyPage),
                FontSize = 12,
                MinHeight = 30,
                Padding = new Thickness(12, 5, 12, 5),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Background = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
                BorderBrush = UIFactory.Brush(ResourceKeys.BorderStrongBrush),
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            };
            ToolTipService.SetToolTip(link, Loc.GetString("Settings.SteamGridDb.GetFreeApiKey.Tooltip"));
            buttons.Children.Add(link);
            _sgdbClearBtn = MakeSgdbButton(Loc.GetString("Dialog.Remove"), "SettingsDangerButtonStyle", ResourceKeys.AccentRedBrush, Loc.GetString("Settings.SteamGridDb.RemoveButton.Tooltip"));
            _sgdbClearBtn.Click += (s, e) =>
            {
                SteamGridDbService.ClearKey();
                if (_sgdbKeyBox != null) _sgdbKeyBox.Text = "";
                RefreshSteamGridDbStatus();
                SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.KeyRemoved"), ResourceKeys.TextTertiaryBrush);
            };
            buttons.Children.Add(_sgdbClearBtn);
            body.Children.Add(buttons);

            // After the existing Accounts card(s): the section's last child
            section.Children.Add(card);
            RefreshSteamGridDbStatus();
        }
        catch (Exception ex)
        {
            _sgdbCardFailed = true;
            _sgdbKeyBox = null;
            CrashReporter.Log($"[SettingsHandler.EnsureSteamGridDbCard] Failed — {ex.Message}");
        }
    }

    private static Button MakeSgdbButton(string text, string style, string brushKey, string tooltip)
    {
        var btn = new Button { Content = text, Style = FindStyle(style) };
        if (Application.Current.Resources.TryGetValue(brushKey, out var v) && v is Brush brush)
        {
            btn.Foreground = brush;
            // Keep the text colour on hover / press (WinUI hover colour sticks otherwise)
            btn.Resources["ButtonForegroundPointerOver"] = brush;
            btn.Resources["ButtonForegroundPressed"] = brush;
        }
        ToolTipService.SetToolTip(btn, tooltip);
        return btn;
    }

    private void SetSgdbStatus(string text, string brushKey)
    {
        if (_sgdbStatus == null) return;
        _sgdbStatus.Text = text;
        _sgdbStatus.Foreground = UIFactory.Brush(brushKey);
        _sgdbStatus.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshSteamGridDbStatus()
    {
        if (_sgdbKeyBox == null) return;
        var key = SteamGridDbService.ApiKey;
        _sgdbKeyBox.PlaceholderText = key == null
            ? Loc.GetString("Settings.SteamGridDb.Placeholder.PasteKey")
            : Loc.GetString("Settings.SteamGridDb.Placeholder.SavedEndsIn", Tail(key));
        if (_sgdbClearBtn != null) _sgdbClearBtn.Visibility = key == null ? Visibility.Collapsed : Visibility.Visible;
        if (_sgdbValidating) return;
        if (key == null) SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.NoKey"), ResourceKeys.TextTertiaryBrush);
        else SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.Saved"), ResourceKeys.AccentGreenBrush);
    }

    private static string Tail(string key) => key.Length <= 4 ? key : key[^4..];

    /// <summary>Checks the typed key with SteamGridDB and saves it (Enter, leaving the box, or Save).</summary>
    private async Task SaveSteamGridDbKeyAsync(bool fromButton)
    {
        if (_sgdbKeyBox == null || _sgdbValidating) return;
        try
        {
            var typed = _sgdbKeyBox.Text?.Trim() ?? "";
            if (typed.Length == 0)
            {
                if (fromButton) SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.PasteFirst"), ResourceKeys.AccentAmberDimBrush);
                return;
            }
            if (string.Equals(typed, SteamGridDbService.ApiKey, StringComparison.Ordinal))
            {
                _sgdbKeyBox.Text = "";
                RefreshSteamGridDbStatus();
                return;
            }

            _sgdbValidating = true;
            if (_sgdbSaveBtn != null) { _sgdbSaveBtn.IsEnabled = false; _sgdbSaveBtn.Content = Loc.GetString("Settings.SteamGridDb.Button.Checking"); }
            SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.Checking"), ResourceKeys.TextTertiaryBrush);
            var token = _window.LifetimeToken;
            var status = await Task.Run(() => SteamGridDbService.ValidateKeyAsync(typed, token));
            if (_window.IsShuttingDown) return;
            _sgdbValidating = false;
            if (_sgdbSaveBtn != null) { _sgdbSaveBtn.IsEnabled = true; _sgdbSaveBtn.Content = Loc.GetString("Common.Save"); }

            switch (status)
            {
                case SteamGridDbService.Status.Ok:
                    if (SteamGridDbService.SaveKey(typed) is { } err)
                    {
                        SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.SaveFailed", err), ResourceKeys.AccentRedBrush);
                        return;
                    }
                    _sgdbKeyBox.Text = "";
                    RefreshSteamGridDbStatus();
                    break;
                case SteamGridDbService.Status.BadKey:
                    SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.BadKey"), ResourceKeys.AccentRedBrush);
                    break;
                case SteamGridDbService.Status.Offline:
                    SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.Offline"), ResourceKeys.AccentAmberDimBrush);
                    break;
                default:
                    SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.Unexpected"), ResourceKeys.AccentAmberDimBrush);
                    break;
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.SaveSteamGridDbKeyAsync] {ex.GetType().Name}: {ex.Message}");
            SetSgdbStatus(Loc.GetString("Settings.SteamGridDb.Status.CheckFailed"), ResourceKeys.AccentRedBrush);
        }
        finally
        {
            _sgdbValidating = false;
            if (_sgdbSaveBtn != null && !_window.IsShuttingDown) { _sgdbSaveBtn.IsEnabled = true; _sgdbSaveBtn.Content = Loc.GetString("Common.Save"); }
        }
    }
}
