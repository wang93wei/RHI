// DetailPanelBuilder.Overrides.DriverSettings.cs — Driver Profile Settings (VSync, Latency, Smooth Motion, Power/CPU, ReBAR).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private void BuildDriverProfileSection(GameCardViewModel card, string capturedName)
    {

        // ══════════════════════════════════════════════════════════════════════
        // Nvidia Profile Settings — VSync, Latency, Smooth Motion, Power/CPU, ReBAR
        // ══════════════════════════════════════════════════════════════════════
        var nvidiaPresetService = _dlssPresetService;
        if (nvidiaPresetService.IsSupported)
        {
            bool isAdmin = VulkanLayerService.IsRunningAsAdmin();

            _window.NvidiaProfilePanel.Children.Add(UIFactory.MakeSeparator());

            var nvidiaGrid = new Grid { ColumnSpacing = 12, Opacity = isAdmin ? 1.0 : 0.4, IsHitTestVisible = isAdmin };
            // 4 columns with dividers between: col0 | div1 | col2 | div3 | col4 | div5 | col6
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var installPathSafe = card.InstallPath ?? "";

            // ── Column 0: VSync ──
            var vsyncCol = new StackPanel { Spacing = 4 };
            var vsyncLabel = new TextBlock { Text = Loc.GetString("Xaml.Vsync"), FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
            ToolTipService.SetToolTip(vsyncLabel, Loc.GetString("Overrides.Vsync.Tooltip"));
            vsyncCol.Children.Add(vsyncLabel);

            // VSync Mode
            {
                vsyncCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Mode"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.VSyncModeOptions;
                uint current = nvidiaPresetService.GetVSyncMode(card.GameName, installPathSafe);
                var globalVSync = nvidiaPresetService.GetGlobalVSyncMode();

                var itemsList = new List<string>();
                if (globalVSync.HasValue)
                {
                    var globalName = options.FirstOrDefault(o => o.Value == globalVSync.Value).Name ?? "App Controlled";
                    itemsList.Add(LocOpt.Global(LocOpt.T(globalName)));
                }
                itemsList.AddRange(options.Select(o => LocOpt.T(o.Name)));
                var items = itemsList.ToArray();

                // Determine selected index
                int idx;
                if (globalVSync.HasValue)
                {
                    bool perGameMatchesGlobal = current == globalVSync.Value;
                    var perGameIdx = Array.FindIndex(options, o => o.Value == current);
                    idx = perGameMatchesGlobal ? 0 : (perGameIdx >= 0 ? perGameIdx + 1 : 0);
                }
                else
                {
                    idx = Array.FindIndex(options, o => o.Value == current);
                    if (idx < 0) idx = 0;
                }

                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                };
                ToolTipService.SetToolTip(combo, globalVSync.HasValue
                    ? Loc.GetString("Overrides.VsyncMode.Tooltip.Global")
                    : Loc.GetString("Overrides.VsyncMode.Tooltip"));
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= items.Length) return;
                    if (globalVSync.HasValue && i == 0) // "Global (...)" entry
                    {
                        // Inherit from global — write the global value
                        nvidiaPresetService.SetVSyncMode(card.GameName, installPathSafe, globalVSync ?? options[0].Value);
                    }
                    else
                    {
                        int optIdx = globalVSync.HasValue ? i - 1 : i;
                        if (optIdx >= 0 && optIdx < options.Length)
                            nvidiaPresetService.SetVSyncMode(card.GameName, installPathSafe, options[optIdx].Value);
                    }
                };
                vsyncCol.Children.Add(combo);
                init = false;
            }

            // VSync Tear Control
            {
                vsyncCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.TearControl"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.VSyncTearControlOptions;
                uint current = nvidiaPresetService.GetVSyncTearControl(card.GameName, installPathSafe);
                var items = options.Select(o => LocOpt.T(o.Name)).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                };
                ToolTipService.SetToolTip(combo, Loc.GetString("Overrides.TearControl.Tooltip"));
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    nvidiaPresetService.SetVSyncTearControl(card.GameName, installPathSafe, options[i].Value);
                };
                vsyncCol.Children.Add(combo);
                init = false;
            }

            // Low Latency Mode (in VSync column)
            {
                vsyncCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.LowLatency"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.LowLatencyModeOptions;
                uint current = nvidiaPresetService.GetLowLatencyMode(card.GameName, installPathSafe);
                var items = options.Select(o => LocOpt.T(o.Name)).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                // Locked to Ultra while Smooth Motion is enabled — must turn off Smooth Motion first
                bool smoothOn = nvidiaPresetService.GetSmoothMotionEnable(card.GameName, installPathSafe) != 0;
                bool latencyLocked = smoothOn;
                var combo2 = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    IsEnabled = !latencyLocked,
                    Opacity = latencyLocked ? 0.4 : 1.0,
                };
                ToolTipService.SetToolTip(combo2, latencyLocked
                    ? Loc.GetString("Overrides.LowLatency.Tooltip.Locked")
                    : Loc.GetString("Overrides.LowLatency.Tooltip"));
                var init2 = true;
                combo2.SelectionChanged += (s, ev) =>
                {
                    if (init2) return;
                    int i = combo2.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    nvidiaPresetService.SetLowLatencyMode(card.GameName, installPathSafe, options[i].Value);
                };
                vsyncCol.Children.Add(combo2);
                init2 = false;
            }

            Grid.SetColumn(vsyncCol, 0);
            nvidiaGrid.Children.Add(vsyncCol);
            nvidiaGrid.Children.Add(MakeDlssDivider(1));

            // ── Column 4: Smooth Motion ──
            var smoothCol = new StackPanel { Spacing = 4 };
            var smoothLabel = new TextBlock { Text = Loc.GetString("Dialog.SmoothMotion"), FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
            ToolTipService.SetToolTip(smoothLabel, Loc.GetString("Overrides.SmoothMotion.Tooltip"));
            smoothCol.Children.Add(smoothLabel);

            // Enable
            bool smoothMotionEnabled;
            {
                smoothCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Enable"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.SmoothMotionEnableOptions;
                uint current = nvidiaPresetService.GetSmoothMotionEnable(card.GameName, installPathSafe);
                smoothMotionEnabled = current != 0;
                var items = options.Select(o => LocOpt.T(o.Name)).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                };
                ToolTipService.SetToolTip(combo, Loc.GetString("Overrides.SmoothMotionEnable.Tooltip"));
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    nvidiaPresetService.SetSmoothMotionEnable(card.GameName, installPathSafe, options[i].Value);
                    // Cascade: set APIs to All when enabling, None when disabling
                    bool enabling = options[i].Value != 0;
                    nvidiaPresetService.SetSmoothMotionApis(card.GameName, installPathSafe, enabling ? 0x00000007u : 0x00000000u);
                    // Cascade Low Latency: Ultra when enabling, restore previous value when disabling
                    const uint LowLatencyOff   = 0x00000000;
                    const uint LowLatencyUltra = 0x00000002;
                    if (enabling)
                    {
                        uint prevLatency = nvidiaPresetService.GetLowLatencyMode(card.GameName, installPathSafe);
                        // Save previous value only if it's not already Ultra (nothing to restore)
                        card.PreSmoothMotionLowLatency = prevLatency != LowLatencyUltra ? prevLatency : (uint?)null;
                        nvidiaPresetService.SetLowLatencyMode(card.GameName, installPathSafe, LowLatencyUltra);
                    }
                    else
                    {
                        uint restoreValue = card.PreSmoothMotionLowLatency ?? LowLatencyOff;
                        nvidiaPresetService.SetLowLatencyMode(card.GameName, installPathSafe, restoreValue);
                        card.PreSmoothMotionLowLatency = null;
                    }
                    _window.DispatcherQueue?.TryEnqueue(() => BuildOverridesPanel(card));
                };
                smoothCol.Children.Add(combo);
                init = false;
            }

            // APIs
            {
                smoothCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.AllowedApis"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.SmoothMotionApisOptions;
                uint current = nvidiaPresetService.GetSmoothMotionApis(card.GameName, installPathSafe);
                var items = options.Select(o => LocOpt.T(o.Name)).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    IsEnabled = smoothMotionEnabled,
                    Opacity = smoothMotionEnabled ? 1.0 : 0.4,
                };
                ToolTipService.SetToolTip(combo, Loc.GetString("Overrides.SmoothMotionApis.Tooltip"));
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    nvidiaPresetService.SetSmoothMotionApis(card.GameName, installPathSafe, options[i].Value);
                };
                smoothCol.Children.Add(combo);
                init = false;
            }

            // Flip Pacing (combined — sets both Fullscreen and Windowed together)
            {
                smoothCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.FlipPacing"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.SmoothMotionFlipPacingFsOptions;
                uint current = nvidiaPresetService.GetSmoothMotionFlipPacingFs(card.GameName, installPathSafe);
                var items = options.Select(o => LocOpt.T(o.Name)).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    IsEnabled = smoothMotionEnabled,
                    Opacity = smoothMotionEnabled ? 1.0 : 0.4,
                };
                ToolTipService.SetToolTip(combo, Loc.GetString("Overrides.FlipPacing.Tooltip"));
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    nvidiaPresetService.SetSmoothMotionFlipPacingFs(card.GameName, installPathSafe, options[i].Value);
                    // Also set windowed pacing to the same value (use 0x00000001 for "On" instead of 0xFFFFFFFF)
                    uint winValue = options[i].Value == 0xFFFFFFFF ? 0x00000001 : options[i].Value;
                    nvidiaPresetService.SetSmoothMotionFlipPacingWin(card.GameName, installPathSafe, winValue);
                };
                smoothCol.Children.Add(combo);
                init = false;
            }

            Grid.SetColumn(smoothCol, 4);
            nvidiaGrid.Children.Add(smoothCol);
            nvidiaGrid.Children.Add(MakeDlssDivider(5));

            // ── Column 6: Other (Power, G-Sync, Restore) ──
            var powerCol = new StackPanel { Spacing = 4 };
            var powerLabel = new TextBlock { Text = Loc.GetString("Xaml.Other"), FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
            ToolTipService.SetToolTip(powerLabel, Loc.GetString("Overrides.Power.Tooltip"));
            powerCol.Children.Add(powerLabel);

            // Power Management Mode
            {
                powerCol.Children.Add(new TextBlock { Text = Loc.GetString("Xaml.PowerMode"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.PowerManagementOptions;
                uint current = nvidiaPresetService.GetPowerManagementMode(card.GameName, installPathSafe);
                var items = options.Select(o => LocOpt.T(o.Name)).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                };
                ToolTipService.SetToolTip(combo, Loc.GetString("Overrides.PowerMode.Tooltip"));
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    nvidiaPresetService.SetPowerManagementMode(card.GameName, installPathSafe, options[i].Value);
                };
                powerCol.Children.Add(combo);
                init = false;
            }

            // G-Sync per-game toggle
            {
                powerCol.Children.Add(new TextBlock { Text = Loc.GetString("Xaml.GSync"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                bool gsyncEnabled = nvidiaPresetService.GetPerGameGSyncEnabled(card.GameName, installPathSafe);
                var gsyncCombo = new ComboBox
                {
                    ItemsSource = new[] { Loc.GetString("Xaml.Enabled"), Loc.GetString("Xaml.Disabled") },
                    SelectedIndex = gsyncEnabled ? 0 : 1,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                };
                ToolTipService.SetToolTip(gsyncCombo, Loc.GetString("Overrides.GSync.Tooltip"));
                var gsyncInit = true;
                gsyncCombo.SelectionChanged += (s, ev) =>
                {
                    if (gsyncInit) return;
                    bool enabled = gsyncCombo.SelectedIndex == 0;
                    nvidiaPresetService.SetPerGameGSyncEnabled(card.GameName, installPathSafe, enabled);
                };
                powerCol.Children.Add(gsyncCombo);
                gsyncInit = false;
            }

            // Restore Profile Defaults button (label spacer to align with 3rd row combos)
            powerCol.Children.Add(new TextBlock { Text = " ", FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });
            var restoreProfileBtn = new Button
            {
                Content = Loc.GetString("Dialog.RestoreDefaults"),
                FontSize = 11,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
                Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
                BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                IsEnabled = nvidiaPresetService.IsSupported,
            };
            ToolTipService.SetToolTip(restoreProfileBtn,
                Loc.GetString("Overrides.RestoreProfile.Tooltip"));
            restoreProfileBtn.Click += async (s, ev) =>
            {
                var xamlRoot = (s as FrameworkElement)?.XamlRoot ?? _window.Content.XamlRoot;
                var warningDialog = new ContentDialog
                {
                    Title = Loc.GetString("Dialog.RestoreDriverSettings"),
                    Content = new TextBlock
                    {
                        Text = Loc.GetString("Dialog.RestoreDriverSettings.Content", capturedName),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13,
                    },
                    PrimaryButtonText = Loc.GetString("Dialog.Restore"),
                    CloseButtonText = Loc.GetString("Dialog.Cancel"),
                    XamlRoot = xamlRoot,
                    RequestedTheme = ElementTheme.Dark,
                };

                var result = await DialogService.ShowSafeAsync(warningDialog);
                if (result != ContentDialogResult.Primary) return;

                var success = nvidiaPresetService.RestoreProfileDefaults(capturedName, installPathSafe);
                if (success)
                {
                    var refreshCard = _window.ViewModel.AllCards.FirstOrDefault(c =>
                        c.GameName.Equals(capturedName, StringComparison.OrdinalIgnoreCase));
                    if (refreshCard != null)
                    {
                        // Also restore DLSS/Streamline DLLs to originals
                        if (refreshCard.DlssDetection != null)
                        {
                            var dlssSvc = _dlssStreamlineService;
                            dlssSvc.RestoreAll(refreshCard.DlssDetection);
                            refreshCard.RefreshDlssVersions(dlssSvc);
                        }
                        _window.DispatcherQueue?.TryEnqueue(() => BuildOverridesPanel(refreshCard));
                    }
                }
            };
            powerCol.Children.Add(restoreProfileBtn);

            Grid.SetColumn(powerCol, 6);
            nvidiaGrid.Children.Add(powerCol);

            // ── Column 8: ReBAR ──
            var rebarCol = new StackPanel { Spacing = 4 };
            var rebarLabel = new TextBlock { Text = Loc.GetString("Xaml.Rebar"), FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
            ToolTipService.SetToolTip(rebarLabel, Loc.GetString("Overrides.Rebar.Tooltip"));
            rebarCol.Children.Add(rebarLabel);

            bool rebarEnabled = nvidiaPresetService.GetReBarEnabled(card.GameName, installPathSafe);
            ulong rebarSizeLimit = nvidiaPresetService.GetReBarSizeLimit(card.GameName, installPathSafe);
            var globalReBarState = nvidiaPresetService.GetGlobalReBarEnabled();

            // Enable — with Global (On/Off) option when global is set
            {
                rebarCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Enable"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var enableItems = new List<string>();
                if (globalReBarState.HasValue)
                    enableItems.Add(LocOpt.Global(LocOpt.T(globalReBarState.Value ? "On" : "Off")));
                enableItems.Add(LocOpt.T("Off"));
                enableItems.Add(LocOpt.T("On"));

                // Determine selected index
                int enableIdx;
                if (globalReBarState.HasValue)
                {
                    // If per-game matches global, show "Global" selected; otherwise show the per-game value
                    bool perGameMatchesGlobal = rebarEnabled == globalReBarState.Value;
                    enableIdx = perGameMatchesGlobal ? 0 : (rebarEnabled ? 2 : 1); // Global=0, Off=1, On=2
                }
                else
                {
                    enableIdx = rebarEnabled ? 1 : 0; // Off=0, On=1
                }

                var rebarEnableCombo = new ComboBox
                {
                    ItemsSource = enableItems,
                    SelectedIndex = enableIdx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                };
                ToolTipService.SetToolTip(rebarEnableCombo, globalReBarState.HasValue
                    ? Loc.GetString("Overrides.RebarEnable.Tooltip.Global")
                    : Loc.GetString("Overrides.RebarEnable.Tooltip"));
                var rebarComboInit = true;
                rebarEnableCombo.SelectionChanged += (s, ev) =>
                {
                    if (rebarComboInit) return;
                    int selIdx = rebarEnableCombo.SelectedIndex;
                    if (selIdx < 0) return;
                    if (globalReBarState.HasValue && selIdx == 0) // "Global (...)" entry
                    {
                        // Remove per-game override — inherit from global
                        // Delete the per-game setting by setting it to match global
                        bool globalVal = globalReBarState ?? false;
                        nvidiaPresetService.SetReBarEnabled(card.GameName, installPathSafe, globalVal, 2);
                    }
                    else
                    {
                        // Global=0, Off=1, On=2 when global is set; otherwise Off=0, On=1
                        bool enabling = globalReBarState.HasValue ? selIdx == 2 : selIdx == 1;
                        nvidiaPresetService.SetReBarEnabled(card.GameName, installPathSafe, enabling, 2);
                    }
                    _window.DispatcherQueue?.TryEnqueue(() => BuildOverridesPanel(card));
                };
                rebarCol.Children.Add(rebarEnableCombo);
                rebarComboInit = false;
            }

            // Mode
            {
                rebarCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Mode"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                uint rebarMode = nvidiaPresetService.GetReBarMode(card.GameName, installPathSafe);
                var modeItems = DlssPresetService.ReBarModes.Select(m => LocOpt.T(m.Name)).ToList();

                // Select current effective mode: per-game value, or default to Standard (index 0)
                int modeIdx = Array.FindIndex(DlssPresetService.ReBarModes, m => m.Value == rebarMode);
                if (modeIdx < 0) modeIdx = 0;

                var rebarModeCombo = new ComboBox
                {
                    ItemsSource = modeItems,
                    SelectedIndex = modeIdx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    IsEnabled = rebarEnabled,
                    Opacity = rebarEnabled ? 1.0 : 0.4,
                };
                ToolTipService.SetToolTip(rebarModeCombo, Loc.GetString("Overrides.RebarMode.Tooltip"));
                var modeComboInit = true;
                rebarModeCombo.SelectionChanged += (s, ev) =>
                {
                    if (modeComboInit) return;
                    int idx = rebarModeCombo.SelectedIndex;
                    if (idx < 0) return;
                    uint newMode = DlssPresetService.ReBarModes[idx].Value;
                    nvidiaPresetService.SetReBarMode(card.GameName, installPathSafe, newMode);
                };
                rebarCol.Children.Add(rebarModeCombo);
                modeComboInit = false;
            }

            // Size Limit — always shows actual size values (no Global option)
            {
                rebarCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.SizeLimit"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });

                var sizeItems = new List<string>();
                var sizeValues = new List<ulong>();
                foreach (var sl in DlssPresetService.ReBarSizeLimits)
                {
                    sizeItems.Add(LocOpt.T(sl.Name));
                    sizeValues.Add(sl.Value);
                }

                // Select the current effective size: per-game override, or global, or 1GB default
                ulong globalSize = nvidiaPresetService.GetGlobalReBarSizeLimit();
                ulong effectiveSize = rebarSizeLimit != 0 ? rebarSizeLimit : (globalSize != 0 ? globalSize : 0x0000000040000000);
                int sizeIdx;
                var matchIdx = sizeValues.IndexOf(effectiveSize);
                sizeIdx = matchIdx >= 0 ? matchIdx : 1; // Default: 1GB (index 1)

                var rebarSizeCombo = new ComboBox
                {
                    ItemsSource = sizeItems,
                    SelectedIndex = sizeIdx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    IsEnabled = rebarEnabled,
                    Opacity = rebarEnabled ? 1.0 : 0.4,
                };
                ToolTipService.SetToolTip(rebarSizeCombo, Loc.GetString("Overrides.RebarSize.Tooltip"));
                var sizeComboInit = true;
                rebarSizeCombo.SelectionChanged += (s, ev) =>
                {
                    if (sizeComboInit) return;
                    int idx = rebarSizeCombo.SelectedIndex;
                    if (idx < 0) return;
                    ulong newSize = sizeValues[idx];
                    nvidiaPresetService.SetReBarSizeLimit(card.GameName, installPathSafe, newSize);
                };
                rebarCol.Children.Add(rebarSizeCombo);
                sizeComboInit = false;
            }

            Grid.SetColumn(rebarCol, 2);
            nvidiaGrid.Children.Add(rebarCol);
            nvidiaGrid.Children.Add(MakeDlssDivider(3));

            _window.NvidiaProfilePanel.Children.Add(nvidiaGrid);
        }

        // Admin notice at the bottom of the Nvidia Profile section
        bool isElevated = VulkanLayerService.IsRunningAsAdmin();
        _window.NvidiaProfilePanel.Children.Add(new TextBlock
        {
            Text = isElevated
                ? Loc.GetString("Overrides.AdminNotice.Elevated")
                : Loc.GetString("Overrides.AdminNotice.NotElevated"),
            FontSize = 10,
            Foreground = UIFactory.Brush(isElevated ? ResourceKeys.TextTertiaryBrush : ResourceKeys.AccentAmberDimBrush),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        });

    }
}