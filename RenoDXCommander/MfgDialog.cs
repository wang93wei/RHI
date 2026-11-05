using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

/// <summary>
/// Dialog for configuring per-game Multi Frame Generation (MFG) driver profile settings.
/// Controls: FG Mode Override, Generation Factor / Dynamic Max Count, Dynamic Target FPS.
/// </summary>
public static class MfgDialog
{
    private static ILocalizationService Loc => App.Services.GetRequiredService<ILocalizationService>();
    // MFG Mode values
    private const uint MODE_OFF = 0x00000000;
    private const uint MODE_FIXED = 0x00000002;
    private const uint MODE_DYNAMIC = 0x00000004;

    // Dynamic Target FPS special values
    private const uint TARGET_FPS_OFF = 0x00000000;
    private const uint TARGET_FPS_MAX_REFRESH = 0x01000000;

    /// <summary>
    /// Shows a warning dialog on first use, then the MFG configuration dialog.
    /// Returns false if the user cancelled from the warning.
    /// </summary>
    public static async Task ShowAsync(
        DlssPresetService presetService,
        SettingsViewModel settings,
        string gameName,
        string installPath,
        XamlRoot xamlRoot,
        Action saveSettings)
    {
        // First-time warning
        if (!settings.MfgWarningDismissed)
        {
            var warningPanel = new StackPanel { Spacing = 12 };
            warningPanel.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Dialog.Mfg.WarningContent"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = TabsTheme.B(TabsTheme.AmberPill.Fg),
            });

            var dontShowCheck = new CheckBox
            {
                Content = Loc.GetString("Dialog.DonTShowThisWarning"),
                FontSize = 12,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            };
            warningPanel.Children.Add(dontShowCheck);

            warningPanel.Width = 420;
            TabsTheme.Restyle(warningPanel);
            var warningDialog = new ContentDialog
            {
                Title = CogDialog.MakeDialogTitle(Loc.GetString("Dialog.HardwareRequirement")),
                Content = TabsTheme.MakeCard(warningPanel),
                PrimaryButtonText = Loc.GetString("Dialog.Ok"),
                CloseButtonText = Loc.GetString("Dialog.Cancel"),
                XamlRoot = xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };

            CogDialog.ApplyDialogLook(warningDialog, 560);
            var warningResult = await DialogService.ShowSafeAsync(warningDialog);
            if (warningResult != ContentDialogResult.Primary)
                return;

            if (dontShowCheck.IsChecked == true)
            {
                settings.MfgWarningDismissed = true;
                saveSettings();
            }
        }

        // Show the MFG configuration dialog
        await ShowConfigDialogAsync(presetService, gameName, installPath, xamlRoot);
    }

    private static async Task ShowConfigDialogAsync(
        DlssPresetService presetService,
        string gameName,
        string installPath,
        XamlRoot xamlRoot)
    {
        // Read current values off the UI thread — these call _sessionLock.Wait(5000) and
        // will block the UI thread if NVAPI is hung (e.g. after GPU sleep/wake).
        var (currentMode, currentFactor, currentDynamicMax, currentTargetFps) =
            await Task.Run(() => (
                presetService.GetMfgMode(gameName, installPath),
                presetService.GetMfgGenerationFactor(gameName, installPath),
                presetService.GetMfgDynamicMaxCount(gameName, installPath),
                presetService.GetMfgDynamicTargetFps(gameName, installPath)
            ));

        var panel = new StackPanel { Spacing = 8, MinWidth = 300 };

        // ── FG Mode ──
        panel.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Dialog.FgMode"),
            FontSize = 12,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
        });

        var modeItems = new[] { Loc.GetString("Dialog.Mfg.ModeDefault"), Loc.GetString("Dialog.Mfg.ModeFixed"), Loc.GetString("Dialog.Mfg.ModeDynamic") };
        int modeIndex = currentMode switch
        {
            MODE_FIXED => 1,
            MODE_DYNAMIC => 2,
            _ => 0,
        };
        var modeCombo = new ComboBox
        {
            ItemsSource = modeItems,
            SelectedIndex = modeIndex,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
        };
        ToolTipService.SetToolTip(modeCombo, Loc.GetString("Dialog.Mfg.TooltipMode"));
        panel.Children.Add(modeCombo);

        // ── Frame Count ──
        panel.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Dialog.FrameCount"),
            FontSize = 12,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
            Margin = new Thickness(0, 8, 0, 0),
        });

        var countCombo = new ComboBox
        {
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
        };
        ToolTipService.SetToolTip(countCombo, Loc.GetString("Dialog.Mfg.TooltipCount"));
        panel.Children.Add(countCombo);

        // ── Target Frame Rate ──
        panel.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Dialog.TargetFrameRate"),
            FontSize = 12,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
            Margin = new Thickness(0, 8, 0, 0),
        });

        var fpsCombo = new ComboBox
        {
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
        };
        ToolTipService.SetToolTip(fpsCombo, Loc.GetString("Dialog.Mfg.TooltipFps"));
        panel.Children.Add(fpsCombo);

        // Inline custom FPS input (shown when "Custom..." is selected)
        var customFpsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Visibility = Visibility.Collapsed };
        var customFpsBox = new TextBox { PlaceholderText = Loc.GetString("Dialog.201000"), FontSize = 13, MinWidth = 100 };
        var customFpsBtn = new Button { Content = Loc.GetString("Dialog.Set"), FontSize = 12 };
        customFpsPanel.Children.Add(customFpsBox);
        customFpsPanel.Children.Add(customFpsBtn);
        panel.Children.Add(customFpsPanel);
        // ── Populate and wire up state ──
        bool suppressEvents = true;

        void PopulateCountCombo(int selectedModeIndex)
        {
            suppressEvents = true;
            countCombo.Items.Clear();

            if (selectedModeIndex == 1) // Fixed
            {
                countCombo.Items.Add("2x");
                countCombo.Items.Add("3x");
                countCombo.Items.Add("4x");
                countCombo.Items.Add("5x");
                countCombo.Items.Add("6x");
                countCombo.IsEnabled = true;

                // Select based on current generation factor
                int idx = currentFactor switch
                {
                    1 => 0, // 2x
                    2 => 1, // 3x
                    3 => 2, // 4x
                    4 => 3, // 5x
                    5 => 4, // 6x
                    _ => 0,
                };
                countCombo.SelectedIndex = idx;
            }
            else if (selectedModeIndex == 2) // Dynamic
            {
                countCombo.Items.Add(LocOpt.T("Up to 2x"));
                countCombo.Items.Add(LocOpt.T("Up to 3x"));
                countCombo.Items.Add(LocOpt.T("Up to 4x"));
                countCombo.Items.Add(LocOpt.T("Up to 5x"));
                countCombo.Items.Add(LocOpt.T("Up to 6x"));
                countCombo.IsEnabled = true;

                // Select based on current dynamic max count
                // Dynamic max: 0=Off, 2=3x, 3=4x, 4=5x, 5=6x (no 2x option for dynamic)
                // But we show "Up to 2x" as index 0 (value 1)
                int idx = currentDynamicMax switch
                {
                    1 => 0, // Up to 2x
                    2 => 1, // Up to 3x
                    3 => 2, // Up to 4x
                    4 => 3, // Up to 5x
                    5 => 4, // Up to 6x
                    _ => 0,
                };
                countCombo.SelectedIndex = idx;
            }
            else // Default
            {
                countCombo.Items.Add("—");
                countCombo.SelectedIndex = 0;
                countCombo.IsEnabled = false;
            }
            suppressEvents = false;
        }

        // VRR cap values with corresponding monitor refresh rates (shown first in the list)
        var vrrCapOptions = new (uint Fps, string Label)[]
        {
            (59, Loc.GetString("Dialog.Mfg.VrrCapLabel", 59, 60)),
            (73, Loc.GetString("Dialog.Mfg.VrrCapLabel", 73, 75)),
            (97, Loc.GetString("Dialog.Mfg.VrrCapLabel", 97, 100)),
            (116, Loc.GetString("Dialog.Mfg.VrrCapLabel", 116, 120)),
            (138, Loc.GetString("Dialog.Mfg.VrrCapLabel", 138, 144)),
            (157, Loc.GetString("Dialog.Mfg.VrrCapLabel", 157, 165)),
            (171, Loc.GetString("Dialog.Mfg.VrrCapLabel", 171, 180)),
            (189, Loc.GetString("Dialog.Mfg.VrrCapLabel", 189, 200)),
            (224, Loc.GetString("Dialog.Mfg.VrrCapLabel", 224, 240)),
            (258, Loc.GetString("Dialog.Mfg.VrrCapLabel", 258, 280)),
            (275, Loc.GetString("Dialog.Mfg.VrrCapLabel", 275, 300)),
            (324, Loc.GetString("Dialog.Mfg.VrrCapLabel", 324, 360)),
            (416, Loc.GetString("Dialog.Mfg.VrrCapLabel", 416, 480)),
            (431, Loc.GetString("Dialog.Mfg.VrrCapLabel", 431, 500)),
        };
        var vrrFpsSet = new HashSet<uint>(vrrCapOptions.Select(o => o.Fps));

        // Build the FPS items list: VRR caps only + Custom option (matches Settings page pattern)
        var fpsItemsList = new List<(uint Fps, string Label)>();
        foreach (var opt in vrrCapOptions)
            fpsItemsList.Add(opt);

        void PopulateFpsCombo(int selectedModeIndex)
        {
            suppressEvents = true;
            fpsCombo.Items.Clear();

            if (selectedModeIndex == 2) // Dynamic
            {
                fpsCombo.Items.Add(Loc.GetString("Dialog.Off"));
                fpsCombo.Items.Add(Loc.GetString("Dialog.Mfg.MaxRefreshRate"));
                foreach (var item in fpsItemsList)
                    fpsCombo.Items.Add(item.Label);

                // If current value is a custom FPS (not in VRR presets), insert it before Custom
                if (currentTargetFps > 0 && currentTargetFps != TARGET_FPS_MAX_REFRESH
                    && !vrrFpsSet.Contains(currentTargetFps))
                    fpsCombo.Items.Add($"{currentTargetFps} FPS ({Loc.GetString("Xaml.Custom")})");

                fpsCombo.Items.Add(Loc.GetString("Dialog.CustomEllipsis"));
                fpsCombo.IsEnabled = true;

                // Select based on current value
                if (currentTargetFps == TARGET_FPS_OFF)
                    fpsCombo.SelectedIndex = 0;
                else if (currentTargetFps == TARGET_FPS_MAX_REFRESH)
                    fpsCombo.SelectedIndex = 1;
                else
                {
                    int matchIdx = fpsItemsList.FindIndex(o => o.Fps == currentTargetFps);
                    if (matchIdx >= 0)
                        fpsCombo.SelectedIndex = matchIdx + 2;
                    else
                    {
                        // Custom value — select the "(Custom)" item
                        int customIdx = fpsCombo.Items.Count - 2; // before "Custom..."
                        fpsCombo.SelectedIndex = customIdx;
                    }
                }
            }
            else // Default or Fixed
            {
                fpsCombo.Items.Add("—");
                fpsCombo.SelectedIndex = 0;
                fpsCombo.IsEnabled = false;
            }
            suppressEvents = false;
        }

        // Initial population
        PopulateCountCombo(modeIndex);
        PopulateFpsCombo(modeIndex);
        suppressEvents = false;

        // ── Event handlers ──
        modeCombo.SelectionChanged += (s, ev) =>
        {
            if (suppressEvents) return;
            var idx = modeCombo.SelectedIndex;
            uint modeValue = idx switch
            {
                1 => MODE_FIXED,
                2 => MODE_DYNAMIC,
                _ => MODE_OFF,
            };

            // Reset dependent UI state on the UI thread before any NVAPI calls
            if (modeValue == MODE_OFF)
            {
                currentFactor = 0;
                currentDynamicMax = 0;
                currentTargetFps = 0;
            }
            else if (modeValue == MODE_FIXED)
            {
                currentDynamicMax = 0;
                currentTargetFps = 0;
            }
            else if (modeValue == MODE_DYNAMIC)
            {
                currentFactor = 0;
                // Re-read effective values after the NVAPI calls complete and update UI via TryEnqueue
            }

            PopulateCountCombo(idx);
            PopulateFpsCombo(idx);

            // Capture values for the lambda
            string capturedGame = gameName;
            string capturedPath = installPath;
            uint capturedMode = modeValue;
            int capturedIdx = idx;

            _ = Task.Run(() =>
            {
                presetService.SetMfgMode(capturedGame, capturedPath, capturedMode);

                if (capturedMode == MODE_OFF)
                {
                    presetService.SetMfgGenerationFactor(capturedGame, capturedPath, 0);
                    presetService.DeleteMfgDynamicMaxCount(capturedGame, capturedPath);
                    presetService.DeleteMfgDynamicTargetFps(capturedGame, capturedPath);
                }
                else if (capturedMode == MODE_FIXED)
                {
                    presetService.DeleteMfgDynamicMaxCount(capturedGame, capturedPath);
                    presetService.DeleteMfgDynamicTargetFps(capturedGame, capturedPath);
                }
                else if (capturedMode == MODE_DYNAMIC)
                {
                    presetService.SetMfgGenerationFactor(capturedGame, capturedPath, 0);
                    presetService.DeleteMfgDynamicMaxCount(capturedGame, capturedPath);
                    presetService.DeleteMfgDynamicTargetFps(capturedGame, capturedPath);
                    // Read effective inherited values and update UI combos
                    var newDynamicMax = presetService.GetMfgDynamicMaxCount(capturedGame, capturedPath);
                    var newTargetFps = presetService.GetMfgDynamicTargetFps(capturedGame, capturedPath);
                    modeCombo.DispatcherQueue?.TryEnqueue(() =>
                    {
                        currentDynamicMax = newDynamicMax;
                        currentTargetFps = newTargetFps;
                        PopulateCountCombo(capturedIdx);
                        PopulateFpsCombo(capturedIdx);
                    });
                }
            });
        };

        countCombo.SelectionChanged += (s, ev) =>
        {
            if (suppressEvents) return;
            var idx = countCombo.SelectedIndex;
            if (idx < 0) return;

            var currentModeIdx = modeCombo.SelectedIndex;
            if (currentModeIdx == 1) // Fixed → Generation Factor
            {
                // 2x=1, 3x=2, 4x=3, 5x=4, 6x=5
                uint value = (uint)(idx + 1);
                currentFactor = value;
                string capturedGame = gameName;
                string capturedPath = installPath;
                uint capturedValue = value;
                _ = Task.Run(() => presetService.SetMfgGenerationFactor(capturedGame, capturedPath, capturedValue));
            }
            else if (currentModeIdx == 2) // Dynamic → Dynamic Max Count
            {
                // Up to 2x=1, Up to 3x=2, Up to 4x=3, Up to 5x=4, Up to 6x=5
                uint value = (uint)(idx + 1);
                currentDynamicMax = value;
                string capturedGame = gameName;
                string capturedPath = installPath;
                uint capturedValue = value;
                _ = Task.Run(() => presetService.SetMfgDynamicMaxCount(capturedGame, capturedPath, capturedValue));
            }
        };

        fpsCombo.SelectionChanged += (s, ev) =>
        {
            if (suppressEvents) return;
            var idx = fpsCombo.SelectedIndex;
            if (idx < 0) return;

            var selectedText = fpsCombo.SelectedItem as string ?? "";

            // "Custom..." shows inline TextBox for manual entry
            if (selectedText == Loc.GetString("Dialog.CustomEllipsis"))
            {
                customFpsPanel.Visibility = Visibility.Visible;
                customFpsBox.Text = "";
                customFpsBox.Focus(FocusState.Programmatic);
                return;
            }

            customFpsPanel.Visibility = Visibility.Collapsed;

            uint value;
            if (idx == 0) value = TARGET_FPS_OFF;
            else if (idx == 1) value = TARGET_FPS_MAX_REFRESH;
            else if (idx - 2 < fpsItemsList.Count)
                value = fpsItemsList[idx - 2].Fps;
            else
                return; // Custom label item — don't set

            currentTargetFps = value;
            string capturedGame = gameName;
            string capturedPath = installPath;
            uint capturedValue = value;
            _ = Task.Run(() => presetService.SetMfgDynamicTargetFps(capturedGame, capturedPath, capturedValue));
        };

        // Custom FPS "Set" button handler
        customFpsBtn.Click += (s, ev) =>
        {
            if (uint.TryParse(customFpsBox.Text, out var customFps) && customFps >= 20 && customFps <= 1000)
            {
                currentTargetFps = customFps;
                customFpsPanel.Visibility = Visibility.Collapsed;
                PopulateFpsCombo(modeCombo.SelectedIndex);
                string capturedGame = gameName;
                string capturedPath = installPath;
                uint capturedFps = customFps;
                _ = Task.Run(() => presetService.SetMfgDynamicTargetFps(capturedGame, capturedPath, capturedFps));
            }
        };
        customFpsBox.KeyDown += (s, ev) =>
        {
            if (ev.Key == Windows.System.VirtualKey.Enter)
            {
                if (uint.TryParse(customFpsBox.Text, out var customFps) && customFps >= 20 && customFps <= 1000)
                {
                    currentTargetFps = customFps;
                    customFpsPanel.Visibility = Visibility.Collapsed;
                    PopulateFpsCombo(modeCombo.SelectedIndex);
                    string capturedGame = gameName;
                    string capturedPath = installPath;
                    uint capturedFps = customFps;
                    _ = Task.Run(() => presetService.SetMfgDynamicTargetFps(capturedGame, capturedPath, capturedFps));
                }
            }
        };

        // ── Show dialog ──
        panel.Width = 380;
        TabsTheme.Restyle(panel);
        customFpsBox.Width = 120; // Restyle stretches inputs; this one sits in a row beside its Set button
        customFpsBtn.Width = double.NaN;
        var dialog = new ContentDialog
        {
            Title = CogDialog.MakeDialogTitle(
                Loc.GetString("Dialog.MultiFrameGeneration"),
                Loc.GetString("Dialog.MultiFrameGeneration.Subtitle")),
            Content = TabsTheme.MakeCard(panel),
            CloseButtonText = Loc.GetString("Dialog.Close"),
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        CogDialog.ApplyDialogLook(dialog, 520);

        await DialogService.ShowSafeAsync(dialog);
    }
}
