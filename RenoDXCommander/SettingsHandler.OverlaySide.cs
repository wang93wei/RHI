// SettingsHandler.OverlaySide.cs -- the "ReShade menu position" row in Settings › ReShade › Keys and
// screenshots, under Built-in add-ons. Built in C# (XAML constraint), added the first time Settings opens.
// Left / Right docks ReShade's menu on that side of the screen in every game (AuxInstallService.OverlaySide.cs).
// Saved on change; applied on the next reshade.ini write and by "Write these to all games".

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private static readonly string[] OverlaySideValues = [ReShadeOverlayDock.Default, ReShadeOverlayDock.Left, ReShadeOverlayDock.Right];

    private ComboBox? _overlaySideCombo;
    private bool _overlaySideRowFailed;

    private static int OverlaySideIndex(string? side) =>
        Math.Max(0, Array.IndexOf(OverlaySideValues, ReShadeOverlayDock.Normalize(side)));

    /// <summary>Call after EnsureShippedAddonsSetting (the row goes under it).</summary>
    private void EnsureOverlaySideSetting()
    {
        if (_overlaySideCombo != null)
        {
            _overlaySideCombo.SelectedIndex = OverlaySideIndex(ViewModel.Settings.RsOverlaySide);
            return;
        }
        if (_overlaySideRowFailed) return;
        try
        {
            // Under Built-in add-ons when that row exists, else under Effect list style
            var anchor = (FrameworkElement?)_shippedAddonsCombo ?? _window.RsVariableListUseTabsCombo;
            if (FindRowWith(_window.SettingsCardsPanel, anchor, null) is not (Grid anchorRow, StackPanel column))
            {
                _overlaySideRowFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureOverlaySideSetting] Row to add it under not found — setting not added");
                return;
            }

            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
            row.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.OverlaySide.Label"),
                Style = FindStyle("SettingsLabelStyle"),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            combo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Settings.OverlaySide.DefaultOption") });
            combo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Settings.OverlaySide.Left") });
            combo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Settings.OverlaySide.Right") });
            combo.SelectedIndex = OverlaySideIndex(ViewModel.Settings.RsOverlaySide);
            ToolTipService.SetToolTip(combo, Loc.GetString("Settings.OverlaySide.Tooltip"));
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex < 0) return;
                var side = OverlaySideValues[combo.SelectedIndex];
                if (side == ReShadeOverlayDock.Normalize(ViewModel.Settings.RsOverlaySide)) return;
                ViewModel.Settings.RsOverlaySide = side;
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.OverlaySide] ReShade menu position = {side}");
            };
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);

            column.Children.Insert(column.Children.IndexOf(anchorRow) + 1, row);
            _overlaySideCombo = combo;

            ToolTipService.SetToolTip(_window.ApplyHotkeyBtn, Loc.GetString("Settings.OverlaySide.ApplyHotkey.Tooltip"));
        }
        catch (Exception ex)
        {
            _overlaySideRowFailed = true;
            CrashReporter.Log($"[SettingsHandler.EnsureOverlaySideSetting] Failed — {ex.Message}");
        }
    }
}
