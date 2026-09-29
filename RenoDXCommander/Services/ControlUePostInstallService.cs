// ControlUePostInstallService.cs
// Special post-install steps for the Control Ultimate Edition RenoDX mod
// (renodx-control-rr.addon64). Triggered from all three install paths:
// normal InstallModAsync, drag-drop, and watch folder.
//
// Steps performed after the addon file is placed:
//   1. Upgrade nvngx_dlss.dll  → RHI's newest cached version (sentinel backup)
//   2. Deploy nvngx_dlssd.dll  → RHI's newest cached version (sentinel backup)
//   3. Edit renderer.ini       → set "m_eHDRPreset": 2
//   4. Clear SR DLSS preset    → delete from NVIDIA driver profile (inherits global)

using Microsoft.Extensions.DependencyInjection;

namespace RenoDXCommander.Services;

public static class ControlUePostInstallService
{
    /// <summary>
    /// The exact addon filename that triggers this special install flow.
    /// </summary>
    public const string TriggerAddonFileName = "renodx-control-rr.addon64";

    /// <summary>
    /// The exact game name as detected by RHI (Steam folder name).
    /// </summary>
    public const string GameName = "Control Ultimate Edition";

    /// <summary>
    /// Returns true when <paramref name="addonFileName"/> matches the Control UE trigger.
    /// Case-insensitive.
    /// </summary>
    public static bool IsControlAddon(string addonFileName)
        => addonFileName.Equals(TriggerAddonFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Shows the bespoke pre-install dialog for Control Ultimate Edition.
    /// Returns true if the user clicks Install (proceed), false if they cancel.
    /// </summary>
    /// <param name="installPath">Game install path — shown in the dialog.</param>
    public static async Task<bool> ShowInstallDialogAsync(string installPath)
    {
        try
        {
            Microsoft.UI.Xaml.XamlRoot? xamlRoot = null;
            if (Microsoft.UI.Xaml.Application.Current is App app)
            {
                var field = typeof(App).GetField("_window",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(app) is MainWindow mw)
                    xamlRoot = mw.Content?.XamlRoot;
            }
            if (xamlRoot == null) return true; // Can't show dialog — proceed anyway

            var content = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 10 };

            content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "⚠ This is NOT an HDR mod.",
                FontSize = 14,
                FontWeight = new Windows.UI.Text.FontWeight(700),
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            });

            content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "It fixes RT noise using Ray Reconstruction. Two strategies (pick one, they are mutually exclusive):\n"
                     + "  •  Turn off the in-game RT denoiser and use DLSS Super Resolution preset M or L\n"
                     + "  •  Use Ray Reconstruction with extra inputs derived from the game's shaders "
                     + "(game denoiser is turned off here too — RR needs that)",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                FontSize = 13,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            });

            content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "Clicking Install will also:",
                FontSize = 13,
                FontWeight = new Windows.UI.Text.FontWeight(600),
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
                Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 0),
            });

            content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "  •  Upgrade nvngx_dlss.dll to the newest available version\n"
                     + "  •  Deploy nvngx_dlssd.dll (DLSS Ray Reconstruction runtime)\n"
                     + "  •  Set renderer.ini HDR preset to the correct value\n"
                     + "  •  Clear the DLSS SR preset set in the NVIDIA driver profile for this game",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                FontSize = 13,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            });

            content.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "These extra changes are not reverted when uninstalling the mod.",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                FontSize = 12,
                Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
                Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 0),
            });

            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Control Ultimate Edition — RenoDX Mod",
                Content = content,
                PrimaryButtonText = "Install",
                CloseButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark,
            };

            var result = await DialogService.ShowSafeAsync(dialog);
            return result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[ControlUePostInstall] ShowInstallDialogAsync failed — {ex.Message}");
            return true; // Proceed on error
        }
    }

    /// <summary>
    /// Runs all five post-install steps for Control Ultimate Edition.
    /// Safe to call on any thread — all file operations are synchronous after awaiting DLSS cache.
    /// Each step is independent: failures are logged and do not abort subsequent steps.
    /// </summary>
    /// <param name="gameName">Card game name (used for NVIDIA profile lookup).</param>
    /// <param name="installPath">Game install directory (root, where the addon was deployed).</param>
    public static async Task RunAsync(string gameName, string installPath)
    {
        CrashReporter.Log($"[ControlUePostInstall] Running post-install steps for '{gameName}' at '{installPath}'");

        // ── Step 1: Upgrade nvngx_dlss.dll ────────────────────────────────────
        await UpgradeDlssAsync(installPath);

        // ── Step 2: Deploy nvngx_dlssd.dll ────────────────────────────────────
        await DeployDlssdAsync(installPath);

        // ── Step 3: Edit renderer.ini — set m_eHDRPreset to 2 ─────────────────
        PatchRendererIni(installPath);

        // ── Step 4 & 5: Clear SR DLSS preset and render scale from driver ──────
        ClearNvidiaProfileSettings(gameName, installPath);

        CrashReporter.Log($"[ControlUePostInstall] All steps complete for '{gameName}'");
    }

    // ── Step 1 ────────────────────────────────────────────────────────────────

    private static async Task UpgradeDlssAsync(string installPath)
    {
        try
        {
            var dlssSvc = App.Services.GetRequiredService<IDlssStreamlineService>();
            var newestDlssPath = await dlssSvc.EnsureNewestDlssCachedAsync().ConfigureAwait(false);
            if (newestDlssPath == null || !File.Exists(newestDlssPath))
            {
                CrashReporter.Log("[ControlUePostInstall] Step 1 skipped — newest nvngx_dlss.dll not cached");
                return;
            }

            var destPath = Path.Combine(installPath, "nvngx_dlss.dll");
            AuxInstallService.SentinelBackup(destPath);
            File.Copy(newestDlssPath, destPath, overwrite: true);
            CrashReporter.Log($"[ControlUePostInstall] Step 1 — deployed nvngx_dlss.dll to '{destPath}'");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[ControlUePostInstall] Step 1 failed — {ex.Message}");
        }
    }

    // ── Step 2 ────────────────────────────────────────────────────────────────

    private static async Task DeployDlssdAsync(string installPath)
    {
        try
        {
            var dlssSvc = App.Services.GetRequiredService<IDlssStreamlineService>();
            var newestDlssdPath = await dlssSvc.EnsureNewestDlssdCachedAsync().ConfigureAwait(false);
            if (newestDlssdPath == null || !File.Exists(newestDlssdPath))
            {
                CrashReporter.Log("[ControlUePostInstall] Step 2 skipped — newest nvngx_dlssd.dll not cached");
                return;
            }

            var destPath = Path.Combine(installPath, "nvngx_dlssd.dll");
            AuxInstallService.SentinelBackup(destPath);
            File.Copy(newestDlssdPath, destPath, overwrite: true);
            CrashReporter.Log($"[ControlUePostInstall] Step 2 — deployed nvngx_dlssd.dll to '{destPath}'");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[ControlUePostInstall] Step 2 failed — {ex.Message}");
        }
    }

    // ── Step 3 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds renderer.ini in <paramref name="installPath"/> and changes
    /// "m_eHDRPreset": &lt;any value&gt; → "m_eHDRPreset": 2.
    /// The file is a JSON-like settings file; the key may have any integer value.
    /// </summary>
    private static void PatchRendererIni(string installPath)
    {
        try
        {
            var iniPath = Path.Combine(installPath, "renderer.ini");
            if (!File.Exists(iniPath))
            {
                CrashReporter.Log($"[ControlUePostInstall] Step 3 skipped — renderer.ini not found at '{iniPath}'");
                return;
            }

            var content = File.ReadAllText(iniPath);

            // Match "m_eHDRPreset": <digits> and replace with value 2.
            // Uses regex so it handles any spacing and any current integer value.
            var patched = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"""m_eHDRPreset""\s*:\s*\d+",
                @"""m_eHDRPreset"": 2");

            if (patched == content)
            {
                CrashReporter.Log("[ControlUePostInstall] Step 3 — m_eHDRPreset key not found or already at 2, no change");
                return;
            }

            File.WriteAllText(iniPath, patched);
            CrashReporter.Log($"[ControlUePostInstall] Step 3 — patched renderer.ini: m_eHDRPreset set to 2");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[ControlUePostInstall] Step 3 failed — {ex.Message}");
        }
    }

    // ── Steps 4 & 5 ──────────────────────────────────────────────────────────

    private static void ClearNvidiaProfileSettings(string gameName, string installPath)
    {
        try
        {
            var presetSvc = App.Services.GetRequiredService<DlssPresetService>();

            if (!presetSvc.IsSupported)
            {
                CrashReporter.Log("[ControlUePostInstall] Steps 4 & 5 skipped — NVIDIA profile API not supported");
                return;
            }

            // Step 4: Clear SR preset (0 = delete setting → inherits from global/base profile)
            presetSvc.SetSrPreset(gameName, installPath, 0u);
            CrashReporter.Log("[ControlUePostInstall] Step 4 — cleared SR DLSS preset from driver profile");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[ControlUePostInstall] Steps 4 & 5 failed — {ex.Message}");
        }
    }
}
