// SettingsHandler.DisplayModes.cs -- Settings › Display › "Display modes" card (built in C#, XAML constraint):
// the on/off switch for the game page mode switch, the modes (add / rename / reorder / delete, default values per
// mode), the default list of what games swap, and "Switch all games". Appended to the Display section the first
// time Settings opens; rebuilt when modes change. Data: MainViewModel.DisplayModes.cs.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private StackPanel? _dmBody;
    private TextBlock? _dmStatus;
    private bool _dmCardFailed;
    private bool _dmBusy;

    private void EnsureDisplayModesCard()
    {
        if (_dmBody != null) { RefreshDisplayModesCard(); return; }
        if (_dmCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Display") { section = sp; break; }
            if (section == null)
            {
                _dmCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureDisplayModesCard] Display section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var outer = new StackPanel { Spacing = 10 };
            card.Child = outer;
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = Loc.GetString("Settings.DisplayModes.Title"), Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.DisplayModes.Intro"),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            outer.Children.Add(head);
            outer.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.DisplayModes.Note"),
                Style = FindStyle("SettingsNoteStyle"),
                Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush),
                TextWrapping = TextWrapping.Wrap,
            });

            _dmBody = new StackPanel { Spacing = 10 };
            outer.Children.Add(_dmBody);
            _dmStatus = new TextBlock { Style = FindStyle("SettingsNoteStyle"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            outer.Children.Add(_dmStatus);

            section.Children.Add(card);
            ViewModel.DisplayModesChanged += () => _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window.SettingsPanel.Visibility == Visibility.Visible) RefreshDisplayModesCard();
            });
            RefreshDisplayModesCard();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            _dmCardFailed = true;
            _dmBody = null;
            CrashReporter.Log($"[SettingsHandler.EnsureDisplayModesCard] Failed — {ex.Message}");
        }
    }

    private void RefreshDisplayModesCard()
    {
        if (_dmBody == null) return;
        _dmBody.Children.Clear();
        var vm = ViewModel;
        var store = vm.DisplayModeStore;
        var modes = store.Modes;

        // ── On / off ──
        var onCombo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
        onCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.Off") });
        onCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.On") });
        onCombo.SelectedIndex = vm.DisplayModesOn ? 1 : 0;
        ToolTipService.SetToolTip(onCombo, Loc.GetString("Settings.DisplayModes.ShowOnPages.Tooltip"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(onCombo, Loc.GetString("Settings.DisplayModes.Title"));
        onCombo.SelectionChanged += (s, e) =>
        {
            if (onCombo.SelectedIndex < 0) return;
            vm.SetDisplayModesEnabled(onCombo.SelectedIndex == 1);
        };
        _dmBody.Children.Add(SettingRow(Loc.GetString("Settings.DisplayModes.ShowOnPages"), onCombo));

        // ── Modes ──
        _dmBody.Children.Add(new TextBlock { Text = Loc.GetString("Settings.DisplayModes.ModesHeading"), Style = FindStyle("SettingsHeadStyle") });
        for (int i = 0; i < modes.Count; i++)
        {
            var m = modes[i];
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int setCount = DisplayModeItems.All.Count(it => store.ModeDefault(m.Id, it.Key) != null);
            var label = new TextBlock
            {
                Text = setCount == 0
                    ? Loc.GetString("Settings.DisplayModes.NoDefaultsSet", m.Name)
                    : Loc.GetString(setCount == 1 ? "Settings.DisplayModes.DefaultsCountOne" : "Settings.DisplayModes.DefaultsCountMany", m.Name, setCount),
                Style = FindStyle("SettingsLabelStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            row.Children.Add(label);
            var buttons = new Controls.WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            var id = m.Id;
            var name = m.Name;
            buttons.Children.Add(DmButton(Loc.GetString("Settings.DisplayModes.DefaultsButton"), DmKind.Normal, Loc.GetString("Settings.DisplayModes.DefaultsButton.Tooltip", name),
                async () => await ShowModeDefaultsAsync(id)));
            buttons.Children.Add(DmButton(Loc.GetString("Collections.Dialog.Rename"), DmKind.Normal, Loc.GetString("Settings.DisplayModes.RenameButton.Tooltip", name), async () =>
            {
                var newName = await PromptModeNameAsync(Loc.GetString("Settings.DisplayModes.RenameDialog.Title"), Loc.GetString("Settings.DisplayModes.RenameDialog.Prompt", name), Loc.GetString("Settings.DisplayModes.RenameDialog.Primary"), name, id);
                if (newName != null && vm.RenameDisplayMode(id, newName)) SetDmStatus(Loc.GetString("Settings.DisplayModes.Renamed", name, newName), false);
            }));
            // Both always shown (greyed at the ends) so the rows line up
            var up = DmButton(Loc.GetString("Settings.DisplayModes.MoveUpButton"), DmKind.Normal, Loc.GetString("Settings.DisplayModes.MoveUpButton.Tooltip"), async () => { vm.MoveDisplayMode(id, -1); await Task.CompletedTask; });
            up.IsEnabled = i > 0;
            buttons.Children.Add(up);
            var down = DmButton(Loc.GetString("Settings.DisplayModes.MoveDownButton"), DmKind.Normal, Loc.GetString("Settings.DisplayModes.MoveDownButton.Tooltip"), async () => { vm.MoveDisplayMode(id, 1); await Task.CompletedTask; });
            down.IsEnabled = i < modes.Count - 1;
            buttons.Children.Add(down);
            if (modes.Count > 1)
                buttons.Children.Add(DmButton(Loc.GetString("Dialog.Delete"), DmKind.Danger, Loc.GetString("Settings.DisplayModes.DeleteButton.Tooltip", name), async () =>
                {
                    if (!await ConfirmAsync(Loc.GetString("Settings.DisplayModes.DeleteDialog.Title"),
                            Loc.GetString("Settings.DisplayModes.DeleteDialog.Body", name),
                            Loc.GetString("Dialog.Delete"))) return;
                    var (deleted, turnedOff) = await vm.DeleteDisplayModeAsync(id);
                    if (deleted)
                        SetDmStatus(turnedOff > 0
                            ? Loc.GetString(turnedOff == 1 ? "Settings.DisplayModes.DeletedGamesOne" : "Settings.DisplayModes.DeletedGamesMany", name, turnedOff)
                            : Loc.GetString("Settings.DisplayModes.Deleted", name), false);
                }));
            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);
            _dmBody.Children.Add(row);
        }
        var addRow = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
        var addBtn = DmButton(Loc.GetString("Settings.DisplayModes.AddButton"), DmKind.Primary, Loc.GetString("Settings.DisplayModes.AddButton.Tooltip", DisplayModeStore.MaxModes), async () =>
        {
            var newName = await PromptModeNameAsync(Loc.GetString("Settings.DisplayModes.AddDialog.Title"), Loc.GetString("Settings.DisplayModes.AddDialog.Prompt"), Loc.GetString("Settings.DisplayModes.AddDialog.Primary"), "", null);
            if (newName == null) return;
            var newId = vm.AddDisplayMode(newName);
            if (newId != null) SetDmStatus(Loc.GetString("Settings.DisplayModes.Added", newName), false);
        });
        addBtn.IsEnabled = modes.Count < DisplayModeStore.MaxModes;
        addRow.Children.Add(addBtn);
        _dmBody.Children.Add(addRow);

        // ── Default list ──
        _dmBody.Children.Add(new TextBlock { Text = Loc.GetString("Settings.DisplayModes.WhatGamesSwap"), Style = FindStyle("SettingsHeadStyle") });
        var defaults = store.DefaultItems.Select(DisplayModeItems.Get).Where(i => i != null).Select(i => i!.Label).ToList();
        var listBtn = DmButton(Loc.GetString("Settings.DisplayModes.ChooseButton"), DmKind.Normal, Loc.GetString("Settings.DisplayModes.ChooseButton.Tooltip"),
            async () => await ShowDefaultItemsAsync());
        var listRow = new Grid { ColumnSpacing = 10 };
        listRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        listRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        listRow.Children.Add(new TextBlock
        {
            Text = defaults.Count == 0 ? Loc.GetString("Settings.DisplayModes.NothingChosen") : string.Join(", ", defaults),
            Style = FindStyle("SettingsLabelStyle"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(listBtn, 1);
        listRow.Children.Add(listBtn);
        _dmBody.Children.Add(listRow);
        _dmBody.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Settings.DisplayModes.DefaultListHelp"),
            Style = FindStyle("SettingsHelpStyle"),
            TextWrapping = TextWrapping.Wrap,
        });

        // ── Switch all ──
        _dmBody.Children.Add(new TextBlock { Text = Loc.GetString("Settings.DisplayModes.SwitchAllHeading"), Style = FindStyle("SettingsHeadStyle") });
        var allCombo = new ComboBox { Style = FindStyle("SettingsComboStyle"), MinWidth = 140 };
        foreach (var m in modes) allCombo.Items.Add(new ComboBoxItem { Content = m.Name });
        allCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.Off") }); // last: turns every game's mode off
        allCombo.SelectedIndex = 0;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(allCombo, Loc.GetString("Settings.DisplayModes.SwitchAllCombo.AutomationName"));
        var allBtn = DmButton(Loc.GetString("Settings.DisplayModes.SwitchButton"), DmKind.Primary, Loc.GetString("Settings.DisplayModes.SwitchButton.Tooltip"),
            async () =>
            {
                int i = allCombo.SelectedIndex;
                if (i < 0 || i > modes.Count) return;
                if (i == modes.Count) await SwitchAllAsync(null, Loc.GetString("Option.Off"));
                else await SwitchAllAsync(modes[i].Id, modes[i].Name);
            });
        allBtn.IsEnabled = vm.DisplayModesOn;
        var allGrid = new Grid { ColumnSpacing = 8 };
        allGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        allGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        allGrid.Children.Add(allCombo);
        Grid.SetColumn(allBtn, 1);
        allGrid.Children.Add(allBtn);
        _dmBody.Children.Add(SettingRow(Loc.GetString("Settings.DisplayModes.SwitchEveryGameTo"), allGrid, maxWidth: 300));
        _dmBody.Children.Add(new TextBlock
        {
            Text = vm.DisplayModesOn
                ? Loc.GetString("Settings.DisplayModes.SwitchAllHelp")
                : Loc.GetString("Settings.DisplayModes.SwitchAllHelpOff"),
            Style = FindStyle("SettingsHelpStyle"),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private Grid SettingRow(string label, FrameworkElement control, double maxWidth = 210)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = maxWidth });
        row.Children.Add(new TextBlock { Text = label, Style = FindStyle("SettingsLabelStyle"), VerticalAlignment = VerticalAlignment.Center });
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private enum DmKind { Normal, Primary, Danger }

    private Button DmButton(string text, DmKind kind, string tooltip, Func<Task> onClick)
    {
        var style = kind switch
        {
            DmKind.Primary => "SettingsPrimaryButtonStyle",
            DmKind.Danger => "SettingsDangerButtonStyle",
            _ => "SettingsButtonStyle",
        };
        var btn = new Button { Content = text, Style = FindStyle(style) };
        var key = kind switch
        {
            DmKind.Primary => "SettingsButtonBlueBrush",
            DmKind.Danger => ResourceKeys.AccentRedBrush,
            _ => ResourceKeys.SettingsLabelBrush,
        };
        if (Application.Current.Resources.TryGetValue(key, out var v) && v is Brush brush)
        {
            btn.Foreground = brush;
            // Keep the text colour on hover / press (WinUI hover colour sticks otherwise)
            btn.Resources["ButtonForegroundPointerOver"] = brush;
            btn.Resources["ButtonForegroundPressed"] = brush;
        }
        ToolTipService.SetToolTip(btn, tooltip);
        btn.Click += async (s, e) =>
        {
            if (_dmBusy) return;
            _dmBusy = true;
            try { await onClick(); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[SettingsHandler.DisplayModes] '{text}' failed — {ex.Message}");
                SetDmStatus(Loc.GetString("Settings.DisplayModes.ActionFailed", text, ex.Message), true);
            }
            finally { _dmBusy = false; }
        };
        return btn;
    }

    private void SetDmStatus(string text, bool error)
    {
        if (_dmStatus == null) return;
        _dmStatus.Text = text;
        _dmStatus.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextSecondaryBrush);
        _dmStatus.Visibility = Visibility.Visible;
    }

    // ── Dialogs ───────────────────────────────────────────────────────────────

    /// <summary>A mode's default values: one row per item, grouped like the per-game list.</summary>
    private async Task ShowModeDefaultsAsync(string modeId)
    {
        var vm = ViewModel;
        var store = vm.DisplayModeStore;
        var name = store.NameOf(modeId);
        if (name == null) return;
        var cog = new CogDialog(Loc.GetString("Settings.DisplayModes.ModeDefaults.Title", name), _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Settings.DisplayModes.ModeDefaults.Intro", name));
        foreach (var group in DisplayModeItems.Groups)
        {
            cog.AddSection(DisplayModeItems.GroupLabel(group));
            foreach (var item in DisplayModeItems.All.Where(i => i.Group == group))
            {
                var key = item.Key;
                var editor = DisplayModeEditor.Build(item, store.ModeDefault(modeId, key), Loc.GetString("DisplayMode.Value.NotSet"),
                    v => { vm.SetDisplayModeDefault(modeId, key, v); return Task.FromResult<string?>(null); },
                    (text, error) => { if (error) cog.SetError(text); else cog.SetSuccess(text); });
                cog.AddRow(item.Label, editor, item.Help);
            }
        }
        await cog.ShowAsync();
        RefreshDisplayModesCard();
    }

    /// <summary>The default list of what games swap (games without a list of their own).</summary>
    private async Task ShowDefaultItemsAsync()
    {
        var vm = ViewModel;
        var store = vm.DisplayModeStore;
        var cog = new CogDialog(Loc.GetString("Settings.DisplayModes.DefaultItems.Title"), _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Settings.DisplayModes.DefaultItems.Intro"));
        var checks = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
        var current = store.DefaultItems;
        foreach (var group in DisplayModeItems.Groups)
        {
            cog.AddSection(DisplayModeItems.GroupLabel(group));
            var grid = new Controls.AutoFitGrid { MinColumnWidth = 270 };
            foreach (var item in DisplayModeItems.All.Where(i => i.Group == group))
            {
                var cb = new CheckBox { Content = item.Label, FontSize = 12, MinWidth = 0, IsChecked = current.Contains(item.Key, StringComparer.OrdinalIgnoreCase) };
                ToolTipService.SetToolTip(cb, item.Help);
                cb.Checked += (_, _) => Save();
                cb.Unchecked += (_, _) => Save();
                checks[item.Key] = cb;
                grid.Children.Add(cb);
            }
            cog.AddContent(grid);
        }
        void Save() => vm.SetDisplayModeDefaultItems(checks.Where(c => c.Value.IsChecked == true).Select(c => c.Key).ToList());
        await cog.ShowAsync();
        RefreshDisplayModesCard();
    }

    /// <summary>Switches every game to a mode, or with a null <paramref name="modeId"/> turns every game's mode off.</summary>
    private async Task SwitchAllAsync(string? modeId, string modeName)
    {
        var vm = ViewModel;
        bool off = modeId == null;
        if (!await ConfirmAsync(off ? Loc.GetString("Settings.DisplayModes.TurnOffAll.Title") : Loc.GetString("Settings.DisplayModes.SwitchAll.Title"),
                off
                    ? Loc.GetString("Settings.DisplayModes.TurnOffAll.Body")
                    : Loc.GetString("Settings.DisplayModes.SwitchAll.Body", modeName),
                off ? Loc.GetString("Settings.DisplayModes.TurnOffAll.Primary") : Loc.GetString("Settings.DisplayModes.SwitchButton"))) return;

        var text = new TextBlock { Text = Loc.GetString("Settings.DisplayModes.Starting"), TextWrapping = TextWrapping.Wrap };
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6 };
        var dialog = new ContentDialog
        {
            Title = off ? Loc.GetString("Settings.DisplayModes.TurningOff.Title") : Loc.GetString("Settings.DisplayModes.SwitchingTo.Title", modeName),
            Content = new StackPanel { Spacing = 12, Width = 380, Children = { text, bar } },
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        (int Switched, int Running) result;
        await using (var session = await DialogService.ShowProgressAsync(dialog))
        {
            var progress = new Progress<(string msg, double pct)>(p => { text.Text = p.msg; bar.Value = p.pct; });
            result = await vm.SwitchAllDisplayModesAsync(modeId, progress, _window.LifetimeToken);
        }
        var msg = off
            ? Loc.GetString(result.Switched == 1 ? "Settings.DisplayModes.TurnedOffGamesOne" : "Settings.DisplayModes.TurnedOffGamesMany", result.Switched)
            : Loc.GetString(result.Switched == 1 ? "Settings.DisplayModes.SwitchedGamesOne" : "Settings.DisplayModes.SwitchedGamesMany", result.Switched, modeName);
        if (result.Running > 0) msg += Loc.GetString(result.Running == 1 ? "Settings.DisplayModes.RunningSkippedOne" : "Settings.DisplayModes.RunningSkippedMany", result.Running);
        SetDmStatus(msg, false);
        _window.ShowStatusBarMessage(msg, TimeSpan.FromSeconds(8));
    }

    private async Task<bool> ConfirmAsync(string title, string body, string primary)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
            PrimaryButtonText = primary,
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        return await DialogService.ShowSafeAsync(dialog) == ContentDialogResult.Primary;
    }

    /// <summary>Asks for a mode name, checked as it's typed. Null when cancelled.</summary>
    private async Task<string?> PromptModeNameAsync(string title, string prompt, string primary, string initial, string? renamingId)
    {
        var store = ViewModel.DisplayModeStore;
        var box = new TextBox { Text = initial, MaxLength = DisplayModeStore.MaxNameLength, Width = 340, PlaceholderText = Loc.GetString("Settings.DisplayModes.NamePlaceholder") };
        var error = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = TabsTheme.B("#FF9AA8"), Visibility = Visibility.Collapsed };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        panel.Children.Add(error);
        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primary,
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        string? Check() => store.ValidateName(box.Text, renamingId);
        void ShowCheck(bool showEmpty)
        {
            var problem = Check();
            dialog.IsPrimaryButtonEnabled = problem == null;
            error.Text = problem ?? "";
            error.Visibility = problem != null && (showEmpty || !string.IsNullOrWhiteSpace(box.Text)) ? Visibility.Visible : Visibility.Collapsed;
        }
        box.TextChanged += (s, e) => ShowCheck(false);
        dialog.PrimaryButtonClick += (s, e) => { if (Check() != null) { e.Cancel = true; ShowCheck(true); } };
        box.Loaded += (s, e) => { box.Focus(FocusState.Programmatic); box.SelectAll(); };
        ShowCheck(false);
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return null;
        return box.Text.Trim();
    }
}
