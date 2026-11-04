// MainWindow.Events.Components.DisplayCommander.cs — Display Commander (DC) cog dialog.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private const string DcSection = "DisplayCommander";

    /// <summary>
    /// Display Commander ⚙: the [DisplayCommander] settings worth choosing before launch, written to the
    /// game's DisplayCommander.ini. Labels match Display Commander's own menu. Defaults are the mod's own
    /// (settings/main_tab_settings.cpp and advanced_tab_settings.cpp in pmnoxx/display-commander).
    /// </summary>
    private async void DcCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;

            // DisplayCommander.ini lives in the addon deploy path (honours AddonPath in reshade.ini), same as CopyDcIni.
            // Re-resolved on every write, as AddonPath may change while the dialog is open.
            string? CurrentIni() => string.IsNullOrEmpty(card.InstallPath)
                ? null
                : Path.Combine(ModInstallService.GetAddonDeployPath(card.InstallPath), "DisplayCommander.ini");
            var dcIniFile = CurrentIni();

            string? Read(string key)
            {
                try { return dcIniFile != null ? Dlssg20_30Service.ReadIniValue(dcIniFile, DcSection, key) : null; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    CrashReporter.Log($"[MainWindow.DcCogButton_Click] Couldn't read {key} — {ex.Message}");
                    return null;
                }
            }
            int ReadInt(string key, int fallback) => int.TryParse(Read(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
            bool ReadBool(string key) => ReadInt(key, 0) != 0;
            double fpsLimit = double.TryParse(Read("fps_limit"), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0 ? f : 0;

            var cog = new CogDialog(Loc.GetString("Detail.DisplayCommander"), Content.XamlRoot);

            // Writes one key; shows the result in the dialog. Returns false (pick reverted) on failure.
            bool Write(string key, string value, string label, string shown)
            {
                var iniFile = CurrentIni();
                if (iniFile == null || !Directory.Exists(Path.GetDirectoryName(iniFile)))
                {
                    cog.SetError(Loc.GetString("Dialog.Dc.FolderNotFound"));
                    return false;
                }
                try
                {
                    Dlssg20_30Service.WriteIniValue(iniFile, DcSection, key, value);
                    cog.SetSuccess(Loc.GetString("Dialog.Dc.WriteSuccess", label, shown));
                    CrashReporter.Log($"[MainWindow.DcCogButton_Click] {key}={value} for '{card.GameName}'");
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    cog.SetError(Loc.GetString("Dialog.Dc.WriteFailed", label, ex.Message));
                    CrashReporter.Log($"[MainWindow.DcCogButton_Click] {key} write failed — {ex.Message}");
                    return false;
                }
            }
            CogDialog.CompactCell OnOff(string key, string label, string help) =>
                cog.CompactCombo(label, help, new[] { LocOpt.T("Off"), LocOpt.T("On") }, ReadBool(key) ? 1 : 0,
                    i => Write(key, i == 1 ? "1" : "0", label, LocOpt.T(i == 1 ? "On" : "Off")));

            cog.AddIntro(Loc.GetString("Dialog.Dc.Intro"));
            if (card.IsRunning)
                cog.AddWarning(Loc.GetString("Dialog.Dc.GameRunningWarning"));

            // ── Frame rate ─────────────────────────────────────────────────────────
            cog.AddSection(Loc.GetString("Dialog.Dc.SectionFrameRate"));
            AddDcFpsLimitRow(cog, fpsLimit, fps => Write("fps_limit",
                fps.ToString("0.###", CultureInfo.InvariantCulture), Loc.GetString("Xaml.FpsLimit"),
                fps == 0 ? LocOpt.T("No limit") : Loc.GetString("Dialog.Dc.FpsValue", fps.ToString("0.###", CultureInfo.InvariantCulture))));

            var limiterModes = new[] { LocOpt.T("DC's fps limiter"), LocOpt.T("NVIDIA Reflex") };
            var windowModes = new[] { LocOpt.T("No changes"), LocOpt.T("Borderless fullscreen"), LocOpt.T("Borderless windowed"), LocOpt.T("No exclusive fullscreen") };
            int windowMode = Math.Clamp(ReadInt("WindowMode", 0), 0, windowModes.Length - 1);
            cog.AddCompactGrid(
                cog.CompactCombo(Loc.GetString("Dialog.Dc.FpsLimiterMode"),
                    Loc.GetString("Dialog.Dc.FpsLimiterMode.Help"),
                    limiterModes, Math.Clamp(ReadInt("fps_limiter_mode", 0), 0, 1),
                    i => Write("fps_limiter_mode", i.ToString(CultureInfo.InvariantCulture), Loc.GetString("Dialog.Dc.FpsLimiterMode"), limiterModes[i])),
                OnOff("inject_reflex", Loc.GetString("Dialog.Dc.InjectReflex"),
                    Loc.GetString("Dialog.Dc.InjectReflex.Help")),
                cog.CompactCombo(Loc.GetString("Dialog.Dc.WindowMode"),
                    Loc.GetString("Dialog.Dc.WindowMode.Help"),
                    windowModes, windowMode,
                    i => Write("WindowMode", i.ToString(CultureInfo.InvariantCulture), Loc.GetString("Dialog.Dc.WindowMode"), windowModes[i])),
                null);

            // ── Reflex (Advanced tab, "NVIDIA Reflex (Minimal)") ──────────────────
            cog.AddSection(Loc.GetString("Dialog.Dc.SectionReflex"));
            cog.AddCompactGrid(
                OnOff("ReflexAutoConfigure", Loc.GetString("Dialog.Dc.AutoConfigureReflex"), Loc.GetString("Dialog.Dc.AutoConfigureReflex.Help")),
                OnOff("ReflexEnableSleep", Loc.GetString("Dialog.Dc.EnableReflexSleepMode"), Loc.GetString("Dialog.Dc.EnableReflexSleepMode.Help")),
                OnOff("ReflexUseMarkers", Loc.GetString("Dialog.Dc.UseReflexMarkers"), Loc.GetString("Dialog.Dc.UseReflexMarkers.Help")),
                OnOff("ReflexGenerateMarkers", Loc.GetString("Dialog.Dc.GenerateReflexMarkers"), Loc.GetString("Dialog.Dc.GenerateReflexMarkers.Help")));

            // ── Other ─────────────────────────────────────────────────────────────
            cog.AddSection(Loc.GetString("Dialog.Dc.SectionOther"));
            var graceSeconds = new[] { 0, 1, 2, 5, 10, 61 };
            var graceLabels = new[] { LocOpt.T("Off"), LocOpt.T("1 second"), LocOpt.T("2 seconds"), LocOpt.T("5 seconds"), LocOpt.T("10 seconds"), LocOpt.T("Always") };
            int grace = ReadInt("WinUpGraceSeconds", 1);
            int graceIdx = Array.IndexOf(graceSeconds, grace);
            var graceItems = graceLabels.ToList();
            if (graceIdx < 0) { graceItems.Add(Loc.GetString("Dialog.Dc.SecondsValue", grace)); graceIdx = graceItems.Count - 1; }
            cog.AddCompactGrid(
                cog.CompactCombo(Loc.GetString("Dialog.Dc.WinUpGraceTime"),
                    Loc.GetString("Dialog.Dc.WinUpGraceTime.Help"),
                    graceItems, graceIdx, i =>
                    {
                        if (i >= graceSeconds.Length) return true; // existing custom value
                        return Write("WinUpGraceSeconds", graceSeconds[i].ToString(CultureInfo.InvariantCulture), Loc.GetString("Dialog.Dc.WinUpGraceTime"), graceLabels[i]);
                    }),
                null);
            cog.AddNote(Loc.GetString("Dialog.Dc.TargetDisplayNote"));

            // ── Files ─────────────────────────────────────────────────────────────
            cog.AddSection(Loc.GetString("Dialog.Dc.SectionFiles"));
            var openIniBtn = cog.MakeOpenFileButton(Loc.GetString("Dialog.Dc.OpenIni"), dcIniFile);
            var deployBtn = cog.MakeButton(Loc.GetString("Dialog.DeployDisplayCommanderIni"), CogButtonKind.Primary, () =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                AuxInstallService.CopyDcIni(card.InstallPath);
                openIniBtn.IsEnabled = dcIniFile != null && File.Exists(dcIniFile);
                cog.SetSuccess(Loc.GetString("Dialog.Dc.IniCopied"));
                CrashReporter.Log($"[MainWindow.DcCogButton_Click] Deployed DisplayCommander.ini for '{card.GameName}'");
            }, Loc.GetString("Dialog.Dc.DeployTooltip"));
            cog.AddButtons(deployBtn, openIniBtn);

            // No default button: Enter in the FPS box must not close the dialog
            cog.Build().DefaultButton = ContentDialogButton.None;
            await cog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.DcCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// FPS Limit dropdown (No limit, VRR presets, Custom...) with an inline box for a custom value.
    /// Display Commander stores a decimal (e.g. 239.99), so custom values may have decimals.
    /// </summary>
    private void AddDcFpsLimitRow(CogDialog cog, double current, Func<double, bool> write)
    {
        var presets = VrrFpsPresets;
        string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        List<string> Items(double fps)
        {
            var items = new List<string> { LocOpt.T("No limit") };
            items.AddRange(presets.Select(p => p.Label));
            if (fps > 0 && !presets.Any(p => p.Fps == fps)) items.Add(Loc.GetString("Option.FpsCustomFormat", Fmt(fps)));
            items.Add(LocOpt.T("Custom..."));
            return items;
        }
        int Index(List<string> items, double fps)
        {
            if (fps <= 0) return 0;
            int i = Array.FindIndex(presets, p => p.Fps == fps);
            return i >= 0 ? i + 1 : items.Count - 2;
        }

        var items0 = Items(current);
        var combo = CogDialog.MakeCombo(items0, Index(items0, current));
        cog.AddRow(Loc.GetString("Xaml.FpsLimit"), combo,
            Loc.GetString("Dialog.Dc.FpsLimit.Help"));

        var box = CogDialog.MakeTextBox("", Loc.GetString("Dialog.Dc.CustomFpsPlaceholder"));
        var setBtn = CogDialog.StyledButton(Loc.GetString("Dialog.Set"), CogButtonKind.Secondary, compact: true);
        var customRow = cog.AddRow(Loc.GetString("Dialog.Dc.CustomFps"), CogDialog.Inline(box, setBtn), Loc.GetString("Dialog.Dc.CustomFps.Help"));
        customRow.Visibility = Visibility.Collapsed;

        bool suppress = false;
        void Refresh(double fps)
        {
            suppress = true;
            current = fps;
            var items = Items(fps);
            combo.ItemsSource = items;
            combo.SelectedIndex = Index(items, fps);
            customRow.Visibility = Visibility.Collapsed;
            suppress = false;
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress) return;
            if (combo.SelectedItem as string == LocOpt.T("Custom..."))
            {
                customRow.Visibility = Visibility.Visible;
                box.Text = "";
                box.Focus(FocusState.Programmatic);
                return;
            }
            customRow.Visibility = Visibility.Collapsed;
            int idx = combo.SelectedIndex;
            double fps;
            if (idx == 0) fps = 0;
            else if (idx > 0 && idx - 1 < presets.Length) fps = presets[idx - 1].Fps;
            else return; // the "(Custom)" item: already the current value
            if (write(fps)) current = fps;
            else Refresh(current);
        };

        void SetCustom()
        {
            var text = box.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps) || fps < 20 || fps > 1000)
            {
                cog.SetError(Loc.GetString("Dialog.Dc.CustomFpsError"));
                return;
            }
            fps = Math.Round(fps, 3);
            if (write(fps)) Refresh(fps);
        }
        setBtn.Click += (_, _) => SetCustom();
        box.KeyDown += (_, ev) =>
        {
            if (ev.Key != Windows.System.VirtualKey.Enter) return;
            ev.Handled = true;
            SetCustom();
        };
    }
}
