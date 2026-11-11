// MainWindow.Events.Components.FrameLimiters.cs — ReLimiter (UL) and Display Commander (DC) cog dialog handlers.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    /// <summary>VRR frame rate caps (same as the global settings), used by the ReLimiter and Display Commander ⚙.</summary>
    /// <remarks>Built on each access, not in a static initializer: the type initializer must not touch DI
    /// (MainWindowStaticInitTests runs it without a service provider).</remarks>
    private static (int Fps, string Label)[] VrrFpsPresets => new[]
    {
        (59,  LocOpt.T("59 (60Hz VRR)")),
        (73,  LocOpt.T("73 (75Hz VRR)")),
        (97,  LocOpt.T("97 (100Hz VRR)")),
        (116, LocOpt.T("116 (120Hz VRR)")),
        (138, LocOpt.T("138 (144Hz VRR)")),
        (157, LocOpt.T("157 (165Hz VRR)")),
        (171, LocOpt.T("171 (180Hz VRR)")),
        (189, LocOpt.T("189 (200Hz VRR)")),
        (224, LocOpt.T("224 (240Hz VRR)")),
        (258, LocOpt.T("258 (280Hz VRR)")),
        (275, LocOpt.T("275 (300Hz VRR)")),
        (324, LocOpt.T("324 (360Hz VRR)")),
        (416, LocOpt.T("416 (480Hz VRR)")),
        (431, LocOpt.T("431 (500Hz VRR)")),
    };

    private async void UlCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            // relimiter.ini lives in the addon deploy path (honours AddonPath in reshade.ini)
            string? ulIniFile = string.IsNullOrEmpty(card.InstallPath)
                ? null
                : Path.Combine(ModInstallService.GetAddonDeployPath(card.InstallPath), "relimiter.ini");
            bool ulIniExists = ulIniFile != null && File.Exists(ulIniFile);

            // Re-resolved on every write, as before (AddonPath may change while the dialog is open)
            string? CurrentUlIniFile() => string.IsNullOrEmpty(card.InstallPath)
                ? null
                : Path.Combine(ModInstallService.GetAddonDeployPath(card.InstallPath), "relimiter.ini");

            // Find the relimiter log file (relimiter_*.log)
            string? logFile = null;
            if (!string.IsNullOrEmpty(card.InstallPath) && Directory.Exists(card.InstallPath))
            {
                try
                {
                    logFile = Directory.GetFiles(card.InstallPath, "relimiter_*.log").FirstOrDefault();
                }
                catch (Exception ex) { CrashReporter.Log($"[MainWindow.UlCogButton_Click] Failed to locate relimiter log file — {ex.Message}"); }
            }

            // Read current per-game values from the game's relimiter.ini
            int currentTargetFps = 0;
            bool currentDlssHooks = ViewModel.Settings.UlDlssHooks; // default to global
            if (ulIniExists)
            {
                try
                {
                    var ulIni = AuxInstallService.ParseIni(File.ReadAllLines(ulIniFile!));
                    if (ulIni.TryGetValue("FrameLimiter", out var flSection))
                    {
                        if (flSection.TryGetValue("target_fps", out var fpsVal) && int.TryParse(fpsVal, out var parsedFps))
                            currentTargetFps = parsedFps;
                        if (flSection.TryGetValue("dlss_info_hooks", out var hooksVal))
                            currentDlssHooks = hooksVal.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex) { CrashReporter.Log($"[MainWindow.UlCogButton_Click] Failed to read relimiter.ini, using defaults — {ex.Message}"); }
            }

            var cog = new CogDialog(Loc.GetString("Detail.ReLimiter"), Content.XamlRoot);

            // ── Frame Limiter ─────────────────────────────────────────────────────
            cog.AddSection(Loc.GetString("Dialog.FrameLimiter"));
            if (!ulIniExists)
                cog.AddWarning(Loc.GetString("Dialog.Ul.DeployWarning"));

            var vrrPresets = VrrFpsPresets;
            var vrrFpsSet = new HashSet<int>(vrrPresets.Select(p => p.Fps));

            // Items: Off, presets, "<n> (Custom)" when the current value isn't a preset, "Custom..."
            List<string> BuildFpsItems(int fps)
            {
                var items = new List<string> { LocOpt.T("Off") };
                items.AddRange(vrrPresets.Select(p => p.Label));
                if (fps > 0 && !vrrFpsSet.Contains(fps))
                    items.Add(Loc.GetString("Dialog.CustomFpsFormat", fps));
                items.Add(LocOpt.T("Custom..."));
                return items;
            }
            int FpsIndex(List<string> items, int fps)
            {
                if (fps == 0) return 0; // Off
                int matchIdx = Array.FindIndex(vrrPresets, p => p.Fps == fps);
                return matchIdx >= 0 ? matchIdx + 1 : items.Count - 2; // +1 for "Off"; custom item sits before "Custom..."
            }

            var fpsItems = BuildFpsItems(currentTargetFps);
            var targetFpsCombo = CogDialog.MakeCombo(fpsItems, FpsIndex(fpsItems, currentTargetFps));
            bool suppressFpsChange = false;

            var targetFpsRow = cog.AddRow(Loc.GetString("Xaml.TargetFps"), targetFpsCombo,
                Loc.GetString("Dialog.TargetFps.Tooltip"));

            // Inline custom FPS input (shown when "Custom..." is selected; no nested dialogs)
            var customFpsBox = CogDialog.MakeTextBox("", Loc.GetString("Dialog.201000"));
            var customFpsBtn = CogDialog.StyledButton(Loc.GetString("Dialog.Set"), CogButtonKind.Secondary, compact: true);
            var customFpsRow = cog.AddRow(Loc.GetString("Dialog.Ul.CustomFps"), CogDialog.Inline(customFpsBox, customFpsBtn),
                Loc.GetString("Dialog.Ul.CustomFps.Help"));
            customFpsRow.Visibility = Visibility.Collapsed;

            // Rebuild the combo after setting a custom value
            void RefreshFpsCombo(int newFps)
            {
                suppressFpsChange = true;
                currentTargetFps = newFps;
                var items = BuildFpsItems(newFps);
                targetFpsCombo.ItemsSource = items;
                targetFpsCombo.SelectedIndex = FpsIndex(items, newFps);
                customFpsRow.Visibility = Visibility.Collapsed;
                suppressFpsChange = false;
            }

            // Writes target_fps; returns false (with the error shown) on failure
            bool WriteTargetFps(int fps)
            {
                var iniFile = CurrentUlIniFile();
                if (iniFile == null || !File.Exists(iniFile))
                {
                    cog.SetError(Loc.GetString("Dialog.Ul.IniNotFound"));
                    return false;
                }
                try
                {
                    AuxInstallService.ApplyUlTargetFps(iniFile, fps);
                    currentTargetFps = fps;
                    cog.SetSuccess(fps == 0 ? Loc.GetString("Ul.Status.TargetFpsDisabled") : Loc.GetString("Ul.Status.TargetFpsSet", fps));
                    CrashReporter.Log($"[MainWindow.UlCogButton_Click] target_fps={fps} for '{card.GameName}'");
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    cog.SetError(Loc.GetString("Dialog.Ul.SaveFailed", Loc.GetString("Xaml.TargetFps"), ex.Message));
                    CrashReporter.Log($"[MainWindow.UlCogButton_Click] target_fps write failed — {ex.Message}");
                    return false;
                }
            }

            targetFpsCombo.SelectionChanged += (s, ev) =>
            {
                if (suppressFpsChange) return;
                if (string.IsNullOrEmpty(card.InstallPath)) return;

                var selectedText = targetFpsCombo.SelectedItem as string ?? "";

                // "Custom..." shows the inline TextBox for manual entry
                if (selectedText == LocOpt.T("Custom..."))
                {
                    customFpsRow.Visibility = Visibility.Visible;
                    customFpsBox.Text = "";
                    customFpsBox.Focus(FocusState.Programmatic);
                    return;
                }

                customFpsRow.Visibility = Visibility.Collapsed;

                // Handle preset/Off selection
                int newFps;
                var idx = targetFpsCombo.SelectedIndex;
                if (idx == 0)
                    newFps = 0; // Off
                else if (idx > 0 && idx - 1 < vrrPresets.Length)
                    newFps = vrrPresets[idx - 1].Fps;
                else
                    return; // Custom label item — don't set

                WriteTargetFps(newFps);
            };

            void SetCustomFps()
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                if (!int.TryParse(customFpsBox.Text, out var customFps) || customFps < 20 || customFps > 1000)
                {
                    cog.SetError(Loc.GetString("Dialog.Ul.CustomFpsError"));
                    return;
                }
                if (WriteTargetFps(customFps))
                    RefreshFpsCombo(customFps);
            }
            customFpsBtn.Click += (s, ev) => SetCustomFps();
            customFpsBox.KeyDown += (s, ev) =>
            {
                if (ev.Key == Windows.System.VirtualKey.Enter)
                {
                    ev.Handled = true;
                    SetCustomFps();
                }
            };

            // ── Compatibility ─────────────────────────────────────────────────────
            cog.AddSection(Loc.GetString("Dialog.CompatibilitySettings"));
            var dlssHooksCombo = cog.AddCombo(Loc.GetString("Dialog.DlssHooks"),
                Loc.GetString("Dialog.DlssHooks.Tooltip"),
                new[] { LocOpt.T("Off"), LocOpt.T("On") }, currentDlssHooks ? 1 : 0, i =>
                {
                    var iniFile = CurrentUlIniFile();
                    if (iniFile == null || !File.Exists(iniFile))
                    {
                        cog.SetError(Loc.GetString("Dialog.Ul.IniNotFound"));
                        return false;
                    }
                    try
                    {
                        AuxInstallService.ApplyUlDlssHooks(iniFile, i == 1);
                        cog.SetSuccess(i == 1 ? Loc.GetString("Ul.Status.DlssHooksEnabled") : Loc.GetString("Ul.Status.DlssHooksDisabled"));
                        CrashReporter.Log($"[MainWindow.UlCogButton_Click] dlss_info_hooks={(i == 1)} for '{card.GameName}'");
                        return true;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        cog.SetError(Loc.GetString("Dialog.Ul.SaveFailed", Loc.GetString("Dialog.DlssHooks"), ex.Message));
                        CrashReporter.Log($"[MainWindow.UlCogButton_Click] dlss_info_hooks write failed — {ex.Message}");
                        return false;
                    }
                });

            // Greyed out until relimiter.ini exists (rows stay in place so the layout doesn't jump)
            if (!ulIniExists)
            {
                // AddCombo returns the ComboBox; its row is combo → label grid → row panel
                var dlssHooksRow = ((dlssHooksCombo.Parent as FrameworkElement)?.Parent as FrameworkElement) ?? dlssHooksCombo;
                foreach (var row in new[] { targetFpsRow, dlssHooksRow })
                {
                    row.Opacity = CogDialog.DisabledOpacity;
                    row.IsHitTestVisible = false;
                }
                targetFpsCombo.IsTabStop = false;
                dlssHooksCombo.IsTabStop = false;
            }

            // ── Files ─────────────────────────────────────────────────────────────
            cog.AddSection(Loc.GetString("Dialog.Ul.SectionFiles"));
            var openIniBtn = cog.MakeOpenFileButton(Loc.GetString("Dialog.Ul.OpenIni"), ulIniFile);
            var deployBtn = cog.MakeButton(Loc.GetString("Dialog.DeployRelimiterIni"), CogButtonKind.Primary, () =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                AuxInstallService.CopyUlIni(card.InstallPath);
                openIniBtn.IsEnabled = ulIniFile != null && File.Exists(ulIniFile);
                cog.SetSuccess(Loc.GetString("Dialog.Ul.IniCopied"));
                CrashReporter.Log($"[MainWindow.UlCogButton_Click] Deployed relimiter.ini for '{card.GameName}'");
            }, Loc.GetString("Dialog.Ul.DeployTooltip"));

            var openLogBtn = cog.MakeOpenFileButton(Loc.GetString("Dialog.OpenRelimiterLog"), logFile);

            // Copied as a file (not text) so Discord shows the log's real name
            var copyLogBtn = cog.MakeButton(Loc.GetString("Dialog.CopyRelimiterLogToClipboard"), CogButtonKind.Secondary, async () =>
            {
                if (logFile == null || !File.Exists(logFile)) { cog.SetError(Loc.GetString("Dialog.Ul.LogNotFound")); return; }
                await CopyFileToClipboardAsFileAsync(logFile);
                cog.SetSuccess(Loc.GetString("Dialog.Ul.LogCopied", Path.GetFileName(logFile)));
            }, Loc.GetString("Dialog.Ul.CopyLogTooltip"));
            copyLogBtn.IsEnabled = logFile != null;

            cog.AddButtons(deployBtn, openIniBtn, openLogBtn, copyLogBtn);

            // No default button: Enter in the key/number boxes must not close the dialog
            cog.Build().DefaultButton = ContentDialogButton.None;
            await cog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.UlCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

}
