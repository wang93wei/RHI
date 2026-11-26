// SettingsHandler.ReLimiterPcl.cs -- "PC Latency (PCL)" row in Settings › Components › ReLimiter, under DLSS hooks.
// Built in C# (XAML constraint), added the first time Settings opens. Adds or removes the Windows account's
// Performance Log Users membership (PclPermissionService), which ReLimiter v4 needs to read NVIDIA PC Latency.
// Both directions show a warning first: the permission covers the whole Windows account, not just ReLimiter.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private Button? _pclButton;
    private TextBlock? _pclStatusText;
    private bool _pclRowFailed;
    private bool _pclBusy;
    private bool _pclMember; // what the button does next: true = Remove

    private void EnsureReLimiterPclSetting()
    {
        if (_pclButton != null) { _ = RefreshPclRowAsync(); return; }
        if (_pclRowFailed) return;
        try
        {
            if (FindRowWith(_window.SettingsCardsPanel, _window.UlDlssHooksCombo, null) is not (Grid hooksRow, StackPanel column))
            {
                _pclRowFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureReLimiterPclSetting] DLSS hooks row not found — setting not added");
                return;
            }

            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
            var label = new TextBlock
            {
                Text = Loc.GetString("Pcl.Label"),
                Style = FindStyle("SettingsLabelStyle"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(label, Loc.GetString("Pcl.Tooltip"));
            row.Children.Add(label);

            var btn = new Button
            {
                Content = Loc.GetString("Settings.SteamGridDb.Button.Checking"),
                Style = FindStyle("SettingsButtonStyle"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = false,
            };
            btn.Click += PclButton_Click;
            Grid.SetColumn(btn, 1);
            row.Children.Add(btn);

            var status = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
            };

            int at = column.Children.IndexOf(hooksRow) + 1;
            column.Children.Insert(at, row);
            column.Children.Insert(at + 1, status);
            _pclButton = btn;
            _pclStatusText = status;
            _ = RefreshPclRowAsync();
        }
        catch (Exception ex)
        {
            _pclRowFailed = true;
            CrashReporter.Log($"[SettingsHandler.EnsureReLimiterPclSetting] Failed — {ex.Message}");
        }
    }

    private async Task RefreshPclRowAsync()
    {
        if (_pclButton == null || _pclStatusText == null || _pclBusy) return;
        var s = await Task.Run(PclPermissionService.GetStatus);
        _pclMember = s.DirectMember == true;
        _pclButton.Content = _pclMember ? Loc.GetString("Pcl.Button.Remove") : Loc.GetString("Pcl.Button.Allow");
        _pclButton.IsEnabled = true;

        (string text, string brush) = s switch
        {
            { DirectMember: true, InToken: true } => (Loc.GetString("Pcl.Status.Allowed"), ResourceKeys.AccentGreenBrush),
            { DirectMember: true } => (Loc.GetString("Pcl.Status.AllowedSignOut"), ResourceKeys.AccentAmberBrush),
            { DirectMember: false, InToken: true } => (Loc.GetString("Pcl.Status.RemovedSignOut"), ResourceKeys.AccentAmberBrush),
            { DirectMember: false } => (Loc.GetString("Pcl.Status.NotAllowed"), ResourceKeys.TextTertiaryBrush),
            _ => (Loc.GetString("Pcl.Status.Unknown"), ResourceKeys.TextTertiaryBrush),
        };
        _pclStatusText.Text = text;
        _pclStatusText.Foreground = UIFactory.Brush(brush);
    }

    private async void PclButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_pclBusy || _pclButton == null || _pclStatusText == null) return;
            bool enable = !_pclMember;
            bool admin = VulkanLayerService.IsRunningAsAdmin();

            var body = new StackPanel { Spacing = 10, MaxWidth = 480 };
            void Para(string t) => body.Children.Add(new TextBlock { Text = t, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
            if (enable)
            {
                Para(Loc.GetString("Pcl.Dialog.Enable.Body1"));
                Para(Loc.GetString("Pcl.Dialog.Enable.Body2"));
                Para((admin ? "" : Loc.GetString("Pcl.Dialog.AdminPrompt") + " ")
                   + Loc.GetString("Pcl.Dialog.Enable.Body3"));
            }
            else
            {
                Para(Loc.GetString("Pcl.Dialog.Disable.Body1"));
                Para((admin ? "" : Loc.GetString("Pcl.Dialog.AdminPrompt") + " ")
                   + Loc.GetString("Pcl.Dialog.Disable.Body2"));
            }

            var dialog = new ContentDialog
            {
                Title = enable ? Loc.GetString("Pcl.Dialog.Enable.Title") : Loc.GetString("Pcl.Dialog.Disable.Title"),
                Content = body,
                PrimaryButtonText = enable ? Loc.GetString("Pcl.Dialog.AllowButton") : Loc.GetString("Dialog.Remove"),
                CloseButtonText = Loc.GetString("Dialog.Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return;

            _pclBusy = true;
            _pclButton.IsEnabled = false;
            _pclStatusText.Text = admin ? Loc.GetString("Pcl.Status.Working") : Loc.GetString("Pcl.Status.WaitingAdmin");
            _pclStatusText.Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush);
            var (ok, error) = await PclPermissionService.SetAsync(enable);
            _pclBusy = false;
            await RefreshPclRowAsync();
            if (ok)
            {
                // ReLimiter v4 games already installed get native_pcl_enabled now, not at their next ReLimiter update
                var cards = ViewModel.AllCards.ToList();
                var (written, running) = await Task.Run(() => ViewModels.MainViewModel.SetUlNativePclInV4Games(cards, enable));
                if (written > 0 || running > 0)
                    _window.ShowStatusBarMessage(
                        $"PC Latency turned {(enable ? "on" : "off")} in {written} ReLimiter v4 game{(written == 1 ? "" : "s")}"
                        + (running > 0 ? $"; {running} running game{(running == 1 ? "" : "s")} left as {(running == 1 ? "it is" : "they are")} (changed at the next ReLimiter update)" : ""),
                        TimeSpan.FromSeconds(10));
            }
            if (!ok && error != null)
            {
                _pclStatusText.Text = error;
                _pclStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush);
            }
        }
        catch (Exception ex)
        {
            _pclBusy = false;
            if (_pclButton != null) _pclButton.IsEnabled = true;
            CrashReporter.Log($"[SettingsHandler.PclButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }
}
