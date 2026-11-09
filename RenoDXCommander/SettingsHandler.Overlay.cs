// SettingsHandler.Overlay.cs -- the "RHI overlay" section of Settings (before Tools & Maintenance, after
// Achievements): on / off and the hotkey. Built in C# (XAML constraint), inserted the first time Settings
// opens, before the section navigation is built so it gets a jump button. See docs/rhi-overlay-plan.md.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private ComboBox? _rhiOverlayCombo;
    private ComboBox? _rhiOverlayPadCombo;
    private ComboBox? _rhiOverlayPauseCombo;
    private TextBox? _rhiOverlayHotkeyBox;
    private TextBlock? _rhiOverlayStatus;
    private bool _rhiOverlaySectionFailed;

    private void EnsureRhiOverlaySection()
    {
        if (_rhiOverlayCombo != null)
        {
            _rhiOverlayCombo.SelectedIndex = ViewModel.Settings.RhiOverlayEnabled ? 1 : 0;
            if (_rhiOverlayPadCombo != null) _rhiOverlayPadCombo.SelectedIndex = ViewModel.Settings.RhiOverlayController ? 1 : 0;
            if (_rhiOverlayPauseCombo != null) _rhiOverlayPauseCombo.SelectedIndex = ViewModel.Settings.RhiOverlayPauseGame ? 1 : 0;
            if (_rhiOverlayHotkeyBox != null) _rhiOverlayHotkeyBox.Text = HotkeyManager.FormatHotkeyDisplay(ViewModel.Settings.RhiOverlayHotkey);
            UpdateRhiOverlayStatus();
            return;
        }
        if (_rhiOverlaySectionFailed) return;
        try
        {
            var panel = _window.SettingsCardsPanel;
            int toolsIndex = -1;
            for (int i = 0; i < panel.Children.Count; i++)
                if (panel.Children[i] is StackPanel sp && sp.Tag is string t && t == "Section:Tools") { toolsIndex = i; break; }
            if (toolsIndex < 0)
            {
                _rhiOverlaySectionFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureRhiOverlaySection] Tools section not found — section not added");
                return;
            }

            var section = new StackPanel { Tag = "Section:RhiOverlay", Spacing = 12 };
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Overlay.Settings.Title"),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Overlay.Settings.Description"),
                FontSize = 12,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            });
            section.Children.Add(head);

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 12 };
            card.Child = body;
            body.Children.Add(new TextBlock { Text = Loc.GetString("Overlay.Settings.CardTitle"), Style = FindStyle("SettingsCardTitleStyle") });

            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left = new StackPanel { Spacing = 9, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Top };
            var divider = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush) };
            var right = new StackPanel { Spacing = 9, Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(divider, 1);
            Grid.SetColumn(right, 2);
            columns.Children.Add(left);
            columns.Children.Add(divider);
            columns.Children.Add(right);

            // Left: on / off
            var onRow = MakeSettingRow(Loc.GetString("Overlay.Settings.EnabledLabel"), out var onCell);
            var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            combo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.Off") });
            combo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.On") });
            combo.SelectedIndex = ViewModel.Settings.RhiOverlayEnabled ? 1 : 0;
            ToolTipService.SetToolTip(combo,
                Loc.GetString("Overlay.Settings.EnabledTooltip"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, Loc.GetString("Overlay.Settings.EnabledLabel"));
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex < 0) return;
                bool on = combo.SelectedIndex == 1;
                if (on == ViewModel.Settings.RhiOverlayEnabled) return;
                ViewModel.Settings.RhiOverlayEnabled = on;
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.RhiOverlay] {(on ? "On" : "Off")}");
                _window.ApplyRhiOverlaySetting();
                UpdateRhiOverlayStatus();
            };
            onCell.Child = combo;
            left.Children.Add(onRow);
            _rhiOverlayCombo = combo;

            var padRow = MakeSettingRow(Loc.GetString("Overlay.Settings.ControllerLabel"), out var padCell);
            var padCombo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            padCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.Off") });
            padCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.On") });
            padCombo.SelectedIndex = ViewModel.Settings.RhiOverlayController ? 1 : 0;
            ToolTipService.SetToolTip(padCombo,
                Loc.GetString("Overlay.Settings.ControllerTooltip"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(padCombo, Loc.GetString("Overlay.Settings.ControllerAutomation"));
            padCombo.SelectionChanged += (s, e) =>
            {
                if (padCombo.SelectedIndex < 0) return;
                bool on = padCombo.SelectedIndex == 1;
                if (on == ViewModel.Settings.RhiOverlayController) return;
                ViewModel.Settings.RhiOverlayController = on;
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.RhiOverlayController] {(on ? "On" : "Off")}");
                _window.ApplyRhiOverlaySetting();
            };
            padCell.Child = padCombo;
            left.Children.Add(padRow);
            _rhiOverlayPadCombo = padCombo;

            // Right: hotkey
            var keyRow = MakeSettingRow(Loc.GetString("Overlay.Settings.HotkeyLabel"), out var keyCell);
            var box = new TextBox
            {
                Style = FindStyle("SettingsTextBoxStyle"),
                IsReadOnly = true,
                Text = HotkeyManager.FormatHotkeyDisplay(ViewModel.Settings.RhiOverlayHotkey),
            };
            ToolTipService.SetToolTip(box,
                Loc.GetString("Overlay.Settings.HotkeyTooltip"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, Loc.GetString("Overlay.Settings.HotkeyAutomation"));
            box.PreviewKeyDown += RhiOverlayHotkeyBox_PreviewKeyDown;
            keyCell.Child = box;
            right.Children.Add(keyRow);
            _rhiOverlayHotkeyBox = box;

            var pauseRow = MakeSettingRow(Loc.GetString("Overlay.Settings.PauseLabel"), out var pauseCell);
            var pauseCombo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            pauseCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.Off") });
            pauseCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.On") });
            pauseCombo.SelectedIndex = ViewModel.Settings.RhiOverlayPauseGame ? 1 : 0;
            ToolTipService.SetToolTip(pauseCombo,
                Loc.GetString("Overlay.Settings.PauseTooltip"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pauseCombo, Loc.GetString("Overlay.Settings.PauseAutomation"));
            pauseCombo.SelectionChanged += (s, e) =>
            {
                if (pauseCombo.SelectedIndex < 0) return;
                bool on = pauseCombo.SelectedIndex == 1;
                if (on == ViewModel.Settings.RhiOverlayPauseGame) return;
                ViewModel.Settings.RhiOverlayPauseGame = on;
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.RhiOverlayPauseGame] {(on ? "On" : "Off")}");
            };
            pauseCell.Child = pauseCombo;
            right.Children.Add(pauseRow);
            _rhiOverlayPauseCombo = pauseCombo;

            body.Children.Add(columns);
            _rhiOverlayStatus = new TextBlock { Style = FindStyle("SettingsHelpStyle"), TextWrapping = TextWrapping.Wrap };
            body.Children.Add(_rhiOverlayStatus);
            UpdateRhiOverlayStatus();
            section.Children.Add(card);

            panel.Children.Insert(toolsIndex, section);
        }
        catch (Exception ex)
        {
            _rhiOverlaySectionFailed = true;
            _rhiOverlayCombo = null;
            CrashReporter.Log($"[SettingsHandler.EnsureRhiOverlaySection] Failed — {ex.Message}");
        }
    }

    /// <summary>A Settings row: label left, the control (put in <paramref name="cell"/>) right.</summary>
    private Grid MakeSettingRow(string label, out Border cell)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
        row.Children.Add(new TextBlock { Text = label, Style = FindStyle("SettingsLabelStyle"), VerticalAlignment = VerticalAlignment.Center });
        cell = new Border();
        Grid.SetColumn(cell, 1);
        row.Children.Add(cell);
        return row;
    }

    private void UpdateRhiOverlayStatus()
    {
        if (_rhiOverlayStatus == null) return;
        var key = HotkeyManager.FormatHotkeyDisplay(ViewModel.Settings.RhiOverlayHotkey);
        if (!ViewModel.Settings.RhiOverlayEnabled)
        {
            _rhiOverlayStatus.Text = Loc.GetString("Overlay.Settings.StatusOff");
            _rhiOverlayStatus.Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush);
        }
        else if (_window.RhiOverlayHotkeyActive)
        {
            _rhiOverlayStatus.Text = Loc.GetString("Overlay.Settings.StatusReady", key);
            _rhiOverlayStatus.Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush);
        }
        else
        {
            _rhiOverlayStatus.Text = Loc.GetString("Overlay.Settings.StatusConflict", key);
            _rhiOverlayStatus.Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush);
        }
    }

    private void RhiOverlayHotkeyBox_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var key = e.Key;
        if (IsModifierKey(key) || IsHotkeyBoxPassThroughKey(key)) return;
        e.Handled = true;

        string hotkey;
        if (key == Windows.System.VirtualKey.Back)
            hotkey = SettingsViewModel.DefaultRhiOverlayHotkey;
        else
        {
            bool shift = IsKeyDown(Windows.System.VirtualKey.Shift);
            bool ctrl = IsKeyDown(Windows.System.VirtualKey.Control);
            bool alt = IsKeyDown(Windows.System.VirtualKey.Menu);
            int vk = (int)key;
            // A key on its own (or Shift + a letter) would be taken from every program while RHI runs
            bool functionKey = vk >= 112 && vk <= 135;
            if (!ctrl && !alt && !(shift && functionKey))
            {
                if (_rhiOverlayStatus != null)
                {
                    _rhiOverlayStatus.Text = Loc.GetString("Overlay.Settings.HotkeyNeedsModifier");
                    _rhiOverlayStatus.Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush);
                }
                return;
            }
            hotkey = HotkeyManager.BuildHotkeyString(vk, shift, ctrl, alt);
        }

        if (sender is TextBox box) box.Text = HotkeyManager.FormatHotkeyDisplay(hotkey);
        if (hotkey != ViewModel.Settings.RhiOverlayHotkey)
        {
            ViewModel.Settings.RhiOverlayHotkey = hotkey;
            ViewModel.SaveSettingsPublic();
            CrashReporter.Log($"[SettingsHandler.RhiOverlayHotkey] Saved: {hotkey}");
        }
        _window.ApplyRhiOverlaySetting();
        UpdateRhiOverlayStatus();
    }

    private static bool IsKeyDown(Windows.System.VirtualKey key) =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private static bool IsModifierKey(Windows.System.VirtualKey key) =>
        key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.Shift or Windows.System.VirtualKey.Menu
            or Windows.System.VirtualKey.LeftControl or Windows.System.VirtualKey.RightControl
            or Windows.System.VirtualKey.LeftShift or Windows.System.VirtualKey.RightShift
            or Windows.System.VirtualKey.LeftMenu or Windows.System.VirtualKey.RightMenu
            or (Windows.System.VirtualKey)91 or (Windows.System.VirtualKey)92;
}
