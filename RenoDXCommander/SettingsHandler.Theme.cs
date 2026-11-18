// SettingsHandler.Theme.cs — Settings › General › "Appearance" card (built in C#, XAML constraint). Left column:
// colour theme, a built-in theme or Custom, where the user picks nine colours with colour pickers (applied live
// through ThemeService; persisted as ColorTheme / CustomThemeColors). Right column: the interface / top bar /
// game list size rows, which SettingsHandler.UiScale.cs adds to _appearanceSizes.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private ComboBox? _themeCombo;
    private StackPanel? _themeCustomPanel;
    private TextBlock? _themeContrastNote;
    /// <summary>The Appearance card's right column, where EnsureUiScaleRow puts the size rows.</summary>
    private StackPanel? _appearanceSizes;
    private readonly Dictionary<string, (Border Swatch, TextBlock Hex)> _themeSwatches = new();
    private bool _themeCardFailed;
    private bool _themeSelecting;
    private ThemePalette? _pendingCustom;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _themeDragTimer;

    private void EnsureColorThemeCard()
    {
        if (_themeCombo != null) { SelectSavedTheme(); return; }
        if (_themeCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:General") { section = sp; break; }
            if (section == null)
            {
                _themeCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureColorThemeCard] General section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var outer = new StackPanel { Spacing = 10 };
            card.Child = outer;
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = Loc.GetString("Settings.Theme.Title"), Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.Theme.Description"),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            outer.Children.Add(head);

            // Colours left, sizes right (the usual two-column Settings grid)
            var topCols = new Grid();
            topCols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topCols.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topCols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var colourCol = new StackPanel { Spacing = 9, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Top };
            var topDivider = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush) };
            _appearanceSizes = new StackPanel { Spacing = 9, Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(topDivider, 1);
            Grid.SetColumn(_appearanceSizes, 2);
            topCols.Children.Add(colourCol);
            topCols.Children.Add(topDivider);
            topCols.Children.Add(_appearanceSizes);
            colourCol.Children.Add(new TextBlock { Text = "Colours", Style = FindStyle("SettingsHeadStyle") });
            _appearanceSizes.Children.Add(new TextBlock { Text = "Sizes", Style = FindStyle("SettingsHeadStyle") });
            outer.Children.Add(topCols);

            // Theme row
            var row = MakeThemeRow(Loc.GetString("Settings.Theme.RowLabel"), out var themeCell);
            var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            foreach (var p in ThemePresets.All) combo.Items.Add(PresetLabel(p));
            combo.Items.Add(Loc.GetString("Option.Custom"));
            ToolTipService.SetToolTip(combo, Loc.GetString("Settings.Theme.Combo.Tooltip"));
            themeCell.Child = combo;
            colourCol.Children.Add(row);
            _themeCombo = combo;
            combo.SelectionChanged += ThemeCombo_SelectionChanged;

            // Custom colours: two columns with a divider, like the other cards
            _themeCustomPanel = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };
            var cols = new Grid();
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left = new StackPanel { Spacing = 9, Margin = new Thickness(0, 0, 20, 0) };
            var right = new StackPanel { Spacing = 9, Margin = new Thickness(20, 0, 0, 0) };
            var divider = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush) };
            Grid.SetColumn(divider, 1);
            Grid.SetColumn(right, 2);
            cols.Children.Add(left);
            cols.Children.Add(divider);
            cols.Children.Add(right);

            left.Children.Add(new TextBlock { Text = Loc.GetString("Settings.Theme.Heading.Look"), Style = FindStyle("SettingsHeadStyle") });
            right.Children.Add(new TextBlock { Text = Loc.GetString("Settings.Theme.Heading.StatusColours"), Style = FindStyle("SettingsHeadStyle") });
            foreach (var (key, label) in ThemePalette.CustomKeys)
            {
                bool status = key is "Installed" or "Update" or "Warning" or "Error";
                (status ? right : left).Children.Add(MakeColourRow(key, ColourLabel(key, label)));
            }
            _themeCustomPanel.Children.Add(cols);

            _themeContrastNote = new TextBlock
            {
                Text = Loc.GetString("Settings.Theme.ContrastNote"),
                Style = FindStyle("SettingsNoteStyle"),
                Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            _themeCustomPanel.Children.Add(_themeContrastNote);

            var buttons = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            buttons.Children.Add(MakeThemeButton(Loc.GetString("Settings.Theme.Button.StartFromTheme"), Loc.GetString("Settings.Theme.Button.StartFromTheme.Tooltip"),
                (btn) => ShowStartFromMenu(btn)));
            buttons.Children.Add(MakeThemeButton(Loc.GetString("Settings.Theme.Button.Reset"), Loc.GetString("Settings.Theme.Button.Reset.Tooltip"),
                _ => SetCustom(ThemePalette.Default, save: true)));
            buttons.Children.Add(MakeThemeButton(Loc.GetString("Settings.Theme.Button.Share"), Loc.GetString("Settings.Theme.Button.Share.Tooltip"),
                (btn) => ShowShareThemeFlyout(btn)));
            buttons.Children.Add(MakeThemeButton(Loc.GetString("Settings.Theme.Button.Import"), Loc.GetString("Settings.Theme.Button.Import.Tooltip"),
                (btn) => ShowImportThemeFlyout(btn)));
            _themeCustomPanel.Children.Add(buttons);
            outer.Children.Add(_themeCustomPanel);

            section.Children.Add(card);
            SelectSavedTheme();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            _themeCardFailed = true;
            _themeCombo = null;
            _appearanceSizes = null;
            CrashReporter.Log($"[SettingsHandler.EnsureColorThemeCard] Failed — {ex.Message}");
        }
    }

    private Grid MakeThemeRow(string labelText, out Border cell)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
        row.Children.Add(new TextBlock { Text = labelText, Style = FindStyle("SettingsLabelStyle") });
        cell = new Border();
        Grid.SetColumn(cell, 1);
        row.Children.Add(cell);
        return row;
    }

    /// <summary>"Label | [swatch #RRGGBB]" — the button opens a colour picker; the swatch shows the real colour (not themed).</summary>
    private Grid MakeColourRow(string key, string label)
    {
        var row = MakeThemeRow(label, out var cell);
        var swatch = new Border
        {
            Width = 16, Height = 16, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1),
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderStrongBrush),
        };
        var hex = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        content.Children.Add(swatch);
        content.Children.Add(hex);
        var btn = new Button
        {
            Content = content,
            Style = FindStyle("SettingsButtonStyle"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        ToolTipService.SetToolTip(btn, Loc.GetString("Settings.Theme.PickColour.Tooltip", label));

        var picker = new ColorPicker
        {
            IsAlphaEnabled = false,
            IsMoreButtonVisible = false,
            IsColorSliderVisible = true,
            IsColorChannelTextInputVisible = true,
            IsHexInputVisible = true,
        };
        // Picker | preview of the game page (built on first open; recoloured live by ThemeService)
        var flyoutContent = new Grid { ColumnSpacing = 20 };
        flyoutContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        flyoutContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        flyoutContent.Children.Add(picker);
        var presenterStyle = new Style(typeof(FlyoutPresenter));
        presenterStyle.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 1000.0));
        var flyout = new Flyout { Content = flyoutContent, FlyoutPresenterStyle = presenterStyle };
        bool opening = false;
        flyout.Opening += (_, _) =>
        {
            if (flyoutContent.Children.Count == 1)
            {
                try
                {
                    var preview = BuildThemePreview();
                    Grid.SetColumn(preview, 1);
                    flyoutContent.Children.Add(preview);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
                {
                    CrashReporter.Log($"[SettingsHandler.MakeColourRow] Preview failed — {ex.Message}");
                }
            }
            opening = true;
            picker.Color = CurrentCustom().Get(key);
            opening = false;
        };
        picker.ColorChanged += (_, args) =>
        {
            if (opening) return;
            var c = Windows.UI.Color.FromArgb(255, args.NewColor.R, args.NewColor.G, args.NewColor.B);
            SetCustom(CurrentCustom().With(key, c), save: false);
        };
        flyout.Closed += (_, _) => SaveCustomNow();
        btn.Flyout = flyout;

        _themeSwatches[key] = (swatch, hex);
        cell.Child = btn;
        return row;
    }

    /// <summary>Display label for a built-in theme (theme ids are stable; the label is localised).</summary>
    private static string PresetLabel(ThemePreset p) => p.Id switch
    {
        ThemePresets.DefaultId => Loc.GetString("Option.Default"),
        "OledBlack" => Loc.GetString("Settings.Theme.Preset.OledBlack"),
        "MidnightBlue" => Loc.GetString("Settings.Theme.Preset.MidnightBlue"),
        "Ember" => Loc.GetString("Settings.Theme.Preset.Ember"),
        "Forest" => Loc.GetString("Settings.Theme.Preset.Forest"),
        "Kiro" => Loc.GetString("Settings.Theme.Preset.Kiro"),
        "Slate" => Loc.GetString("Settings.Theme.Preset.Slate"),
        "HighContrast" => Loc.GetString("Settings.Theme.Preset.HighContrast"),
        "ColourBlind" => Loc.GetString("Settings.Theme.Preset.ColourBlind"),
        _ => p.Label,
    };

    /// <summary>Display label for a custom-colour row (theme keys are stable; the label is localised).</summary>
    private static string ColourLabel(string key, string fallback) => key switch
    {
        "Background" => Loc.GetString("Settings.Theme.Colour.Background"),
        "Panel" => Loc.GetString("Settings.Theme.Colour.Panels"),
        "Text" => Loc.GetString("Settings.Theme.Colour.Text"),
        "Accent" => Loc.GetString("Settings.Theme.Colour.Accent"),
        "Buttons" => Loc.GetString("Settings.Theme.Colour.Buttons"),
        "Installed" => Loc.GetString("Settings.Theme.Colour.Installed"),
        "Update" => Loc.GetString("Settings.Theme.Colour.Update"),
        "Warning" => Loc.GetString("Settings.Theme.Colour.Warning"),
        "Error" => Loc.GetString("Settings.Theme.Colour.Error"),
        _ => fallback,
    };

    private Button MakeThemeButton(string text, string tooltip, Action<Button> click)
    {
        var btn = new Button { Content = text, Style = FindStyle("SettingsButtonStyle") };
        ToolTipService.SetToolTip(btn, tooltip);
        btn.Click += (s, _) =>
        {
            try { click((Button)s); }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
            {
                CrashReporter.Log($"[SettingsHandler.ThemeButton] {text}: {ex.Message}");
            }
        };
        return btn;
    }

    private void ShowStartFromMenu(Button anchor)
    {
        var menu = new MenuFlyout();
        foreach (var preset in ThemePresets.All)
        {
            var item = new MenuFlyoutItem { Text = PresetLabel(preset) };
            var palette = preset.Palette;
            item.Click += (_, _) => SetCustom(palette, save: true);
            menu.Items.Add(item);
        }
        menu.ShowAt(anchor);
    }

    // ── State ────────────────────────────────────────────────────────────────

    /// <summary>The custom palette being edited (unsaved drag changes first, then the saved one).</summary>
    private ThemePalette CurrentCustom() =>
        _pendingCustom ?? ThemePalette.ParseCustom(ViewModel.Settings.CustomThemeColors, ThemePalette.Default);

    private void SelectSavedTheme()
    {
        if (_themeCombo == null) return;
        _themeSelecting = true;
        try
        {
            var id = ViewModel.Settings.ColorTheme;
            int idx = string.Equals(id, ThemePresets.CustomId, StringComparison.OrdinalIgnoreCase)
                ? ThemePresets.All.Count
                : Math.Max(0, ThemePresets.All.ToList().FindIndex(p => p == ThemePresets.Find(id)));
            _themeCombo.SelectedIndex = idx;
            RefreshCustomPanel();
        }
        finally { _themeSelecting = false; }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_themeSelecting || _themeCombo == null) return;
        try
        {
            int idx = _themeCombo.SelectedIndex;
            if (idx < 0) return;
            var settings = ViewModel.Settings;
            string previous = settings.ColorTheme;
            if (idx >= ThemePresets.All.Count)
            {
                // First time on Custom: start from the theme that was showing
                if (string.IsNullOrWhiteSpace(settings.CustomThemeColors))
                    settings.CustomThemeColors = ThemePresets.Resolve(previous, null).ToCustomString();
                settings.ColorTheme = ThemePresets.CustomId;
            }
            else
            {
                settings.ColorTheme = ThemePresets.All[idx].Id;
            }
            _pendingCustom = null;
            ViewModel.SaveSettingsPublic();
            ThemeService.Apply(ThemePresets.Resolve(settings.ColorTheme, settings.CustomThemeColors));
            RefreshCustomPanel();
            CrashReporter.Log($"[SettingsHandler.ThemeCombo_SelectionChanged] Colour theme {previous} -> {settings.ColorTheme}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            CrashReporter.Log($"[SettingsHandler.ThemeCombo_SelectionChanged] {ex.Message}");
        }
    }

    /// <summary>Applies a custom palette. While a picker is dragged (<paramref name="save"/> false) changes are applied at most every 60 ms and saved when it closes.</summary>
    private void SetCustom(ThemePalette palette, bool save)
    {
        _pendingCustom = palette;
        UpdateSwatches(palette);
        if (save)
        {
            SaveCustomNow();
            return;
        }
        if (_themeDragTimer == null)
        {
            _themeDragTimer = _window.DispatcherQueue.CreateTimer();
            _themeDragTimer.Interval = TimeSpan.FromMilliseconds(60);
            _themeDragTimer.IsRepeating = false;
            _themeDragTimer.Tick += (_, _) =>
            {
                if (_pendingCustom is { } p) ThemeService.Apply(p, log: false);
            };
        }
        if (!_themeDragTimer.IsRunning) _themeDragTimer.Start();
    }

    private void SaveCustomNow()
    {
        if (_pendingCustom is not { } p) return;
        _themeDragTimer?.Stop();
        var settings = ViewModel.Settings;
        settings.CustomThemeColors = p.ToCustomString();
        settings.ColorTheme = ThemePresets.CustomId;
        _pendingCustom = null;
        ViewModel.SaveSettingsPublic();
        ThemeService.Apply(p);
        CrashReporter.Log($"[SettingsHandler.SaveCustomNow] Custom colours {settings.CustomThemeColors}");
    }

    private void RefreshCustomPanel()
    {
        if (_themeCustomPanel == null) return;
        bool custom = string.Equals(ViewModel.Settings.ColorTheme, ThemePresets.CustomId, StringComparison.OrdinalIgnoreCase);
        _themeCustomPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        if (custom) UpdateSwatches(CurrentCustom());
    }

    private void UpdateSwatches(ThemePalette p)
    {
        foreach (var (key, (swatch, hex)) in _themeSwatches)
        {
            var c = p.Get(key);
            // A plain brush on purpose: the swatch shows the picked colour itself, not a themed version
            swatch.Background = new SolidColorBrush(c);
            hex.Text = ThemePalette.ToHex(c);
        }
        if (_themeContrastNote != null)
        {
            bool low = ThemeColorMapper.Contrast(p.Text, p.Background) < 4.5
                    || ThemeColorMapper.Contrast(p.Text, p.Panel) < 4.5;
            _themeContrastNote.Visibility = low ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
