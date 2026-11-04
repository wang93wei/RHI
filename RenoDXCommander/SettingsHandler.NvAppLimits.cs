// SettingsHandler.NvAppLimits.cs -- "NVIDIA App" block in Driver Settings › Profiles: one button
// that stops the NVIDIA App resetting DLSS overrides (NvAppOverrideLimitService). Built in C#
// (XAML constraint), added once when Settings first opens.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private bool _nvAppLimitsSetupDone;
    private Button? _nvAppLimitsButton;

    private void EnsureNvAppLimitsPanel()
    {
        if (_nvAppLimitsSetupDone) return;
        _nvAppLimitsSetupDone = true;
        try
        {
            // The Profiles card's left column holds the WrapPanel with the create/backup/restore buttons.
            // Found structurally (first 3-button WrapPanel in the Driver section) so it does not depend
            // on the localised button text; the legacy text lookup is kept as a fallback.
            var column = FindProfilesColumn() ?? FindColumnOfButton(_window.SettingsCardsPanel, "Create missing profiles");
            if (column == null)
            {
                CrashReporter.Log("[SettingsHandler.EnsureNvAppLimitsPanel] Profiles column not found — NVIDIA App block not added");
                return;
            }

            column.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.Nvidia.App.Title"),
                Style = FindStyle("SettingsHeadStyle"),
                Margin = new Thickness(0, 6, 0, 0),
            });
            column.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.Nvidia.App.Help"),
                Style = FindStyle("SettingsHelpStyle"),
                TextWrapping = TextWrapping.Wrap,
            });

            _nvAppLimitsButton = new Button
            {
                Content = Loc.GetString("Settings.Nvidia.App.RemoveButton"),
                Style = FindStyle("SettingsButtonStyle"),
                Foreground = UIFactory.Brush(ResourceKeys.SettingsLabelBrush),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            ToolTipService.SetToolTip(_nvAppLimitsButton, Loc.GetString("Settings.Nvidia.App.RemoveButton.Tooltip"));
            _nvAppLimitsButton.Click += NvAppLimitsButton_Click;
            column.Children.Add(_nvAppLimitsButton);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.EnsureNvAppLimitsPanel] Failed — {ex.Message}");
        }
    }

    private async void NvAppLimitsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var xamlRoot = (sender as FrameworkElement)?.XamlRoot ?? _window.Content.XamlRoot;
            if (!NvAppOverrideLimitService.IsNvidiaAppInstalled)
            {
                await ShowNvAppLimitsMessageAsync(xamlRoot, Loc.GetString("Dialog.NvAppLimits.NotInstalled.Title"),
                    Loc.GetString("Dialog.NvAppLimits.NotInstalled.Content"));
                return;
            }

            var confirm = new ContentDialog
            {
                Title = Loc.GetString("Dialog.NvAppLimits.Confirm.Title"),
                Content = new TextBlock
                {
                    Text = Loc.GetString("Dialog.NvAppLimits.Confirm.Content"),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                },
                PrimaryButtonText = Loc.GetString("Dialog.NvAppLimits.Confirm.Primary"),
                CloseButtonText = Loc.GetString("Dialog.Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(confirm) != ContentDialogResult.Primary) return;

            if (_nvAppLimitsButton != null) { _nvAppLimitsButton.IsEnabled = false; _nvAppLimitsButton.Content = Loc.GetString("Dialog.NvAppLimits.Removing"); }
            NvAppOverrideLimitService.Result result;
            try
            {
                result = await Task.Run(NvAppOverrideLimitService.RemoveLimits);
            }
            finally
            {
                if (_nvAppLimitsButton != null) { _nvAppLimitsButton.IsEnabled = true; _nvAppLimitsButton.Content = Loc.GetString("Settings.Nvidia.App.RemoveButton"); }
            }
            CrashReporter.Log($"[SettingsHandler.NvAppLimitsButton_Click] Success={result.Success}: {result.Message}");
            if (_window.IsShuttingDown) return;
            await ShowNvAppLimitsMessageAsync(xamlRoot,
                result.Success ? Loc.GetString("Dialog.NvAppLimits.Removed.Title") : Loc.GetString("Dialog.NvAppLimits.Failed.Title"), result.Message);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.NvAppLimitsButton_Click] {ex.Message}");
        }
    }

    private static async Task ShowNvAppLimitsMessageAsync(XamlRoot? xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13, IsTextSelectionEnabled = true },
            CloseButtonText = Loc.GetString("Dialog.Ok"),
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(dialog);
    }

    /// <summary>
    /// Finds the Profiles card's left column without depending on localised button text: the first
    /// WrapPanel inside the "Driver" settings section that holds at least three buttons
    /// (Create missing profiles / Back up profiles / Restore profiles).
    /// </summary>
    private Panel? FindProfilesColumn()
    {
        StackPanel? driver = null;
        foreach (var child in _window.SettingsCardsPanel.Children)
            if (child is StackPanel sp && sp.Tag is string tag && tag == SectionTagPrefix + "Driver")
            { driver = sp; break; }
        if (driver == null) return null;

        Panel? result = null;
        bool Walk(DependencyObject? node)
        {
            if (node is Panel p)
            {
                if (p is RenoDXCommander.Controls.WrapPanel && p.Children.Count(b => b is Button) >= 3)
                {
                    result = p.Parent as Panel ?? _window.SettingsCardsPanel;
                    return true;
                }
                foreach (var c in p.Children) if (Walk(c)) return true;
                return false;
            }
            if (node is Border bd) return Walk(bd.Child);
            if (node is ContentControl cc && cc.Content is DependencyObject d) return Walk(d);
            return false;
        }
        Walk(driver);
        return result;
    }

    private static Style? FindStyle(string key)
        => Application.Current.Resources.TryGetValue(key, out var s) ? s as Style : null;

    /// <summary>Finds the Panel that contains the Panel holding a Button with this text.</summary>
    private static Panel? FindColumnOfButton(DependencyObject? root, string buttonText)
    {
        Panel? found = null;
        bool Walk(DependencyObject? node, Panel? grandparent, Panel? parent)
        {
            switch (node)
            {
                case Button b when b.Content is string s && string.Equals(s, buttonText, StringComparison.Ordinal):
                    found = grandparent;
                    return true;
                case Panel p:
                    foreach (var c in p.Children) if (Walk(c, parent, p)) return true;
                    return false;
                case Border bd: return Walk(bd.Child, grandparent, parent);
                case ContentControl cc when cc.Content is DependencyObject d: return Walk(d, grandparent, parent);
            }
            return false;
        }
        Walk(root, null, null);
        return found;
    }
}
