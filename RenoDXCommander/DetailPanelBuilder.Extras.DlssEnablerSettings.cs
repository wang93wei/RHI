// DetailPanelBuilder.Extras.DlssEnablerSettings.cs — ⚙ for the standalone DLSS Enabler: its frame generation settings
// in dlss-enabler.ini (beside its DLL), set before the game starts. Help texts follow the comments DLSS Enabler
// 4.10.0.23 writes in that file. The rest (ray tracing, colour grade, Reflex, debug) stays in its in-game menu (~).

using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    /// <summary>⚙ dialog for the standalone DLSS Enabler. Every change is saved as soon as it's made.</summary>
    private async Task ShowDlssEnablerSettingsAsync(GameCardViewModel card, DlssEnablerService svc, string installPath, bool isInstalled)
    {
        var gameName = card.GameName;
        bool canEdit = isInstalled && !string.IsNullOrEmpty(installPath);
        string? cfgPath = canEdit ? DlssEnablerConfig.PathIn(installPath) : null;
        string cfgText = "";
        if (canEdit)
        {
            // Installed before RHI wrote the file at install: create the defaults now so there is something to change
            cfgText = await Task.Run(() =>
            {
                svc.EnsureStandaloneConfig(installPath);
                try { return File.Exists(cfgPath) ? File.ReadAllText(cfgPath!) : ""; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    CrashReporter.Log($"[DlssEnablerCog] Couldn't read '{cfgPath}' — {ex.Message}");
                    return "";
                }
            });
            if (cfgText.Length == 0) canEdit = false;
        }

        // Localized display pairs: display text shown, ini value written
        var offOn    = new[] { LocOpt.T("Off"), LocOpt.T("On") };
        var offOnVal = new[] { "false", "true" };

        var cog = new CogDialog(Loc.GetString("Extras.DlssEnabler"), _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Extras.DlssEnabler.Settings.Intro"));
        if (!isInstalled)
            cog.AddNote(Loc.GetString("Extras.DlssEnabler.Settings.NoteInstall"));
        else if (!canEdit)
            cog.AddWarning(Loc.GetString("Extras.DlssEnabler.Settings.WarningNoIni"));
        if (card.IsRunning)
            cog.AddWarning(Loc.GetString("Extras.DlssEnabler.Settings.WarningRunning"));

        // One dropdown bound to one value in dlss-enabler.ini. A current value not in the list is shown as an extra item.
        CogDialog.CompactCell Choice(string label, string help, string section, string key,
            string[] displays, string[] values, string defaultValue)
        {
            var current = canEdit ? DlssEnablerConfig.GetFromText(cfgText, section, key) : null;
            var d = displays.ToList();
            var v = values.ToList();
            if (current != null && !v.Contains(current, StringComparer.OrdinalIgnoreCase)) { d.Add(current); v.Add(current); }
            int idx = v.FindIndex(x => string.Equals(x, current ?? defaultValue, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = v.FindIndex(x => string.Equals(x, defaultValue, StringComparison.OrdinalIgnoreCase));
            return cog.CompactCombo(label, help, d, idx, i =>
            {
                if (!canEdit) return false;
                DlssEnablerConfig.Set(cfgPath!, section, key, v[i]);
                CrashReporter.Log($"[DlssEnablerCog] '{gameName}': {section}.{key} = {v[i]}");
                cog.SetSuccess(Loc.GetString("Extras.DlssEnabler.Settings.Saved"));
                return true;
            }, isEnabled: canEdit);
        }

        cog.AddSection(Loc.GetString("Extras.DlssEnabler.Settings.SectionFrameGen"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.ForceLoad.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.ForceLoad.Help"),
                "Performance", "ForceLoadDLSSG", offOn, offOnVal, "false"),
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.Multiplier.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.Multiplier.Help"),
                "Performance", "MFGOverrideMode",
                new[] { LocOpt.T("Game decides"), LocOpt.T("Off (1x)"), "2x", "3x", "4x", "5x", "6x" },
                new[] { "0", "1", "2", "3", "4", "5", "6" }, "0"),
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.DynamicMfg.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.DynamicMfg.Help"),
                "Performance", "DynamicMFG", offOn, offOnVal, "false"),
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.DynamicTarget.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.DynamicTarget.Help"),
                "Performance", "DFGTargetFps",
                new[] { "60", "90", "120", "144", "165", "180", "200", "240", "300" },
                new[] { "60", "90", "120", "144", "165", "180", "200", "240", "300" }, "120"),
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.Hotkeys.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.Hotkeys.Help"),
                "Performance", "MFGHotkeys", offOn, offOnVal, "false"),
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.Generator.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.Generator.Help"),
                "Debug", "UseFsrOnly", new[] { "DLSS-G", LocOpt.T("FSR only") }, offOnVal, "false"),
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.HudDetection.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.HudDetection.Help"),
                "NGX", "HudDetectionMode", new[] { LocOpt.T("Game"), LocOpt.T("Off"), LocOpt.T("On") }, new[] { "0", "1", "2" }, "0"),
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.GhostBuster.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.GhostBuster.Help"),
                "GhostBuster", "Enabled", offOn, offOnVal, "true"));

        cog.AddSection(Loc.GetString("Extras.DlssEnabler.Settings.SectionOverlay"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.StatusBar.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.StatusBar.Help"),
                "UI", "Monitoring", offOn, offOnVal, "true"),
            Choice(Loc.GetString("Extras.DlssEnabler.Settings.MenuKey.Label"),
                Loc.GetString("Extras.DlssEnabler.Settings.MenuKey.Help"),
                "UI", "ToggleKey",
                new[] { "~ (" + LocOpt.T("Tilde") + ")", "F9", "F10", "F11", "F12" },
                new[] { "Tilde", "F9", "F10", "F11", "F12" }, "Tilde"));

        cog.AddButtons(cog.MakeOpenFileButton(Loc.GetString("Extras.DlssEnabler.Settings.OpenIni"), canEdit ? cfgPath : null));
        await cog.ShowAsync();
    }
}
