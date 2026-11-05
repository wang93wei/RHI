// DetailPanelBuilder.Extras.RtxEncoreSettings.cs -- RTX Encore ⚙: settings worth choosing before launch
// (frame generation, XeFG, HUD/UI, upscaling, Smooth Motion, Neural Rendering core options, compatibility),
// the Neural Rendering DLL and the Intel XeSS files status. Settings are edited in place in
// rtx-encore.jsonc (RtxEncoreConfig), beside the mod. The overlay, NR styles/per-pass tuning, shortcuts
// and diagnostics stay in RTX Encore's in-game menu (Insert).

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private static string[] RtxOffOn => new[] { Loc.GetString("Extras.RtxEncore.Opt.Off"), Loc.GetString("Extras.RtxEncore.Opt.On") };
    private static readonly string[] RtxOffOnValues = { "false", "true" };

    /// <summary>⚙ dialog for RTX Encore. Every change is saved as soon as it's made.</summary>
    private async Task ShowRtxEncoreSettingsAsync(GameCardViewModel card, RtxEncoreService svc,
        string installPath, bool isInstalled, bool nrDeployed)
    {
        var gameName = card.GameName;
        var store    = card.Source ?? "";
        var installedAs = _window.ViewModel.GetRtxEncoreInstalledAs(gameName, store);
        string? modDir  = isInstalled && !string.IsNullOrEmpty(installedAs) ? svc.ModDir(installPath, installedAs) : null;
        string? cfgPath = modDir != null ? Path.Combine(modDir, RtxEncoreConfig.FileName) : null;
        bool canEdit = cfgPath != null;

        var cog = new CogDialog(Loc.GetString("Extras.RtxEncore.Settings.Title"), _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Extras.RtxEncore.Settings.Intro"));
        if (!canEdit)
            cog.AddNote(Loc.GetString("Extras.RtxEncore.Settings.NotInstalled"));
        else if (!File.Exists(cfgPath))
            cog.AddNote(Loc.GetString("Extras.RtxEncore.Settings.NoConfig"));
        if (card.IsRunning)
            cog.AddWarning(Loc.GetString("Extras.RtxEncore.Settings.GameRunning"));

        Button? openCfgBtn = null;

        // One dropdown bound to one setting in rtx-encore.jsonc. values are the file's values ("true", "4", "xefg").
        CogDialog.CompactCell Choice(string label, string help, string section, string key,
            string[] displays, string[] values, string defaultValue, Action<int>? after = null)
        {
            var current = canEdit ? RtxEncoreConfig.Get(cfgPath!, section, key) : null;
            int idx = Array.FindIndex(values, v => string.Equals(v, current ?? defaultValue, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = Array.FindIndex(values, v => string.Equals(v, defaultValue, StringComparison.OrdinalIgnoreCase));
            return cog.CompactCombo(label, help, displays, idx, i =>
            {
                if (!canEdit) return false;
                var v = values[i];
                bool raw = v is "true" or "false" || int.TryParse(v, out _);
                RtxEncoreConfig.Set(cfgPath!, section, key, raw ? v : "\"" + v + "\"");
                CrashReporter.Log($"[RtxEncoreCog] '{gameName}': {section}.{key} = {v}");
                if (openCfgBtn != null) openCfgBtn.IsEnabled = true;
                cog.SetSuccess(Loc.GetString("Extras.RtxEncore.Settings.Saved"));
                after?.Invoke(i);
                return true;
            }, isEnabled: canEdit);
        }

        void MarkSaved() { if (openCfgBtn != null) openCfgBtn.IsEnabled = true; }

        // A number box bound to one setting in rtx-encore.jsonc
        CogDialog.CompactCell Number(string label, string help, string section, string key, int min, int max, int defaultValue) =>
            new(label, help, BuildRtxEncoreNumberBox(cog, cfgPath, gameName, section, key, label, min, max, defaultValue, MarkSaved));

        // ── Frame generation ────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionFg"));
        var xefgControls = new List<Control>();
        void SetXefgEnabled(bool on) { foreach (var c in xefgControls) c.IsEnabled = canEdit && on; }
        ComboBox? multiplierCombo = null;
        var dynamicControls = new List<Control>();

        var engineCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.Engine.Label"), Loc.GetString("Extras.RtxEncore.Settings.Engine.Help"),
            "renderingEngines", "frameGenerationEngine",
            new[] { Loc.GetString("Extras.RtxEncore.Opt.Game"), "DLSS-FG", "XeFG" }, new[] { "game", "dlss-fg", "xefg" }, "game",
            i => SetXefgEnabled(i == 2));
        var modeCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.Mode.Label"), Loc.GetString("Extras.RtxEncore.Settings.Mode.Help"),
            "frameGeneration", "mode",
            new[] { Loc.GetString("Extras.RtxEncore.Opt.GameDecides"), Loc.GetString("Extras.RtxEncore.Opt.Fixed"), Loc.GetString("Extras.RtxEncore.Opt.Dynamic") }, new[] { "game", "fixed", "dynamic" }, "game",
            i =>
            {
                if (multiplierCombo != null) multiplierCombo.IsEnabled = canEdit && i == 1;
                foreach (var c in dynamicControls) c.IsEnabled = canEdit && i == 2;
            });
        var multiplierCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.Multiplier.Label"), Loc.GetString("Extras.RtxEncore.Settings.Multiplier.Help"),
            "frameGeneration", "multiplier",
            new[] { "2x", "3x", "4x", "5x", "6x" }, new[] { "2", "3", "4", "5", "6" }, "4");
        multiplierCombo = (ComboBox)multiplierCell.Control;
        multiplierCombo.IsEnabled = canEdit && ((ComboBox)modeCell.Control).SelectedIndex == 1;
        var presetCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.DLSSFGpreset.Label"), Loc.GetString("Extras.RtxEncore.Settings.DLSSFGpreset.Help"),
            "dlssPresets", "dlssFgPreset",
            new[] { Loc.GetString("Extras.RtxEncore.Opt.Game"), "A", "B", "C" }, new[] { "game", "a", "b", "c" }, "game");
        var flipCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.Flipmeteringbypass.Label"), Loc.GetString("Extras.RtxEncore.Settings.Flipmeteringbypass.Help"),
            "frameGeneration", "flipMeteringBypass", RtxOffOn, RtxOffOnValues, "false");
        var fpsCell = Number(Loc.GetString("Extras.RtxEncore.Settings.FramelimitFPS.Label"), Loc.GetString("Extras.RtxEncore.Settings.FramelimitFPS.Help"),
            "nativeMenuFeatures", "reflexFrameLimit", 0, 1000, 0);
        var targetCell = Number(Loc.GetString("Extras.RtxEncore.Settings.DynamictargetFPS.Label"), Loc.GetString("Extras.RtxEncore.Settings.DynamictargetFPS.Help"),
            "frameGeneration", "dynamicTargetFrameRate", 0, 1000, 0);
        var exp56Cell = Choice(Loc.GetString("Extras.RtxEncore.Settings.Dynamicupto6x.Label"), Loc.GetString("Extras.RtxEncore.Settings.Dynamicupto6x.Help"),
            "frameGeneration", "dynamicExperimental56", RtxOffOn, RtxOffOnValues, "false");
        dynamicControls.AddRange(new[] { targetCell.Control, exp56Cell.Control }.OfType<Control>());
        bool isDynamic = ((ComboBox)modeCell.Control).SelectedIndex == 2;
        foreach (var c in dynamicControls) c.IsEnabled = canEdit && isDynamic;
        cog.AddCompactGrid(engineCell, modeCell, multiplierCell, presetCell, targetCell, exp56Cell, flipCell, fpsCell);

        // ── XeFG ────────────────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionXefgEngine"));
        var tensorCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.NVIDIATensorCoresexperimental.Label"), Loc.GetString("Extras.RtxEncore.Settings.NVIDIATensorCoresexperimental.Help"),
            "xefg", "xefgTensorCores", RtxOffOn, RtxOffOnValues, "false");
        var pacingCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.Variablepacingabove2x.Label"), Loc.GetString("Extras.RtxEncore.Settings.Variablepacingabove2x.Help"),
            "xefg", "xefgMfg4VariablePacing", RtxOffOn, RtxOffOnValues, "true");
        var uiCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.HUDUIhandling.Label"), Loc.GetString("Extras.RtxEncore.Settings.HUDUIhandling.Help"),
            "xefg", "xefgUiHandling", new[] { Loc.GetString("Extras.RtxEncore.Opt.Interpolate"), Loc.GetString("Extras.RtxEncore.Opt.GameLayers") }, new[] { "interpolate", "game" }, "interpolate");
        var alphaCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.UIalpha.Label"), Loc.GetString("Extras.RtxEncore.Settings.UIalpha.Help"),
            "xefg", "xefgUiAlpha", new[] { Loc.GetString("Extras.RtxEncore.Opt.Unknown"), Loc.GetString("Extras.RtxEncore.Opt.Straight"), Loc.GetString("Extras.RtxEncore.Opt.Premultiplied") }, new[] { "unknown", "straight", "premultiplied" }, "unknown");
        xefgControls.AddRange(new[] { tensorCell.Control, pacingCell.Control, uiCell.Control, alphaCell.Control }.OfType<Control>());
        SetXefgEnabled(((ComboBox)engineCell.Control).SelectedIndex == 2);
        cog.AddCompactGrid(tensorCell, pacingCell, uiCell, alphaCell);
        cog.AddNote(Loc.GetString("Extras.RtxEncore.Settings.XefgOnlyNote"));

        // ── Image quality ───────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionQuality"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.RtxEncore.Settings.Highmultiplierquality.Label"), Loc.GetString("Extras.RtxEncore.Settings.Highmultiplierquality.Help"),
                "imageQuality", "highMultiplierQuality", RtxOffOn, RtxOffOnValues, "true"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.Frameprotection.Label"), Loc.GetString("Extras.RtxEncore.Settings.Frameprotection.Help"),
                "imageQuality", "frameProtection", RtxOffOn, RtxOffOnValues, "true"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.Fastframegeneration.Label"), Loc.GetString("Extras.RtxEncore.Settings.Fastframegeneration.Help"),
                "imageQuality", "fastFrameGeneration", RtxOffOn, RtxOffOnValues, "true"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.Protectiontuning.Label"), Loc.GetString("Extras.RtxEncore.Settings.Protectiontuning.Help"),
                "imageQuality", "protectionTuning", new[] { Loc.GetString("Extras.RtxEncore.Opt.Refined"), Loc.GetString("Extras.RtxEncore.Opt.Classic") }, new[] { "refined", "classic" }, "refined"));

        // ── HUD and UI ──────────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionHudUi"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.RtxEncore.Settings.AutoUIrecomposition.Label"), Loc.GetString("Extras.RtxEncore.Settings.AutoUIrecomposition.Help"),
                "hudUi", "autoUiRecomposition", RtxOffOn, RtxOffOnValues, "true"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.UIassist.Label"), Loc.GetString("Extras.RtxEncore.Settings.UIassist.Help"),
                "hudUi", "uiAssist", RtxOffOn, RtxOffOnValues, "true"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.ForceUIrecomposition.Label"), Loc.GetString("Extras.RtxEncore.Settings.ForceUIrecomposition.Help"),
                "hudUi", "forceUiRecomposition", RtxOffOn, RtxOffOnValues, "false"));

        // ── Upscaling ───────────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionUpscaling"));
        var customScaleCell = Number(Loc.GetString("Extras.RtxEncore.Settings.Customrenderscale.Label"), Loc.GetString("Extras.RtxEncore.Settings.Customrenderscale.Help"),
            "dlssSuperResolution", "dlssCustomScale", 50, 100, 67);
        var renderScaleCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.DLSSrenderscale.Label"), Loc.GetString("Extras.RtxEncore.Settings.DLSSrenderscale.Help"),
            "dlssSuperResolution", "dlssRenderScale",
            new[] { Loc.GetString("Extras.RtxEncore.Opt.Game"), "DLAA", Loc.GetString("Extras.RtxEncore.Opt.Quality"), Loc.GetString("Extras.RtxEncore.Opt.Balanced"), Loc.GetString("Extras.RtxEncore.Opt.Performance"), Loc.GetString("Extras.RtxEncore.Opt.UltraPerformance"), Loc.GetString("Extras.RtxEncore.Opt.Custom") },
            new[] { "game", "dlaa", "quality", "balanced", "performance", "ultra-performance", "custom" }, "game",
            i => ((Control)customScaleCell.Control).IsEnabled = canEdit && i == 6);
        ((Control)customScaleCell.Control).IsEnabled = canEdit && ((ComboBox)renderScaleCell.Control).SelectedIndex == 6;
        var xessTensorCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.XeSSonTensorCoresexperimental.Label"),
            Loc.GetString("Extras.RtxEncore.Settings.XeSSonTensorCoresexperimental.Help"),
            "xess", "xessTensorCores", RtxOffOn, RtxOffOnValues, "false");
        var upscalerCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.Upscaler.Label"),
            Loc.GetString("Extras.RtxEncore.Settings.Upscaler.Help"),
            "renderingEngines", "reconstructionEngine",
            new[] { Loc.GetString("Extras.RtxEncore.Opt.Game"), "XeSS" }, new[] { "game", "xess" }, "game",
            i => ((Control)xessTensorCell.Control).IsEnabled = canEdit && i == 1);
        ((Control)xessTensorCell.Control).IsEnabled = canEdit && ((ComboBox)upscalerCell.Control).SelectedIndex == 1;
        cog.AddCompactGrid(
            upscalerCell,
            xessTensorCell,
            renderScaleCell,
            customScaleCell,
            Choice(Loc.GetString("Extras.RtxEncore.Settings.FastSuperResolution.Label"), Loc.GetString("Extras.RtxEncore.Settings.FastSuperResolution.Help"),
                "dlssSuperResolution", "fastSuperResolution", RtxOffOn, RtxOffOnValues, "false"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.DLSSSRpreset.Label"), Loc.GetString("Extras.RtxEncore.Settings.DLSSSRpreset.Help"),
                "dlssPresets", "dlssSrPreset",
                new[] { Loc.GetString("Extras.RtxEncore.Opt.Game"), Loc.GetString("Extras.RtxEncore.Opt.Default"), "A", "B", "C", "D", "E", "F", "J", "K", "L", "M" },
                new[] { "game", "default", "a", "b", "c", "d", "e", "f", "j", "k", "l", "m" }, "game"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.DLSSRRpreset.Label"), Loc.GetString("Extras.RtxEncore.Settings.DLSSRRpreset.Help"),
                "dlssPresets", "dlssRrPreset",
                new[] { Loc.GetString("Extras.RtxEncore.Opt.Game"), Loc.GetString("Extras.RtxEncore.Opt.Default"), "A", "B", "C", "D", "E", "F" },
                new[] { "game", "default", "a", "b", "c", "d", "e", "f" }, "game"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.SRHDRinput.Label"), Loc.GetString("Extras.RtxEncore.Settings.SRHDRinput.Help"),
                "superResolutionInputs", "srHdr", new[] { Loc.GetString("Extras.RtxEncore.Opt.Game"), Loc.GetString("Extras.RtxEncore.Opt.On"), Loc.GetString("Extras.RtxEncore.Opt.Off") }, new[] { "game", "on", "off" }, "game"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.SRautoexposure.Label"), Loc.GetString("Extras.RtxEncore.Settings.SRautoexposure.Help"),
                "superResolutionInputs", "srAutoExposure", new[] { Loc.GetString("Extras.RtxEncore.Opt.Game"), Loc.GetString("Extras.RtxEncore.Opt.On"), Loc.GetString("Extras.RtxEncore.Opt.Off") }, new[] { "game", "on", "off" }, "game"));

        // ── Smooth Motion ───────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionSmoothMotion"));
        var gameApiValue = card.GraphicsApi switch
        {
            GraphicsApiType.DirectX11 => "d3d11",
            GraphicsApiType.Vulkan    => "vulkan",
            _                         => "d3d12",
        };
        var apiDisplays = new[] { "DirectX 12", "DirectX 11", "Vulkan" };
        var apiValues   = new[] { "d3d12", "d3d11", "vulkan" };
        var smCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.SmoothMotion.Label"), Loc.GetString("Extras.RtxEncore.Settings.SmoothMotion.Help"),
            "smoothMotion", "smoothMotionEnabled", RtxOffOn, RtxOffOnValues, "false",
            i =>
            {
                // Turning it on in a file without an API yet: use the game's own API, not RTX Encore's DirectX 12 default
                if (i == 1 && canEdit && RtxEncoreConfig.Get(cfgPath!, "smoothMotion", "smoothMotionApi") == null)
                    RtxEncoreConfig.SetString(cfgPath!, "smoothMotion", "smoothMotionApi", gameApiValue);
            });
        var smApiCell = Choice(Loc.GetString("Extras.RtxEncore.Settings.GraphicsAPI.Label"), Loc.GetString("Extras.RtxEncore.Settings.GraphicsAPI.Help"),
            "smoothMotion", "smoothMotionApi", apiDisplays, apiValues, gameApiValue);
        cog.AddCompactGrid(smCell, smApiCell);

        // ── Neural Rendering DLL ────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionNeuralRendering"));
        AddRtxEncoreNrSetting(cog, card, svc, installPath, isInstalled, nrDeployed);
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.RtxEncore.Settings.NeuralRendering.Label"), Loc.GetString("Extras.RtxEncore.Settings.NeuralRendering.Help"),
                "neuralRendering/core", "nrEnabled", RtxOffOn, RtxOffOnValues, "false"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.NRmodel.Label"), Loc.GetString("Extras.RtxEncore.Settings.NRmodel.Help"),
                "dlssPresets", "dlssNrPreset", new[] { "B", "A", Loc.GetString("Extras.RtxEncore.Opt.Auto") }, new[] { "b", "a", "auto" }, "b"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.NRprecision.Label"), Loc.GetString("Extras.RtxEncore.Settings.NRprecision.Help"),
                "neuralRendering/optimizations", "nrPrecision", new[] { Loc.GetString("Extras.RtxEncore.Opt.Exact"), Loc.GetString("Extras.RtxEncore.Opt.Fast") }, new[] { "exact", "fast" }, "exact"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.NRfastprocessing.Label"), Loc.GetString("Extras.RtxEncore.Settings.NRfastprocessing.Help"),
                "neuralRendering/optimizations", "nrFastProcessing", RtxOffOn, RtxOffOnValues, "true"));

        // ── Compatibility ───────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionCompatibility"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.RtxEncore.Settings.GPUseries.Label"), Loc.GetString("Extras.RtxEncore.Settings.GPUseries.Help"),
                "general", "gpuSeries", new[] { Loc.GetString("Extras.RtxEncore.Opt.Auto"), "RTX 40", "RTX 30", "RTX 20" }, new[] { "auto", "rtx40", "rtx30", "rtx20" }, "auto"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.NVIDIAAppFGmodels.Label"), Loc.GetString("Extras.RtxEncore.Settings.NVIDIAAppFGmodels.Help"),
                "compatibility", "forceOTA", RtxOffOn, RtxOffOnValues, "false"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.Disableingamemenu.Label"), Loc.GetString("Extras.RtxEncore.Settings.Disableingamemenu.Help"),
                "compatibility", "disableInGameMenu", RtxOffOn, RtxOffOnValues, "false"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.FGinmenus.Label"), Loc.GetString("Extras.RtxEncore.Settings.FGinmenus.Help"),
                "compatibility", "disableMenuDetection", RtxOffOn, RtxOffOnValues, "false"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.RaytracedHairWorks.Label"), Loc.GetString("Extras.RtxEncore.Settings.RaytracedHairWorks.Help"),
                "nativeMenuFeatures", "hairEnabled", RtxOffOn, RtxOffOnValues, "true"));

        // ── Intel XeSS files ────────────────────────────────────────────────────
        if (modDir != null)
        {
            var (intelComplete, inGameFolders) = Rtx40MfgService.GetEncoreIntelState(modDir);
            if (intelComplete)
                cog.AddNote(inGameFolders > 0
                    ? Loc.GetString("Extras.RtxEncore.Settings.XessInstalledInGame", inGameFolders)
                    : Loc.GetString("Extras.RtxEncore.Settings.XessInstalled"));
            else
                cog.AddWarning(Loc.GetString("Extras.RtxEncore.Settings.XessMissing"));
        }

        openCfgBtn = cog.MakeOpenFileButton(Loc.GetString("Extras.RtxEncore.Settings.OpenConfig"), cfgPath);
        cog.AddButtons(openCfgBtn,
            cog.MakeButton(Loc.GetString("Extras.RtxEncore.Settings.OpenGithub"), CogButtonKind.Secondary,
                async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(RtxEncoreService.RepoUrl)); }));

        await cog.ShowAsync();
    }

    /// <summary>Whole-number box for one rtx-encore.jsonc setting: saves on Enter and when focus leaves.</summary>
    private static TextBox BuildRtxEncoreNumberBox(CogDialog cog, string? cfgPath, string gameName,
        string section, string key, string label, int min, int max, int defaultValue, Action onSaved)
    {
        var fallback = defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var current = cfgPath != null ? RtxEncoreConfig.Get(cfgPath, section, key) : null;
        var box = CogDialog.MakeTextBox(current ?? fallback, fallback);
        box.IsEnabled = cfgPath != null;
        string saved = box.Text;
        void Save()
        {
            if (cfgPath == null) return;
            var text = box.Text.Trim();
            if (text == saved) return;
            if (!int.TryParse(text, out var value) || value < min || value > max)
            {
                cog.SetError(Loc.GetString("Extras.RtxEncore.Settings.NumberRange", label, min, max));
                box.Text = saved;
                return;
            }
            try
            {
                RtxEncoreConfig.SetInt(cfgPath, section, key, value);
                saved = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                box.Text = saved;
                CrashReporter.Log($"[RtxEncoreCog] '{gameName}': {section}.{key} = {value}");
                onSaved();
                cog.SetSuccess(Loc.GetString("Extras.RtxEncore.Settings.Saved"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                CrashReporter.Log($"[RtxEncoreCog] {section}.{key} save failed — {ex.Message}");
                cog.SetError(Loc.GetString("Extras.RtxEncore.Settings.SaveFailed", label, ex.Message));
                box.Text = saved;
            }
        }
        box.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { Save(); e.Handled = true; } };
        box.LostFocus += (_, _) => Save();
        return box;
    }

    /// <summary>The "Deploy NR DLL" setting: copies nvngx_dlssnr.dll 310.9.16 beside RTX Encore, or removes it.</summary>
    private void AddRtxEncoreNrSetting(CogDialog cog, GameCardViewModel card, RtxEncoreService svc,
        string installPath, bool isInstalled, bool nrDeployed)
    {
        var gameName = card.GameName;
        var store    = card.Source ?? "";
        bool nrOn = nrDeployed;
        bool suppress = false;
        ComboBox? nrCombo = null;

        void Revert()
        {
            if (nrCombo == null) return;
            suppress = true;
            nrCombo.SelectedIndex = nrOn ? 1 : 0;
            suppress = false;
        }

        async Task ApplyAsync(bool enable)
        {
            if (enable == nrOn) return;
            _window.ViewModel.SetRtxEncoreNrDllDeployed(gameName, enable, store);
            if (!isInstalled)
            {
                nrOn = enable;
                cog.SetSuccess(enable ? Loc.GetString("Extras.RtxEncore.Nr.SavedWillDeploy") : Loc.GetString("Extras.RtxEncore.Nr.Saved"));
                return;
            }
            if (enable)
            {
                if (nrCombo != null) nrCombo.IsEnabled = false;
                try
                {
                    if (svc.GetNrDllCachedPath() == null)
                        cog.SetStatus(Loc.GetString("Extras.RtxEncore.Nr.Downloading", RtxEncoreService.NrRequiredVer));
                    var cached = await svc.EnsureNrDllCachedAsync(App.Services.GetRequiredService<IDlssStreamlineService>());
                    if (cached == null)
                    {
                        CrashReporter.Log($"[RtxEncoreCog] NR {RtxEncoreService.NrRequiredVer} not available — not deployed");
                        _window.ViewModel.SetRtxEncoreNrDllDeployed(gameName, false, store);
                        Revert();
                        cog.SetError(Loc.GetString("Extras.RtxEncore.Nr.DownloadFailed", RtxEncoreService.NrRequiredVer));
                        return;
                    }
                    var installedAs = _window.ViewModel.GetRtxEncoreInstalledAs(gameName, store);
                    if (await Task.Run(() => svc.DeployNrDll(installPath, installedAs)))
                    {
                        nrOn = true;
                        cog.SetSuccess(Loc.GetString("Extras.RtxEncore.Nr.Deployed", RtxEncoreService.NrRequiredVer));
                    }
                    else
                    {
                        _window.ViewModel.SetRtxEncoreNrDllDeployed(gameName, false, store);
                        Revert();
                        cog.SetError(Loc.GetString("Extras.RtxEncore.Nr.CopyFailed"));
                    }
                    RequestExtrasRebuild(card);
                }
                finally
                {
                    if (nrCombo != null) nrCombo.IsEnabled = true;
                }
            }
            else
            {
                svc.RemoveNrDll(installPath, _window.ViewModel.GetRtxEncoreInstalledAs(gameName, store));
                nrOn = false;
                cog.SetSuccess(Loc.GetString("Extras.RtxEncore.Nr.Removed"));
                RequestExtrasRebuild(card);
            }
        }

        nrCombo = cog.AddCombo(Loc.GetString("Extras.RtxEncore.Nr.Deploy"),
            Loc.GetString("Extras.RtxEncore.Nr.Deploy.Help", RtxEncoreService.NrRequiredVer),
            new[] { Loc.GetString("Extras.RtxEncore.Opt.No"), Loc.GetString("Extras.RtxEncore.Opt.Yes") }, nrOn ? 1 : 0, i =>
            {
                if (suppress) return true;
                _ = ApplySafeAsync(i == 1);
                return true;
            });

        async Task ApplySafeAsync(bool enable)
        {
            try { await ApplyAsync(enable); }
            catch (Exception ex)
            {
                CrashReporter.Log($"[RtxEncoreCog] NR DLL change failed — {ex.Message}");
                cog.SetError(Loc.GetString("Extras.RtxEncore.Nr.ChangeFailed", ex.Message));
            }
        }
    }
}
