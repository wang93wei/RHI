// DetailPanelBuilder.Overrides.Dlss.cs — DLSS/Streamline column builder helpers.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    /// <summary>
    /// Builds a single DLSS/Streamline column with label, version ComboBox, optional preset ComboBox, and optional render scale ComboBox.
    /// </summary>
    private StackPanel BuildDlssColumn(string label, bool isPresent,
        IReadOnlyList<string> availableVersions, string? installedVersion,
        (string Name, uint Value)[]? presets, uint currentPreset,
        Func<string, Task> onVersionSelected, Action<uint>? onPresetSelected,
        uint currentRenderScale = 0, Action<uint>? onRenderScaleSelected = null,
        string? originalVersion = null, bool driverOverrideActive = false)
    {
        var col = new StackPanel { Spacing = 4, Opacity = isPresent ? 1.0 : 0.4 };

        col.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        });

        // Version ComboBox
        var versionLabel = new TextBlock { Text = Loc.GetString("Dialog.Version"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) };
        if (driverOverrideActive)
            ToolTipService.SetToolTip(versionLabel, Loc.GetString("Overrides.Dlss.DriverOverride.LabelTooltip"));
        col.Children.Add(versionLabel);

        // Version entries: Display = localized UI text, MatchKey = value used to match the
        // installed version (bare version string), Value = what onVersionSelected receives
        // ("Default" for (Default)-marked entries).
        var entries = new List<(string Display, string MatchKey, string Value)>();

        if (!isPresent && installedVersion == null)
        {
            // Game truly doesn't have this component — show "None"
            entries.Add((LocOpt.T("None"), "None", "None"));
        }
        else
        {
            string? formattedOriginal = originalVersion != null
                ? DlssStreamlineService.FormatVersion(originalVersion)
                : (installedVersion != null ? installedVersion : null);
            bool defaultInList = false;

            foreach (var ver in availableVersions)
            {
                if (formattedOriginal != null && (ver.Equals(formattedOriginal, StringComparison.OrdinalIgnoreCase)
                    || ver.StartsWith(formattedOriginal, StringComparison.OrdinalIgnoreCase)
                    || formattedOriginal.StartsWith(ver, StringComparison.OrdinalIgnoreCase)))
                {
                    entries.Add((Loc.GetString("Option.VersionDefaultFormat", ver), ver, "Default"));
                    defaultInList = true;
                }
                else
                    entries.Add((ver, ver, ver));
            }
            entries.Add((LocOpt.T("Custom"), "Custom", "Custom"));

            // If original version isn't in the managed list, insert it at top with (Default)
            if (!defaultInList && formattedOriginal != null)
                entries.Insert(0, (Loc.GetString("Option.VersionDefaultFormat", formattedOriginal), formattedOriginal, "Default"));
        }

        var items = entries.Select(e => e.Display).ToList();

        // Find selected index based on installed version
        int selectedIndex = 0;
        if (installedVersion != null && isPresent)
        {
            if (installedVersion.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = entries.Count - 1;
            }
            else
            {
                bool matched = false;
                for (int i = 0; i < items.Count; i++)
                {
                    var itemBase = entries[i].MatchKey;
                    if (installedVersion.Equals(itemBase, StringComparison.OrdinalIgnoreCase)
                        || itemBase.StartsWith(installedVersion, StringComparison.OrdinalIgnoreCase)
                        || installedVersion.StartsWith(itemBase, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        matched = true;
                        break;
                    }
                }

                // Installed version not in manifest list (e.g. early access / custom build)
                // Insert it before "Custom" so it shows correctly rather than falling back to (Default)
                if (!matched)
                {
                    var insertIdx = entries.Count - 1; // before "Custom"
                    entries.Insert(insertIdx, (installedVersion, installedVersion, installedVersion));
                    selectedIndex = insertIdx;
                }
            }
        }

        var versionCombo = new ComboBox
        {
            ItemsSource = driverOverrideActive ? new List<string> { LocOpt.T("Driver Override Active") } : items,
            SelectedIndex = driverOverrideActive ? 0 : selectedIndex,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = isPresent && !driverOverrideActive,
            Opacity = driverOverrideActive ? 0.4 : 1.0,
        };
        if (driverOverrideActive)
            ToolTipService.SetToolTip(versionCombo, Loc.GetString("Overrides.Dlss.DriverOverride.ComboTooltip"));

        // When driver override is active, tooltip is already on the combo — no extra text needed
        col.Children.Add(versionCombo);

        bool versionInit = true;
        versionCombo.SelectionChanged += async (s, ev) =>
        {
            if (versionInit) return;
            int i = versionCombo.SelectedIndex;
            if (i < 0 || i >= entries.Count) return;
            await onVersionSelected(entries[i].Value);
        };
        versionInit = false;

        // Preset ComboBox (only for SR, RR, FG)
        if (presets != null && isPresent)
        {
            col.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Preset"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });

            var presetItems = presets.Select(p => LocOpt.T(p.Name)).ToList();
            int presetIdx = 0;
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i].Value == currentPreset) { presetIdx = i; break; }
            }

            var presetCombo = new ComboBox
            {
                ItemsSource = presetItems,
                SelectedIndex = presetIdx,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = isPresent,
            };

            // Add tooltip explaining presets
            string presetTooltip = label switch
            {
                "DLSS Super Resolution" => Loc.GetString("Overrides.Dlss.PresetTooltip.Sr"),
                "Ray Reconstruction" => Loc.GetString("Overrides.Dlss.PresetTooltip.Rr"),
                "Frame Generation" => Loc.GetString("Overrides.Dlss.PresetTooltip.Fg"),
                _ => ""
            };
            if (!string.IsNullOrEmpty(presetTooltip))
                ToolTipService.SetToolTip(presetCombo, presetTooltip);

            bool presetInit = true;
            presetCombo.SelectionChanged += (s, ev) =>
            {
                if (presetInit) return;
                var idx = presetCombo.SelectedIndex;
                if (idx >= 0 && idx < presets.Length)
                    onPresetSelected?.Invoke(presets[idx].Value);
            };
            presetInit = false;
            col.Children.Add(presetCombo);
        }

        // Render Scale ComboBox (only for SR and RR)
        if (onRenderScaleSelected != null && isPresent)
        {
            col.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.RenderScale"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
            var rsOptions = DlssPresetService.RenderScaleOptions;
            var rsItems = rsOptions.Select(o => LocOpt.T(o.Name)).ToList();

            // Determine current selection
            int rsIdx = 0; // Off
            if (currentRenderScale > 0)
            {
                // Check if it matches a named option
                int namedIdx = Array.FindIndex(rsOptions, o => o.Value == currentRenderScale);
                if (namedIdx >= 0)
                    rsIdx = namedIdx;
                else
                    rsIdx = rsItems.Count - 1; // Custom
            }

            // If Custom is selected, show the percentage in the item text
            if (rsIdx == rsItems.Count - 1 && currentRenderScale > 0)
                rsItems[^1] = Loc.GetString("Option.CustomPercentFormat", currentRenderScale);

            var rsCombo = new ComboBox
            {
                ItemsSource = rsItems,
                SelectedIndex = rsIdx,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = isPresent,
            };
            ToolTipService.SetToolTip(rsCombo, Loc.GetString("Overrides.Dlss.RenderScale.Tooltip"));

            bool rsInit = true;
            rsCombo.SelectionChanged += (s, ev) =>
            {
                if (rsInit) return;
                var idx = rsCombo.SelectedIndex;
                if (idx < 0 || idx >= rsOptions.Length) return;

                if (rsOptions[idx].Name == "Custom")
                {
                    // Show a TextBox inline — replace the combo temporarily
                    var parent = rsCombo.Parent as StackPanel;
                    if (parent == null) return;
                    var comboIdx = parent.Children.IndexOf(rsCombo);
                    var inputBox = new TextBox
                    {
                        PlaceholderText = Loc.GetString("Dialog.33100"),
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        MaxLength = 3,
                    };
                    inputBox.KeyDown += (ks, ke) =>
                    {
                        if (ke.Key == Windows.System.VirtualKey.Enter)
                        {
                            if (uint.TryParse(inputBox.Text, out var val) && val >= 33 && val <= 100)
                            {
                                onRenderScaleSelected(val);
                            }
                            else
                            {
                                // Invalid — revert to Off
                                onRenderScaleSelected(0);
                            }
                        }
                        else if (ke.Key == Windows.System.VirtualKey.Escape)
                        {
                            // Cancel — revert
                            onRenderScaleSelected(currentRenderScale);
                        }
                    };
                    inputBox.LostFocus += (ls, le) =>
                    {
                        if (uint.TryParse(inputBox.Text, out var val) && val >= 33 && val <= 100)
                            onRenderScaleSelected(val);
                        else
                            onRenderScaleSelected(currentRenderScale); // revert
                    };
                    parent.Children[comboIdx] = inputBox;
                    inputBox.Focus(FocusState.Programmatic);
                }
                else
                {
                    onRenderScaleSelected(rsOptions[idx].Value);
                }
            };
            rsInit = false;
            col.Children.Add(rsCombo);
        }

        return col;
    }

    private static Border MakeDlssDivider(int column)
    {
        var divider = new Border
        {
            Width = 1,
            Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 0),
        };
        Grid.SetColumn(divider, column);
        return divider;
    }
}