// DetailLayoutHost.GameSettings.cs -- Game settings (the cog beside the game's title): per-game versions of
// Settings-page options that the Tune this game sections don't cover. Spotlight = a built side panel, like
// Screenshots; Tabs = in place under the header (CogDialog host); Classic+ = a dialog (Settings button in the header).
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

        foreach (var (title, rows) in BuildGameSettingsGroups(card, openDisplayModes: null))
            root.Children.Add(TabsTheme.TitledCard(title, rows));
        return root;
    }

    /// <summary>
    /// Tabs and Classic+: the same settings as a cog (in place under the header in Tabs, a dialog in Classic+).
    /// Display modes opens its own dialog, so the cog closes first (only one dialog can be open).
    /// </summary>
    internal async Task ShowGameSettingsCogAsync(XamlRoot? xamlRoot)
    {
        if (_window.ViewModel.SelectedGame is not { } card || xamlRoot == null) return;
        bool openDisplayModes = false;
        var cog = new CogDialog(Loc.GetString("Detail.GameSettings"), xamlRoot);
        cog.AddIntro(Loc.GetString("Detail.GameSettings.Intro"));
        foreach (var (title, rows) in BuildGameSettingsGroups(card, () => { openDisplayModes = true; cog.Hide(); }))
        {
            cog.AddSection(title);
            cog.AddContent(rows);
        }
        await cog.ShowAsync();
        if (openDisplayModes) await _window.ShowDisplayModeGameDialogAsync(card);
    }

    /// <summary>The settings in groups (a titled card each in Spotlight, a section each in the cog). New rows go in the matching group.</summary>
    private List<(string Title, StackPanel Rows)> BuildGameSettingsGroups(GameCardViewModel card, Action? openDisplayModes)
    {
        StackPanel Group() => new() { Spacing = 16 };

        var game = Group();
        AddIdentityRows(game, card);          // name, bitness, Graphics API

        var launching = Group();
        AddLaunchRows(launching, card);
        AddSteamUpdateBlockRow(launching, card);

        var overlay = Group();
        AddOverlayRow(overlay, card);
        AddDisplayModeRow(overlay, card, openDisplayModes);

        var reset = Group();
        AddResetRow(reset, card);

        return new()
        {
            (Loc.GetString("Option.Game"), game),
            (Loc.GetString("Detail.GameSettings.Group.Launching"), launching),
            (Loc.GetString("Detail.GameSettings.Group.OverlayAndDisplay"), overlay),
            (Loc.GetString("Xaml.Reset"), reset),
        };
    }

    /// <summary>
    /// Steam games only: makes the game's appmanifest_&lt;appid&gt;.acf read-only so Steam can't update it.
    /// The file's attribute is the state (nothing saved), so the toggle matches the file.
    /// </summary>
    private void AddSteamUpdateBlockRow(StackPanel rows, GameCardViewModel card)
    {
        if (!string.Equals(card.Source, "Steam", StringComparison.OrdinalIgnoreCase)) return;
        var acf = SteamUpdateBlocker.FindAppManifest(card.InstallPath, card.DetectedGame?.SteamAppId);
        if (acf == null) return;

        string baseDesc = Loc.GetString("Detail.GameSettings.SteamBlock.Desc");
        var toggle = new ToggleSwitch
        {
            IsOn = SteamUpdateBlocker.IsBlocked(acf),
            OnContent = Loc.GetString("Xaml.On"),
            OffContent = Loc.GetString("Xaml.Off"),
            MinWidth = 0,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, Loc.GetString("Detail.GameSettings.SteamBlock.Label"));
        var status = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
            Visibility = Visibility.Collapsed,
        };
        bool reverting = false;
        toggle.Toggled += (s, e) =>
        {
            if (reverting) return;
            var error = SteamUpdateBlocker.SetBlocked(acf, toggle.IsOn);
            if (error == null) { status.Visibility = Visibility.Collapsed; return; }
            reverting = true;
            toggle.IsOn = !toggle.IsOn;
            reverting = false;
            status.Text = Loc.GetString("Detail.GameSettings.SteamBlock.Error", error);
            status.Visibility = Visibility.Visible;
        };
        AddGameSettingRow(rows, Loc.GetString("Detail.GameSettings.SteamBlock.Label"), baseDesc, toggle);
        rows.Children.Add(status);
    }

    /// <summary>
    /// Launch executable and launch arguments (MainViewModel.LaunchOptions.cs).
    /// Text boxes save on Enter and when focus leaves.
    /// </summary>
    private void AddLaunchRows(StackPanel rows, GameCardViewModel card)
    {
        var vm = _window.ViewModel;
        string name = card.GameName;
        const double boxWidth = 280;

        void Saved() => _window.DetailPanelBuilderInstance.QueueOverridesRebuild(name);

        // ── Launch executable: box + Browse / Reset ──
        var exeBox = new TextBox
        {
            Text = vm.GetLaunchExeOverride(name),
            PlaceholderText = Loc.GetString("Dialog.AutoDetectOrPastePath"),
            FontSize = 12,
            Width = boxWidth,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(exeBox, Loc.GetString("Overrides.LaunchExe"));
        void SaveExe()
        {
            if (exeBox.Text.Trim() == vm.GetLaunchExeOverride(name)) return;
            vm.SetLaunchExeOverride(name, exeBox.Text);
            Saved();
        }
        exeBox.LostFocus += (s, e) => SaveExe();
        exeBox.KeyDown += (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) SaveExe(); };

        var browseBtn = new Button { Content = Loc.GetString("Common.Browse"), Padding = new Thickness(14, 0, 14, 0) };
        TabsTheme.Apply(browseBtn, TabsTheme.BlueButton, height: 32, fontSize: 12);
        browseBtn.Click += async (s, e) =>
        {
            try
            {
                var path = await MainViewModel.BrowseForLaunchExeAsync(WinRT.Interop.WindowNative.GetWindowHandle(_window), card);
                if (string.IsNullOrEmpty(path)) return;
                exeBox.Text = path;
                SaveExe();
            }
            catch (Exception ex) { CrashReporter.Log($"[DetailLayoutHost.GameSettings] Browse for launch exe failed — {ex.Message}"); }
        };
        var resetBtn = new Button { Content = Loc.GetString("Overrides.LaunchExe.ResetButton"), Padding = new Thickness(14, 0, 14, 0) };
        TabsTheme.Apply(resetBtn, TabsTheme.DangerButton, height: 32, fontSize: 12);

        var exePanel = new StackPanel { Spacing = 8, Width = boxWidth };
        exePanel.Children.Add(exeBox);
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        btnRow.Children.Add(browseBtn);
        btnRow.Children.Add(resetBtn);
        exePanel.Children.Add(btnRow);
        AddGameSettingRow(rows, Loc.GetString("Overrides.LaunchExe"),
            Loc.GetString("Detail.GameSettings.LaunchExe.Desc"), exePanel);

        // ── Launch arguments ──
        var argsBox = new TextBox
        {
            Text = vm.GetLaunchArgsOverride(name),
            PlaceholderText = Loc.GetString("Dialog.LaunchArguments"),
            FontSize = 12,
            Width = boxWidth,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(argsBox, Loc.GetString("Dialog.LaunchArguments"));
        void SaveArgs()
        {
            if (argsBox.Text.Trim() == vm.GetLaunchArgsOverride(name)) return;
            vm.SetLaunchArgsOverride(name, argsBox.Text);
            Saved();
        }
        argsBox.LostFocus += (s, e) => SaveArgs();
        argsBox.KeyDown += (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) SaveArgs(); };
        string argsDesc = Loc.GetString("Detail.GameSettings.LaunchArgs.Desc");
        if (string.Equals(card.Source, "Epic", StringComparison.OrdinalIgnoreCase))
            argsDesc += Loc.GetString("Detail.GameSettings.LaunchArgs.EpicNote");
        AddGameSettingRow(rows, Loc.GetString("Dialog.LaunchArguments"), argsDesc, argsBox);

        resetBtn.Click += (s, e) =>
        {
            exeBox.Text = "";
            argsBox.Text = "";
            vm.SetLaunchExeOverride(name, "");
            vm.SetLaunchArgsOverride(name, "");
            Saved();
        };
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

    private void AddDisplayModeRow(StackPanel rows, GameCardViewModel card, Action? openDisplayModes)
    {
        var vm = _window.ViewModel;
        var btn = new Button { Content = Loc.GetString("Detail.GameSettings.Choose"), Padding = new Thickness(14, 0, 14, 0) };
        TabsTheme.Apply(btn, TabsTheme.BlueButton, height: 34, fontSize: 12);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, Loc.GetString("Detail.GameSettings.DisplayMode.Automation"));
        btn.Click += async (s, e) =>
        {
            if (openDisplayModes != null) { openDisplayModes(); return; } // a cog: it closes, then the dialog opens
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
