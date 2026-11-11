// DetailLayoutHost.GameSettings.cs -- Spotlight's Game settings panel (the cog beside the game's title): per-game
// versions of Settings-page options that the Tune this game sections don't cover. A built panel, like Screenshots.
// Rows are label + one-line description + control (AddGameSettingRow); add new settings there.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

internal sealed partial class DetailLayoutHost
{
    internal const string GameSettingsKey = "GameSettings";

    /// <summary>The panel's content (OpenPanel).</summary>
    private FrameworkElement? BuildGameSettingsPanel()
    {
        if (_window.ViewModel.SelectedGame is not { } card) return null;
        var root = new StackPanel { Spacing = 14 };
        root.Children.Add(MutedText(Loc.GetString("Detail.GameSettings.Intro")));

        var rows = new StackPanel { Spacing = 16 };
        AddOverlayRow(rows, card);
        AddDisplayModeRow(rows, card);
        root.Children.Add(TabsTheme.MakeCard(rows, 16));
        return root;
    }

    private void AddOverlayRow(StackPanel rows, GameCardViewModel card)
    {
        var vm = _window.ViewModel;
        string name = card.GameName, store = card.Source ?? "";
        var toggle = new ToggleSwitch
        {
            IsOn = !vm.IsOverlayDisabled(name, store),
            OnContent = Loc.GetString("Xaml.On"),
            OffContent = Loc.GetString("Xaml.Off"),
            MinWidth = 0,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, Loc.GetString("Overlay.Settings.EnabledLabel"));
        toggle.Toggled += (s, e) =>
        {
            try
            {
                vm.SetOverlayDisabled(name, !toggle.IsOn, store);
                _window.RefreshOverlayPassthrough();
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[DetailLayoutHost.GameSettings] Overlay toggle for '{name}' failed — {ex.Message}");
            }
        };
        string desc = Loc.GetString("Detail.GameSettings.OverlayDesc");
        if (!vm.Settings.RhiOverlayEnabled) desc += " " + Loc.GetString("Detail.GameSettings.OverlayDisabledNote");
        AddGameSettingRow(rows, Loc.GetString("Overlay.Settings.EnabledLabel"), desc, toggle);
    }

    private void AddDisplayModeRow(StackPanel rows, GameCardViewModel card)
    {
        var vm = _window.ViewModel;
        var btn = new Button { Content = Loc.GetString("Detail.GameSettings.Choose"), Padding = new Thickness(14, 0, 14, 0) };
        TabsTheme.Apply(btn, TabsTheme.BlueButton, height: 34, fontSize: 12);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, Loc.GetString("Detail.GameSettings.DisplayMode.Automation"));
        btn.Click += async (s, e) =>
        {
            try { await _window.ShowDisplayModeGameDialogAsync(card); }
            catch (Exception ex) when (ex is InvalidOperationException) { CrashReporter.Log($"[DetailLayoutHost.GameSettings] Display modes — {ex.Message}"); }
        };
        string desc = vm.DisplayModesOn
            ? Loc.GetString("Detail.GameSettings.DisplayModesDesc.On", vm.ActiveDisplayModeName(card))
            : Loc.GetString("Detail.GameSettings.DisplayModesDesc.Off");
        AddGameSettingRow(rows, Loc.GetString("Detail.GameSettings.DisplayModes"), desc, btn);
    }

    /// <summary>One setting: label and a one-line description on the left, the control on the right.</summary>
    private static void AddGameSettingRow(StackPanel rows, string label, string description, FrameworkElement control)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TabsTheme.B(TabsTheme.TextPrimary),
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
        });
        grid.Children.Add(text);

        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        rows.Children.Add(grid);
    }
}
