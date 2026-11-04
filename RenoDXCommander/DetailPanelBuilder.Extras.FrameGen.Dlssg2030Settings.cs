// DetailPanelBuilder.Extras.FrameGen.Dlssg2030Settings.cs — ⚙ dialog for 20/30 FG Unlock.
// Edits the game's dlssg_sm86.ini. The mod treats a mistyped value as "turn frame generation
// off", so every setting is a dropdown of valid values only.

using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private async Task ShowDlssg2030SettingsAsync(string installPath, bool isInstalled)
    {
        var iniPath = string.IsNullOrEmpty(installPath) ? "" : Path.Combine(installPath, Dlssg20_30Service.IniFileName);
        var logPath = string.IsNullOrEmpty(installPath) ? "" : Path.Combine(installPath, Dlssg20_30Service.LogRelPath);
        bool hasIni = isInstalled && File.Exists(iniPath);

        var cog = new CogDialog(Loc.GetString("Extras.Dlssg2030.Settings.Title"), _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Extras.Dlssg2030.Settings.Intro"));
        if (!hasIni)
            cog.AddWarning(Loc.GetString("Extras.Dlssg2030.Settings.NotInstalled"));

        // One labelled dropdown bound to [section] key. Options are (label, ini value).
        ComboBox AddSetting(string label, string help, string section, string key, string fallback,
            (string Label, string Value)[] options, Func<string, bool>? beforeWrite = null)
        {
            var current = hasIni ? Dlssg20_30Service.ReadIniValue(iniPath, section, key) : null;
            var idx = Array.FindIndex(options, o => o.Value.Equals(current ?? fallback, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = Array.FindIndex(options, o => o.Value == fallback);
            return cog.AddCombo(label, help, options.Select(o => o.Label).ToList(), idx, i =>
            {
                var value = options[i].Value;
                if (beforeWrite != null && !beforeWrite(value)) return false;
                try
                {
                    Dlssg20_30Service.WriteIniValue(iniPath, section, key, value);
                    cog.SetSuccess(Loc.GetString("Extras.Dlssg2030.Settings.Saved", label));
                    CrashReporter.Log($"[Dlssg2030Settings] {section}.{key}={value} in '{installPath}'");
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    cog.SetError(Loc.GetString("Extras.Dlssg2030.Settings.SaveFailed", label, ex.Message));
                    CrashReporter.Log($"[Dlssg2030Settings] Write failed — {ex.Message}");
                    return false;
                }
            }, isEnabled: hasIni);
        }

        AddSetting(Loc.GetString("Extras.Dlssg2030.Settings.FrameGeneration.Label"),
            Loc.GetString("Extras.Dlssg2030.Settings.FrameGeneration.Help"),
            "General", "Enabled", "1",
            new[] { (Loc.GetString("Extras.Dlssg2030.Settings.OptOn"), "1"), (Loc.GetString("Extras.Dlssg2030.Settings.OptOff"), "0") });

        ComboBox? framesCombo = null;
        bool engineIsXess = hasIni && "XeSS".Equals(Dlssg20_30Service.ReadIniValue(iniPath, "FrameGeneration", "Engine"), StringComparison.OrdinalIgnoreCase);
        AddSetting(Loc.GetString("Extras.Dlssg2030.Settings.Engine.Label"),
            Loc.GetString("Extras.Dlssg2030.Settings.Engine.Help"),
            "FrameGeneration", "Engine", "DLSS",
            new[] { (Loc.GetString("Extras.Dlssg2030.Settings.Engine.OptDlss"), "DLSS"), (Loc.GetString("Extras.Dlssg2030.Settings.Engine.OptXess"), "XeSS") },
            beforeWrite: value =>
            {
                engineIsXess = value == "XeSS";
                // XeSS can't do 5X/6X: the mod would turn FG off, so drop to 4X
                if (engineIsXess && framesCombo != null && framesCombo.SelectedIndex is 3 or 4)
                    framesCombo.SelectedIndex = 2;
                return true;
            });

        framesCombo = AddSetting(Loc.GetString("Extras.Dlssg2030.Settings.MaxFrames.Label"),
            Loc.GetString("Extras.Dlssg2030.Settings.MaxFrames.Help"),
            "FrameGeneration", "MaxGeneratedFrames", "3",
            new[] { (Loc.GetString("Extras.Dlssg2030.Settings.Opt2X"), "1"), (Loc.GetString("Extras.Dlssg2030.Settings.Opt3X"), "2"), (Loc.GetString("Extras.Dlssg2030.Settings.Opt4XDefault"), "3"), (Loc.GetString("Extras.Dlssg2030.Settings.Opt5X"), "4"), (Loc.GetString("Extras.Dlssg2030.Settings.Opt6X"), "5"), (Loc.GetString("Extras.Dlssg2030.Settings.OptGameDecides"), "0") },
            beforeWrite: value =>
            {
                if ((value == "4" || value == "5") && engineIsXess)
                {
                    cog.SetError(Loc.GetString("Extras.Dlssg2030.Settings.XessMaxFrames"));
                    return false;
                }
                return true;
            });

        AddSetting(Loc.GetString("Extras.Dlssg2030.Settings.Preset.Label"),
            Loc.GetString("Extras.Dlssg2030.Settings.Preset.Help"),
            "DLSSG", "Preset", "A",
            new[] { (Loc.GetString("Extras.Dlssg2030.Settings.Preset.OptA"), "A"), (Loc.GetString("Extras.Dlssg2030.Settings.Preset.OptB"), "B"), (Loc.GetString("Extras.Dlssg2030.Settings.Preset.OptC"), "C") });

        AddSetting(Loc.GetString("Extras.Dlssg2030.Settings.SpeedTier.Label"),
            Loc.GetString("Extras.Dlssg2030.Settings.SpeedTier.Help"),
            "DLSSG", "Optimized", "0",
            new[] { (Loc.GetString("Extras.Dlssg2030.Settings.SpeedTier.OptExact"), "0"), (Loc.GetString("Extras.Dlssg2030.Settings.SpeedTier.OptExactOpt"), "1"), (Loc.GetString("Extras.Dlssg2030.Settings.SpeedTier.OptFast"), "2"), (Loc.GetString("Extras.Dlssg2030.Settings.SpeedTier.OptFastest"), "3") });

        AddSetting(Loc.GetString("Extras.Dlssg2030.Settings.SuperResolution.Label"),
            Loc.GetString("Extras.Dlssg2030.Settings.SuperResolution.Help"),
            "SuperResolution", "Mode", "Off",
            new[] { (Loc.GetString("Extras.Dlssg2030.Settings.SuperResolution.OptOffDefault"), "Off"), ("M-INT8", "M-INT8"), ("L-INT8", "L-INT8"), ("M-FP16", "M-FP16"), ("L-FP16", "L-FP16") });

        cog.AddSection(Loc.GetString("Extras.Dlssg2030.Settings.Files"));
        cog.AddButtons(cog.MakeOpenFileButton(Loc.GetString("Extras.Dlssg2030.Settings.OpenIni"), iniPath), cog.MakeOpenFileButton(Loc.GetString("Extras.Dlssg2030.Settings.OpenLog"), logPath));

        await cog.ShowAsync();
    }
}
