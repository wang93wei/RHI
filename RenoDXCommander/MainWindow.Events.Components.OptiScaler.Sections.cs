// MainWindow.Events.Components.OptiScaler.Sections.cs — OptiScaler cog dialog sections shown for the
// Nightly, DLSS NR and F5 variants: Frame Generation, Additional Settings, Engine.ini (Unreal only)
// and Neural Rendering (DLSS NR / F5 only). Every setting applies as soon as it changes.
// Each section builds its cells first (OsComboCell), then lays them out two per row (AddOsGrid).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    // ── Frame Generation ─────────────────────────────────────────────────────────

    private void BuildOsFrameGenSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;
        var store = ctx.Store;

        cog.AddSection(Loc.GetString("Dialog.FrameGeneration"));

        // FG Enabled — master on/off
        var fgEnabledItems = new[] { "Auto (false)", "True" };
        var currentFgEnabled = ReadOsIniSectionValue(card.InstallPath, "FrameGen", "Enabled", "");
        var fgEnabledCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.FgEnabled"),
            Loc.GetString("Dialog.OptiScaler.FgEnabledTooltip"),
            fgEnabledItems, currentFgEnabled.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "FrameGen", "Enabled", fgEnabledItems[i] == "True" ? "true" : "false");
            });

        // Streamline/DLSS Enabler (combined) + Streamline version
        var dlssStreamlineSvc = App.Services.GetRequiredService<IDlssStreamlineService>();
        var slVersions = dlssStreamlineSvc.StreamlineVersions;
        var persistedSlVersion = ViewModel.GetOsStreamlineVersion(card.GameName, store);
        string slVersionDefault;
        if (!string.IsNullOrEmpty(persistedSlVersion) && slVersions.Contains(persistedSlVersion))
            slVersionDefault = persistedSlVersion;
        else if (slVersions.Contains("2.12.0"))
            slVersionDefault = "2.12.0";
        else
            slVersionDefault = slVersions.Count > 0 ? slVersions[0] : "2.12.0";
        var slVersionItems = slVersions.Count > 0 ? slVersions.ToList() : new List<string> { slVersionDefault };

        bool combinedOn = ViewModel.GetOsDeployStreamline(card.GameName, store);
        ComboBox? slVersionCombo = null;
        var yesNo = new[] { "No", "Yes" };
        var combinedCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.StreamlineDlssEnabler"),
            Loc.GetString("Dialog.OptiScaler.CombinedTooltip"),
            yesNo, combinedOn ? 1 : 0,
            i =>
            {
                bool on = yesNo[i] == "Yes";
                ViewModel.SetOsDeployStreamline(card.GameName, on, store);
                ViewModel.SetOsDeployDlssEnabler(card.GameName, on, store);
                if (slVersionCombo != null) slVersionCombo.IsEnabled = on;
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                try
                {
                    var osDir = Path.Combine(card.InstallPath, "OptiScaler");
                    if (on)
                    {
                        _optiScalerService.DeployStreamlineToGame(card.InstallPath);
                        _ = _dlssEnablerService.InstallAsync(osDir);
                        // Swap to the selected version immediately so the combo reflects reality
                        var selVer = slVersionCombo?.SelectedItem as string;
                        if (!string.IsNullOrEmpty(selVer))
                        {
                            var slDir = Path.Combine(card.InstallPath, "OptiScaler", "Streamline");
                            dlssStreamlineSvc.SwapStreamlineAsync(slDir, selVer).SafeFireAndForget("OsCog.SwapStreamline");
                        }
                    }
                    else
                    {
                        _optiScalerService.RemoveStreamlineFromGame(card.InstallPath);
                        _dlssEnablerService.Uninstall(osDir);
                    }
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[OsCog] Streamline/DLSS Enabler — {ex.Message}");
                    cog.SetError(Loc.GetString("Dialog.OptiScaler.UpdateEnablerFailed", ex.Message));
                }
            });

        var slVersionCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.StreamlineVersion"),
            Loc.GetString("Dialog.OptiScaler.StreamlineVersionTooltip"),
            slVersionItems, OsIndexOf(slVersionItems, slVersionDefault),
            i =>
            {
                var selectedVer = slVersionItems[i];
                ViewModel.SetOsStreamlineVersion(card.GameName, selectedVer, store);
                if (ViewModel.GetOsDeployStreamline(card.GameName, store) && !string.IsNullOrEmpty(card.InstallPath))
                {
                    var slFolder = Path.Combine(card.InstallPath, "OptiScaler", "Streamline");
                    dlssStreamlineSvc.SwapStreamlineAsync(slFolder, selectedVer).SafeFireAndForget("OsCog.SwapStreamline");
                }
            }, isEnabled: combinedOn);
        slVersionCombo = slVersionCell.Combo;

        // FG Input
        var fgInputCurrent = OsIniToFgInput(ViewModel.GetOsFgInput(card.GameName, store));
        // The current ini value can be outside the preset list — keep it visible in the dropdown
        var fgInputItems = OsWithValue(new[] { "Auto (Default)", "OptiFG (Upscaler)", "DLSSG via Streamline", "DLSSG via Nvngx", "FSR 3.1 FG", "FSR 3.0 FG", "XeFG" }, fgInputCurrent);
        var fgInputCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.FgInput"),
            Loc.GetString("Dialog.OptiScaler.FgInputTooltip"),
            fgInputItems, OsIndexOf(fgInputItems, fgInputCurrent),
            i =>
            {
                var v = OsFgInputToIni(fgInputItems[i]);
                ViewModel.SetOsFgInput(card.GameName, v, store);
                if (!string.IsNullOrEmpty(card.InstallPath)) OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "FrameGen", "FGInput", v);
            });

        // FG Output + FG Nvngx Override (only used with DLSSG output)
        ComboBox? fgNvngxCombo = null;
        void SetNvngxUsable(bool usable)
        {
            if (fgNvngxCombo == null) return;
            fgNvngxCombo.Opacity = usable ? 1.0 : CogDialog.DisabledOpacity;
            fgNvngxCombo.IsHitTestVisible = usable;
            fgNvngxCombo.IsEnabled = usable;
        }

        var fgOutputCurrent = OsIniToFgOutput(ViewModel.GetOsFgOutput(card.GameName, store));
        // The current ini value can be outside the preset list — keep it visible in the dropdown
        var fgOutputItems = OsWithValue(new[] { "Auto (Default)", "FSR FG", "DLSSG", "XeFG" }, fgOutputCurrent);
        var fgOutputCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.FgOutput"),
            Loc.GetString("Dialog.OptiScaler.FgOutputTooltip"),
            fgOutputItems, OsIndexOf(fgOutputItems, fgOutputCurrent),
            i =>
            {
                var sel = fgOutputItems[i];
                var v = OsFgOutputToIni(sel);
                ViewModel.SetOsFgOutput(card.GameName, v, store);
                if (!string.IsNullOrEmpty(card.InstallPath)) OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "FrameGen", "FGOutput", v);
                SetNvngxUsable(sel == "DLSSG");
            });

        var nvngxCurrent = OsIniToFgNvngx(ViewModel.GetOsFgNvngxReplacement(card.GameName, store));
        // The current ini value can be outside the preset list — keep it visible in the dropdown
        var nvngxItems = OsWithValue(new[] { "None (Real DLSSG)", "Nukem's", "Enabler", "FSR 3/4 FG", "Combo" }, nvngxCurrent);
        var fgNvngxCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.FgNvngxOverride"),
            Loc.GetString("Dialog.OptiScaler.FgNvngxTooltip"),
            nvngxItems, OsIndexOf(nvngxItems, nvngxCurrent, 0),
            i =>
            {
                var v = OsFgNvngxToIni(nvngxItems[i]);
                ViewModel.SetOsFgNvngxReplacement(card.GameName, v, store);
                if (!string.IsNullOrEmpty(card.InstallPath) && string.Equals(ViewModel.GetOsFgOutput(card.GameName, store), "dlssg", StringComparison.OrdinalIgnoreCase))
                    OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "FrameGen", "FGNvngxReplacement", v);
            });
        fgNvngxCombo = fgNvngxCell.Combo;
        ctx.FgOutputCombo = fgOutputCell.Combo;
        ctx.FgNvngxCombo = fgNvngxCombo;
        SetNvngxUsable(fgOutputCell.Combo.SelectedItem as string == "DLSSG");

        // HUD Fix — [OptiFG] HUDFix
        var hudFixCurrent = ReadOsIniSectionValue(card.InstallPath, "OptiFG", "HUDFix", "auto");
        var hudFixItems = new[] { "Default", "On", "Off" };
        var hudFixSelected = hudFixCurrent.Equals("true", StringComparison.OrdinalIgnoreCase) ? "On"
                           : hudFixCurrent.Equals("false", StringComparison.OrdinalIgnoreCase) ? "Off"
                           : "Default";
        var hudFixCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.HudFix"),
            Loc.GetString("Dialog.OptiScaler.HudFixTooltip"),
            hudFixItems, OsIndexOf(hudFixItems, hudFixSelected),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var sel = hudFixItems[i];
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "OptiFG", "HUDFix", sel == "On" ? "true" : sel == "Off" ? "false" : "auto");
            });

        // Force Reflex — [fakenvapi] ForceReflex
        var forceReflexItems = new[] { "Auto (0)", "Force Disable (1)", "Force Enable (2)" };
        var currentForceReflex = ReadOsIniSectionValue(card.InstallPath, "fakenvapi", "ForceReflex", "");
        var forceReflexCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.ForceReflex"),
            Loc.GetString("Dialog.OptiScaler.ForceReflexTooltip"),
            forceReflexItems, currentForceReflex switch { "1" => 1, "2" => 2, _ => 0 },
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "fakenvapi", "ForceReflex", i switch { 1 => "1", 2 => "2", _ => "0" });
            });

        // Use Games Reflex Markers — [DLSSG] UseGamesReflexMarkers
        var markersItems = new[] { "True", "False" };
        var currentReflexMarkers = ReadOsIniSectionValue(card.InstallPath, "DLSSG", "UseGamesReflexMarkers", "");
        var markersCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.UseGamesReflexMarkers"),
            Loc.GetString("Dialog.OptiScaler.UseGamesReflexMarkersTooltip"),
            markersItems, currentReflexMarkers.Equals("false", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSSG", "UseGamesReflexMarkers", markersItems[i] == "False" ? "false" : "true");
            });

        ctx.FgEnabledCombo = fgEnabledCell.Combo;
        ctx.CombinedCombo = combinedCell.Combo;
        ctx.FgInputCombo = fgInputCell.Combo;
        ctx.HudFixCombo = hudFixCell.Combo;
        ctx.ForceReflexCombo = forceReflexCell.Combo;
        ctx.ReflexMarkersCombo = markersCell.Combo;

        // FG Nvngx Override sits under FG Output, the setting it depends on.
        AddOsGrid(ctx,
            fgEnabledCell, hudFixCell,
            combinedCell, slVersionCell,
            fgInputCell, fgOutputCell,
            null, fgNvngxCell,
            forceReflexCell, markersCell);
    }

    // ── Additional Settings ──────────────────────────────────────────────────────

    private void BuildOsAdditionalSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;

        cog.AddSection(Loc.GetString("Dialog.AdditionalSettings"));

        // DLSS SR preset
        var srItems = OsSrPresetMap.Select(p => p.Label).ToArray();
        var srCurrent = ReadOsIniSectionValue(card.InstallPath, "DLSS", "RenderPresetForAll", "auto");
        var srCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.DlssSrPreset"),
            Loc.GetString("Dialog.OptiScaler.SrPresetTooltip"),
            srItems, Math.Max(0, Array.FindIndex(OsSrPresetMap, p => p.Value == srCurrent)),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var val = OsSrPresetMap[i].Value;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSS", "RenderPresetOverride", "true");
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSS", "RenderPresetForAll", val == "auto" ? "0" : val);
            });

        // DLSS RR preset
        var rrItems = OsRrPresetMap.Select(p => p.Label).ToArray();
        var rrCurrent = ReadOsIniSectionValue(card.InstallPath, "DLSSD", "RenderPresetForAll", "auto");
        var rrCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.DlssRrPreset"),
            Loc.GetString("Dialog.OptiScaler.RrPresetTooltip"),
            rrItems, Math.Max(0, Array.FindIndex(OsRrPresetMap, p => p.Value == rrCurrent)),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var val = OsRrPresetMap[i].Value;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSSD", "RenderPresetOverride", "true");
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSSD", "RenderPresetForAll", val == "auto" ? "0" : val);
            });

        // Render Scale — UpscaleRatioOverride
        var rsItems = OsRenderScaleMap.Select(p => p.Label).ToArray();
        var rsCurrentEnabled = ReadOsIniSectionValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideEnabled", "auto");
        var rsCurrentValue   = ReadOsIniSectionValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideValue", "auto");
        string rsSelected = "Off";
        if (rsCurrentEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)
            && float.TryParse(rsCurrentValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rsVal))
        {
            var match = OsRenderScaleMap.Skip(1).FirstOrDefault(p => Math.Abs(p.Ratio - rsVal) < 0.01f);
            if (match.Label != null) rsSelected = match.Label;
        }
        var rsCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.RenderScale"),
            Loc.GetString("Dialog.OptiScaler.RenderScaleTooltip"),
            rsItems, OsIndexOf(rsItems, rsSelected),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var entry = OsRenderScaleMap[i];
                if (entry.Label == "Off")
                {
                    OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideEnabled", "false");
                }
                else
                {
                    OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideEnabled", "true");
                    OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideValue", FormatOsFloat(entry.Ratio));
                }
            });

        // Disable Flip Metering — [NvApi] DisableFlipMetering
        var flipItems = new[] { "Default", "On" };
        var flipCurrent = ReadOsIniSectionValue(card.InstallPath, "NvApi", "DisableFlipMetering", "auto");
        var flipCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.DisableFlipMetering"),
            Loc.GetString("Dialog.OptiScaler.FlipTooltip"),
            flipItems, flipCurrent == "true" ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "NvApi", "DisableFlipMetering", flipItems[i] == "On" ? "true" : "false");
            });

        ctx.SrPresetCombo = srCell.Combo;
        ctx.RrPresetCombo = rrCell.Combo;
        ctx.RsCombo = rsCell.Combo;
        ctx.FlipCombo = flipCell.Combo;
        AddOsGrid(ctx,
            srCell, rrCell,
            rsCell, flipCell);
    }

    // ── Engine.ini (Unreal Engine only) ──────────────────────────────────────────

    private void BuildOsEngineIniSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;
        var store = ctx.Store;

        cog.AddSection("Engine.ini");

        void ApplyOrRemove(bool apply, (string, string, string)[] keys, string what)
        {
            if (string.IsNullOrEmpty(card.InstallPath)) return;
            try
            {
                if (apply) AuxInstallService.ApplyEngineIniCustomKeys(card.InstallPath, keys, card.EngineIniProjectOverride, card.GameName, card.Source);
                else AuxInstallService.RemoveEngineIniCustomKeys(card.InstallPath, keys.Select(k => k.Item2), card.EngineIniProjectOverride, card.GameName, card.Source);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MainWindow.OsCogButton_Click] Failed to update {what} Engine.ini keys — {ex.Message}");
                cog.SetError(Loc.GetString("Dialog.OptiScaler.UpdateEngineIniFailed", ex.Message));
            }
        }

        // Dilated Motion Vectors
        var dmvItems = new[] { "Default", "Off" };
        var dmvCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.DilatedMotionVectors"),
            Loc.GetString("Dialog.OptiScaler.DmvTooltip"),
            dmvItems, ViewModel.GetOsDilatedMotionVectorsOff(card.GameName, store) ? 1 : 0,
            i =>
            {
                bool off = dmvItems[i] == "Off";
                ViewModel.SetOsDilatedMotionVectorsOff(card.GameName, off, store);
                ApplyOrRemove(off, new (string, string, string)[]
                {
                    ("SystemSettings", "r.NGX.DLSS.DilateMotionVectors", "0"),
                    ("SystemSettings", "r.Streamline.DilateMotionVectors", "0"),
                }, "DilateMotionVectors");
            });

        // FSR Crash Fix
        var fsrItems = new[] { "None", "FSR2", "FSR3", "FSR3.1" };
        var fsrFixCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.FsrCrashFix"),
            Loc.GetString("Dialog.OptiScaler.FsrTooltip"),
            fsrItems, OsIndexOf(fsrItems, ViewModel.GetOsFsrCrashFix(card.GameName, store)),
            i =>
            {
                var sel = fsrItems[i];
                var allK = new[] { "r.FidelityFX.FSR2.UseNativeDX12", "r.FidelityFX.FSR3.UseNativeDX12", "r.FidelityFX.FSR3.UseRHI" };
                if (!string.IsNullOrEmpty(card.InstallPath))
                {
                    try { AuxInstallService.RemoveEngineIniCustomKeys(card.InstallPath, allK, card.EngineIniProjectOverride, card.GameName, card.Source); }
                    catch (Exception ex)
                    {
                        CrashReporter.Log($"[MainWindow.OsCogButton_Click] Failed to remove FSR crash-fix Engine.ini keys — {ex.Message}");
                        cog.SetError(Loc.GetString("Dialog.OptiScaler.UpdateEngineIniFailed", ex.Message));
                    }
                    if (sel != "None")
                    {
                        var k = sel switch
                        {
                            "FSR2" => new (string, string, string)[] { ("SystemSettings", "r.FidelityFX.FSR2.UseNativeDX12", "1") },
                            "FSR3" => new (string, string, string)[] { ("SystemSettings", "r.FidelityFX.FSR3.UseNativeDX12", "1") },
                            _      => new (string, string, string)[] { ("SystemSettings", "r.FidelityFX.FSR3.UseNativeDX12", "1"), ("SystemSettings", "r.FidelityFX.FSR3.UseRHI", "0") },
                        };
                        try { AuxInstallService.ApplyEngineIniCustomKeys(card.InstallPath, k, card.EngineIniProjectOverride, card.GameName, card.Source); }
                        catch (Exception ex)
                        {
                            CrashReporter.Log($"[MainWindow.OsCogButton_Click] Failed to apply FSR crash-fix Engine.ini keys — {ex.Message}");
                            cog.SetError(Loc.GetString("Dialog.OptiScaler.UpdateEngineIniFailed", ex.Message));
                        }
                    }
                }
                ViewModel.SetOsFsrCrashFix(card.GameName, sel == "None" ? null : sel, store);
            });

        // FSR-FG Swapchain
        var onItems = new[] { "Default", "On" };
        var swapchainCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.FsrFgSwapchain"),
            Loc.GetString("Dialog.OptiScaler.FgSwapTooltip"),
            onItems, ViewModel.GetOsFsrFgSwapchain(card.GameName, store) ? 1 : 0,
            i =>
            {
                bool on = onItems[i] == "On";
                ViewModel.SetOsFsrFgSwapchain(card.GameName, on, store);
                ApplyOrRemove(on, new (string, string, string)[] { ("SystemSettings", "r.FidelityFX.FI.OverrideSwapChainDX12", "1") }, "FSR-FG swapchain");
            });

        // Upscaler Plugin
        var pluginCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.UpscalerPlugin"),
            Loc.GetString("Dialog.OptiScaler.UpscalerPluginTooltip"),
            onItems, ViewModel.GetOsUpscalerPlugin(card.GameName, store) ? 1 : 0,
            i =>
            {
                bool on = onItems[i] == "On";
                ViewModel.SetOsUpscalerPlugin(card.GameName, on, store);
                ApplyOrRemove(on, new (string, string, string)[]
                {
                    ("SystemSettings", "r.AntiAliasingMethod", "4"),
                    ("SystemSettings", "r.TemporalAA.Upscaler", "1"),
                }, "upscaler plugin");
            });

        // The two FSR fixes share a row.
        AddOsGrid(ctx,
            pluginCell, dmvCell,
            fsrFixCell, swapchainCell);
    }

    // ── Neural Rendering (DLSS NR and F5 variants) ───────────────────────────────

    private void BuildOsNeuralRenderingSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;
        var store = ctx.Store;

        cog.AddSection(Loc.GetString("Dialog.NeuralRendering"));

        string ReadNrIni(string key) => ReadOsIniSectionValue(card.InstallPath, "DlssNr", key, "auto");
        var defaultOnOff = new[] { "Default", "On", "Off" };
        int DefaultOnOffIndex(string raw) =>
            raw.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1
            : raw.Equals("false", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        string DefaultOnOffToIni(int i) => i == 1 ? "true" : i == 2 ? "false" : "auto";

        // NR Runtime
        var dlssStreamlineSvc = App.Services.GetRequiredService<IDlssStreamlineService>();
        var nrVersions = dlssStreamlineSvc.DlssnrVersions.ToList();
        var currentNrRuntime = ViewModel.GetOsNrRuntime(card.GameName, store);
        // Default to newest (first manifest entry) when no override is set
        if (string.IsNullOrEmpty(currentNrRuntime) && nrVersions.Count > 0)
            currentNrRuntime = nrVersions[0];
        if (!string.IsNullOrEmpty(currentNrRuntime) && !nrVersions.Contains(currentNrRuntime))
            nrVersions.Insert(0, currentNrRuntime);
        var nrRuntimeCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.NrRuntime"),
            Loc.GetString("Dialog.OptiScaler.NrRuntimeTooltip"),
            nrVersions, OsIndexOf(nrVersions, currentNrRuntime),
            i =>
            {
                var selectedVer = nrVersions[i];
                if (string.IsNullOrEmpty(selectedVer)) return;
                ViewModel.SetOsNrRuntime(card.GameName, selectedVer, store);
                // Immediately swap the nvngx_dlssnr.dll in the game folder if installed
                if (card.IsOsInstalled && !string.IsNullOrEmpty(card.InstallPath))
                {
                    var gameNrPath = Path.Combine(card.InstallPath, "nvngx_dlssnr.dll");
                    var installPath = card.InstallPath;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var cachedDll = await dlssStreamlineSvc.EnsureSpecificDlssnrCachedAsync(selectedVer).ConfigureAwait(false);
                            if (cachedDll != null)
                            {
                                // Use SentinelBackup to preserve the existing sentinel state,
                                // then overwrite with the new version
                                AuxInstallService.SentinelBackup(gameNrPath);
                                File.Copy(cachedDll, gameNrPath, overwrite: true);
                                CrashReporter.Log($"[OsCog] Swapped nvngx_dlssnr.dll to {selectedVer} in '{installPath}'");
                            }
                            else
                            {
                                CrashReporter.Log($"[OsCog] nvngx_dlssnr.dll {selectedVer} not available — swap skipped");
                            }
                        }
                        catch (Exception ex) { CrashReporter.Log($"[OsCog] NR runtime swap failed — {ex.Message}"); }
                    });
                }
            });

        // NR Enabled
        var nrEnabledCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.NrEnabled"),
            Loc.GetString("Dialog.OptiScaler.NrEnabledTooltip"),
            defaultOnOff, DefaultOnOffIndex(ReadNrIni("Enabled")),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "Enabled", DefaultOnOffToIni(i));
            });

        // Run Before SR
        var nrRunBeforeSrCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.NrRunBeforeSR"),
            Loc.GetString("Dialog.OptiScaler.NrRunBeforeSrTooltip"),
            defaultOnOff, DefaultOnOffIndex(ReadNrIni("RunBeforeSR")),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "RunBeforeSR", DefaultOnOffToIni(i));
            });

        // Passes
        var passesItems = new[] { "Default", "1", "2", "3" };
        var nrPassesRaw = ReadNrIni("Passes");
        var nrPassesCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.NrPasses"),
            Loc.GetString("Dialog.OptiScaler.NrPassesTooltip"),
            passesItems, OsIndexOf(passesItems, nrPassesRaw is "1" or "2" or "3" ? nrPassesRaw : "Default"),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var sel = passesItems[i];
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "Passes", sel == "Default" ? "auto" : sel);
            });

        // Working Scale
        var scaleItems = OsNrScaleMap.Select(p => p.Label).ToArray();
        var nrScaleRaw = ReadNrIni("WorkingScale");
        var nrScaleCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.NrWorkingScale"),
            Loc.GetString("Dialog.OptiScaler.NrWorkingScaleTooltip"),
            scaleItems, Math.Max(0, Array.FindIndex(OsNrScaleMap, p => p.Value == nrScaleRaw)),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "WorkingScale", OsNrScaleMap[i].Value);
            });

        // Apply to Finished Picture
        var finishedItems = new[] { "Default", "On" };
        var nrFinishedCell = OsComboCell(cog, Loc.GetString("Dialog.OptiScaler.NrApplyToFinishedPicture"),
            Loc.GetString("Dialog.OptiScaler.NrFinishedPictureTooltip"),
            finishedItems, ReadNrIni("FinishedPicture").Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "FinishedPicture", finishedItems[i] == "On" ? "true" : "false");
            });

        ctx.NrRuntimeCombo = nrRuntimeCell.Combo;
        ctx.NrEnabledCombo = nrEnabledCell.Combo;
        ctx.NrRunBeforeSrCombo = nrRunBeforeSrCell.Combo;
        ctx.NrPassesCombo = nrPassesCell.Combo;
        ctx.NrWorkingScaleCombo = nrScaleCell.Combo;
        ctx.NrFinishedPicCombo = nrFinishedCell.Combo;
        AddOsGrid(ctx,
            nrEnabledCell, nrRuntimeCell,
            nrRunBeforeSrCell, nrPassesCell,
            nrScaleCell, nrFinishedCell);
    }
}
