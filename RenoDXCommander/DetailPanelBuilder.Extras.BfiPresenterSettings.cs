// DetailPanelBuilder.Extras.BfiPresenterSettings.cs — BFI Presenter ⚙: this game's setup checked against the author's
// requirements (fixed refresh, VSync, no frame limiter, no Smooth Motion; Fix buttons for the NVIDIA profile), the
// tested-games entry, the main BFIPresenter.ini settings (keys and defaults from BFIPresenter.example.ini and the
// settings guide), and the Vulkan layer (all users, load order after ReShade, remove). Everything else stays in the
// BFI Presenter tab of the ReShade overlay.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private static string[] BfiOffOn => new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Xaml.On") };
    private static readonly string[] BfiZeroOne = { "0", "1" };

    private async Task ShowBfiPresenterSettingsAsync(GameCardViewModel card)
    {
        var vm          = _window.ViewModel;
        var svc         = App.Services.GetRequiredService<BfiPresenterService>();
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";
        var installedAs = vm.GetBfiPresenterInstalledAs(gameName, store);
        bool installed  = BfiPresenterService.IsInstalledIn(installPath, installedAs);
        var route       = installed ? BfiPresenterService.RouteOf(installedAs!) : BfiRouteFor(card).Route;
        var iniPath     = installed ? BfiPresenterService.IniPathOf(installPath, installedAs) : null;
        bool canEdit    = iniPath != null;
        bool dx = route == BfiRoute.Dx, vulkan = route == BfiRoute.Vulkan;

        // Gathered off the UI thread before the dialog is built: NVIDIA profile, tested list, RTX Encore settings
        var tested = BfiPresenterService.FindTested(await svc.GetTestedGamesAsync(), gameName);
        var issues = await Task.Run(() => CheckBfiSetup(card, installPath));

        var cog = new CogDialog(Loc.GetString("Extras.BfiPresenter"), _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Extras.Bfi.Settings.Intro",
            vulkan ? Loc.GetString("Extras.Bfi.Settings.Intro.VulkanTab") : "."));
        if (!installed)
            cog.AddNote(Loc.GetString("Extras.Bfi.Settings.InstallFirst"));
        else
        {
            var where = Path.GetDirectoryName(BfiPresenterService.FullPathOf(installPath, installedAs)!)!;
            cog.AddNote(Loc.GetString("Extras.Bfi.Settings.InstalledAs", BfiRouteLabel(route!.Value, card.Is32Bit),
                    svc.AnyStagedVersion ?? Loc.GetString("Extras.Bfi.Settings.VersionUnknown"))
                + (vulkan ? Loc.GetString("Extras.Bfi.Settings.IniVulkan", where)
                          : Loc.GetString("Extras.Bfi.Settings.SettingsFile", where)
                            + (File.Exists(iniPath) ? "." : Loc.GetString("Extras.Bfi.Settings.SettingsFile.Missing"))));
        }
        if (card.IsRunning)
            cog.AddWarning(vulkan
                ? Loc.GetString("Extras.Bfi.Settings.Running.Vulkan")
                : Loc.GetString("Extras.Bfi.Settings.Running.Dx"));

        // ── Tested-games list ───────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.Bfi.Settings.Section.TestedGames"));
        cog.AddNote(tested == null
            ? Loc.GetString("Extras.Bfi.Settings.TestedNotListed")
            : $"{tested.Name}: {tested.Status}. {tested.Notes}".Trim());

        // ── Setup check ─────────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.Bfi.Settings.Section.Setup"));
        if (issues.Count == 0)
            cog.AddNote(_dlssPresetService.IsSupported
                ? Loc.GetString("Extras.Bfi.Settings.SetupOk.Nvidia")
                : Loc.GetString("Extras.Bfi.Settings.SetupOk.Generic"));
        foreach (var issue in issues)
        {
            if (issue.Fix == null) { cog.AddWarning(issue.Text); continue; }
            var text = new TextBlock { Text = issue.Text, TextWrapping = TextWrapping.Wrap, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush) };
            Button? btn = null;
            btn = cog.MakeButton(Loc.GetString("Extras.Bfi.Settings.FixButton"), CogButtonKind.Secondary, async () =>
            {
                btn!.IsEnabled = false;
                bool ok = await Task.Run(issue.Fix);
                if (ok)
                {
                    RefreshDriverSettingsIfShown(card);
                    text.Text = issue.FixedText ?? Loc.GetString("Extras.Bfi.Settings.Fixed");
                    text.Foreground = UIFactory.Brush(ResourceKeys.AccentGreenBrush);
                    cog.SetSuccess(Loc.GetString("Extras.Bfi.Settings.FixSaved"));
                }
                else
                {
                    btn.IsEnabled = true;
                    cog.SetError(Loc.GetString("Extras.Bfi.Settings.FixFailed"));
                }
            });
            cog.AddContent(CogDialog.Inline(text, btn));
        }

        // ── Settings ────────────────────────────────────────────────────────────
        CogDialog.CompactCell Choice(string label, string help, string key, string[] displays, string[] values, string def)
        {
            var current = canEdit ? BfiPresenterService.GetIni(iniPath!, key) : null;
            int idx = Array.FindIndex(values, v => string.Equals(v, current ?? def, StringComparison.OrdinalIgnoreCase));
            if (idx < 0 && current != null)
            {
                // A value set in the overlay that isn't in the list: show it as it is
                displays = displays.Append(current).ToArray();
                values = values.Append(current).ToArray();
                idx = values.Length - 1;
            }
            if (idx < 0) idx = Math.Max(0, Array.IndexOf(values, def));
            return cog.CompactCombo(label, help, displays, idx, i =>
            {
                if (!canEdit) return false;
                BfiPresenterService.SetIni(iniPath!, key, values[i]);
                CrashReporter.Log($"[BfiPresenterCog] '{gameName}': {key} = {values[i]}");
                cog.SetSuccess(vulkan ? Loc.GetString("Extras.RtxEncore.Nr.Saved") : Loc.GetString("Extras.Bfi.Settings.SavedReload"));
                return true;
            }, isEnabled: canEdit);
        }
        CogDialog.CompactCell Number(string label, string help, string key, double min, double max, string def)
            => new(label, help, BuildBfiNumberBox(cog, iniPath, gameName, key, label, min, max, def));

        cog.AddSection(Loc.GetString("Extras.Bfi.Settings.Section.Strobe"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("NeuralRendering.Method.Label"), Loc.GetString("Extras.Bfi.Settings.Method.Help"),
                "Method", new[] { "BFI", Loc.GetString("Extras.Bfi.Settings.Method.VariableMprt") }, BfiZeroOne, "1"),
            Choice(Loc.GetString("Extras.Bfi.Settings.FramesPerCycle.Label"), Loc.GetString("Extras.Bfi.Settings.FramesPerCycle.Help"),
                "Frames", new[] { "2", "3", "4", "5", "6" }, new[] { "2", "3", "4", "5", "6" }, "2"),
            Choice(Loc.GetString("Extras.Bfi.Settings.Gain.Label"), Loc.GetString("Extras.Bfi.Settings.Gain.Help"),
                "Gain", new[] { "0.5", "0.75", "1.0" }, new[] { "0.5", "0.75", "1.0" }, "0.5"),
            Choice(Loc.GetString("Extras.Bfi.Settings.DarkFrameFloor.Label"), Loc.GetString("Extras.Bfi.Settings.DarkFrameFloor.Help"),
                "DarkFloor", new[] { "0", "0.05", "0.1", "0.15" }, new[] { "0.0", "0.05", "0.1", "0.15" }, "0.0"),
            Choice(Loc.GetString("Extras.Bfi.Settings.RollingScan.Label"), Loc.GetString("Extras.Bfi.Settings.RollingScan.Help"),
                "RollingScan", BfiOffOn, BfiZeroOne, "0"),
            Choice(Loc.GetString("Extras.Bfi.Settings.Osd.Label"), Loc.GetString("Extras.Bfi.Settings.Osd.Help"),
                "Osd", BfiOffOn, BfiZeroOne, "0"));

        cog.AddSection(Loc.GetString("Extras.Bfi.Settings.Section.Display"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.Bfi.Settings.LcdPhaseSwap.Label"), Loc.GetString("Extras.Bfi.Settings.LcdPhaseSwap.Help"),
                "LcdPhaseSwap", BfiOffOn, BfiZeroOne, "0"),
            Choice(Loc.GetString("Extras.Bfi.Settings.LcdSaver.Label"), Loc.GetString("Extras.Bfi.Settings.LcdSaver.Help"),
                "LcdSaverGradual", new[] { Loc.GetString("Extras.Bfi.Settings.LcdSaver.Immediate"), Loc.GetString("Extras.Bfi.Settings.LcdSaver.Gradual") }, BfiZeroOne, "0"),
            Choice(Loc.GetString("Extras.RtxEncore.Settings.NoDlss.Output.Label"), Loc.GetString("Extras.Bfi.Settings.Output.Help"),
                "Output", new[] { Loc.GetString("Option.Auto"), "SDR", "scRGB", "HDR10" }, new[] { "auto", "sdr", "scrgb", "hdr10" }, "auto"),
            Number(Loc.GetString("Extras.Bfi.Settings.DisplayPeak.Label"), Loc.GetString("Extras.Bfi.Settings.DisplayPeak.Help"),
                "DisplayPeakNits", 50, 10000, "1400"),
            Choice(Loc.GetString("Extras.Bfi.Settings.AblGuard.Label"), Loc.GetString("Extras.Bfi.Settings.AblGuard.Help"),
                "AblGuard", BfiOffOn, BfiZeroOne, "1"),
            Number(Loc.GetString("Extras.Bfi.Settings.PanelBudget.Label"), Loc.GetString("Extras.Bfi.Settings.PanelBudget.Help"),
                "AblGuardBudgetNits", 50, 1500, "250"));

        cog.AddSection(Loc.GetString("Extras.Bfi.Settings.Section.Smoothness"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.Bfi.Settings.GameFrameCap.Label"), Loc.GetString("Extras.Bfi.Settings.GameFrameCap.Help"),
                "CapGameToRefresh", new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Extras.Bfi.Settings.GameFrameCap.RefreshRate"), Loc.GetString("Extras.Bfi.Settings.GameFrameCap.HalfRefresh"), Loc.GetString("Extras.Bfi.Settings.GameFrameCap.QuarterRefresh"), Loc.GetString("Extras.Bfi.Settings.GameFrameCap.OnePerCycle") }, new[] { "0", "1", "2", "3", "4" }, "1"),
            Choice(Loc.GetString("Extras.Bfi.Settings.AutoFrameCap.Label"), Loc.GetString("Extras.Bfi.Settings.AutoFrameCap.Help"),
                "AutoFrameCap", BfiOffOn, BfiZeroOne, "1"),
            Choice(Loc.GetString("Extras.Bfi.Settings.FramePacing.Label"), Loc.GetString("Extras.Bfi.Settings.FramePacing.Help"),
                "FramePacing", new[] { Loc.GetString("Extras.Bfi.Settings.FramePacing.Smooth"), Loc.GetString("Extras.Bfi.Settings.FramePacing.LowestLatency") }, BfiZeroOne, "1"),
            Choice(Loc.GetString("Extras.Bfi.Settings.LaterRelease.Label"), Loc.GetString("Extras.Bfi.Settings.LaterRelease.Help"),
                "LaterRelease", BfiOffOn, BfiZeroOne, "0"));

        cog.AddSection(Loc.GetString("Extras.Bfi.Settings.Section.SdrInHdr"));
        cog.AddCompactGrid(
            Choice(Loc.GetString("Extras.Bfi.Settings.SdrToHdr.Label"), Loc.GetString("Extras.Bfi.Settings.SdrToHdr.Help")
                        + (vulkan ? Loc.GetString("Extras.Bfi.Settings.SdrToHdr.HelpVulkan") : ""),
                "SdrToHdr", vulkan ? new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Xaml.On"), Loc.GetString("Extras.Bfi.Settings.SdrToHdr.ForceInDxvk") } : BfiOffOn, vulkan ? new[] { "0", "1", "2" } : BfiZeroOne, "1"),
            Number(Loc.GetString("Extras.Bfi.Settings.SdrWhite.Label"), Loc.GetString("Extras.Bfi.Settings.SdrWhite.Help"),
                "SdrWhiteNits", 80, 1000, "200"));

        cog.AddSection(Loc.GetString("Extras.RtxEncore.Settings.SectionCompatibility"));
        if (vulkan)
            cog.AddCompactGrid(
                Choice(Loc.GetString("Extras.Bfi.Settings.VulkanLayer.Label"), Loc.GetString("Extras.Bfi.Settings.VulkanLayer.Help"),
                    "VulkanLayer", BfiOffOn, BfiZeroOne, "1"),
                Choice(Loc.GetString("Extras.Bfi.Settings.ExclusiveFullscreen.Label"), Loc.GetString("Extras.Bfi.Settings.ExclusiveFullscreen.Help"),
                    "VulkanExclusiveFullscreen", BfiOffOn, BfiZeroOne, "1"),
                Choice(Loc.GetString("Extras.Bfi.Settings.QueueAhead.Label"), Loc.GetString("Extras.Bfi.Settings.QueueAhead.Help"),
                    "QueueAhead", BfiOffOn, BfiZeroOne, "0"),
                Choice(Loc.GetString("Extras.Bfi.Settings.FrameGenDll.Label"), Loc.GetString("Extras.Bfi.Settings.FrameGenDll.Help"),
                    "FrameGenDllCheck", BfiOffOn, BfiZeroOne, "1"));
        else
            cog.AddCompactGrid(
                Choice(Loc.GetString("Extras.Bfi.Settings.PresenterResize.Label"), Loc.GetString("Extras.Bfi.Settings.PresenterResize.Help"),
                    "PresenterResize", new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Xaml.On"), Loc.GetString("Extras.Bfi.Settings.PresenterResize.AlsoRecreated") }, new[] { "0", "1", "2" }, "0"),
                Choice(Loc.GetString("Extras.Bfi.Settings.QueueAhead.Label"), Loc.GetString("Extras.Bfi.Settings.QueueAhead.Help.Dx"),
                    "QueueAhead", BfiOffOn, BfiZeroOne, "0"),
                Choice(Loc.GetString("Extras.Bfi.Settings.PresenterBuffers.Label"), Loc.GetString("Extras.Bfi.Settings.PresenterBuffers.Help"),
                    "PresenterBuffers", new[] { "2", "3", "4" }, new[] { "2", "3", "4" }, "3"),
                Choice(Loc.GetString("Extras.Bfi.Settings.ShowOverlays.Label"), Loc.GetString("Extras.Bfi.Settings.ShowOverlays.Help"),
                    "ShowOverlays", BfiOffOn, BfiZeroOne, "0"));

        // ── GPU priority (the BFI Presenter Manager's GPU priority row) ─────────
        cog.AddSection("GPU priority");
        cog.AddCompactGrid(
            Choice("Presenter GPU priority", "How urgently the graphics card treats the flashes. Realtime (default) runs the presenter's small amount of GPU work ahead of the game's, so flashes stay on time however busy the GPU is; it needs the Windows right below, otherwise it falls back to High. Lower to High or Normal if the game freezes with BFI Presenter. Applies at the next game start."
                    + (vulkan ? " Vulkan games get realtime only when they leave the compute queue family free (BFIPresenterVK.log says so)." : ""),
                "PresenterQueuePriority", new[] { "Normal", "High", "Realtime" }, new[] { "0", "1", "2" }, "2"));
        var prio = await Task.Run(RealtimeGpuPriorityService.GetStatus);
        string PrioStatus(RealtimeGpuPriorityService.Status s) => s switch
        {
            { Granted: true, InToken: false } => "Allowed for your Windows account. Sign out of Windows and back in, then games get the realtime GPU queue.",
            { Granted: true } => "Allowed: games get the realtime GPU queue.",
            { Granted: false, InToken: true } => "Removed from your account. Games keep realtime until you sign out of Windows.",
            { Granted: false } => "Not allowed: games use the high-priority GPU queue unless they run as administrator. Allow it if BFI Presenter flashes or flickers in GPU-heavy moments.",
            _ => "Not allowed yet as far as RHI knows (Windows only shows this setting to administrators). If you allowed it with BFI Presenter's Manager, sign out and back in and this updates; otherwise Allow it here.",
        };
        var prioNote = cog.AddNote(PrioStatus(prio));
        bool prioGranted = prio.Granted == true;
        Button? prioBtn = null;
        prioBtn = cog.MakeButton(prioGranted ? "Remove from my account" : "Allow for my account", CogButtonKind.Secondary, async () =>
        {
            prioBtn!.IsEnabled = false;
            var (ok, err) = await RealtimeGpuPriorityService.SetAsync(grant: !prioGranted);
            var now = await Task.Run(RealtimeGpuPriorityService.GetStatus);
            prioGranted = now.Granted == true;
            prioNote.Text = PrioStatus(now);
            prioBtn.Content = prioGranted ? "Remove from my account" : "Allow for my account";
            prioBtn.IsEnabled = true;
            if (ok) cog.SetSuccess(prioGranted
                ? "Added. Sign out of Windows and back in, then start the game: its log says the realtime GPU priority privilege is enabled."
                : "Removed. It stays in effect until you sign out of Windows.");
            else cog.SetError(err ?? "Not changed.");
        }, "Gives your Windows account the right 'Increase scheduling priority' (one administrator prompt), as BFI Presenter's Manager does, "
         + "so games can run the presenter's GPU queue at realtime priority without being run as administrator. Windows grants rights to accounts, "
         + "not programs, so any program you run can then raise its own priority too. Remove takes it away again.");
        cog.AddButtons(prioBtn);

        // ── Vulkan layer ────────────────────────────────────────────────────────
        if (vulkan || BfiPresenterService.LayerFilesPresent)
        {
            cog.AddSection(Loc.GetString("Extras.Bfi.Settings.Section.VulkanLayer"));
            string LayerStatus()
            {
                if (!BfiPresenterService.IsLayerInstalled) return Loc.GetString("Extras.Bfi.Settings.Layer.NotInstalled");
                var s = Loc.GetString("Extras.Bfi.Settings.Layer.InstalledForUser") + (BfiPresenterService.IsLayerRegisteredForAllUsers ? Loc.GetString("Extras.Bfi.Settings.Layer.AndAllUsers") : ".");
                if (BfiPresenterService.LayerOrderWrong) s += Loc.GetString("Extras.Bfi.Settings.Layer.OrderWrong");
                if (!BfiPresenterService.IsLayerRegisteredForAllUsers)
                    s += Loc.GetString("Extras.Bfi.Settings.Layer.NeedsAllUsers");
                return s;
            }
            var layerNote = cog.AddNote(LayerStatus());
            var buttons = new List<Button>();
            if (BfiPresenterService.IsLayerInstalled)
            {
                bool all = BfiPresenterService.IsLayerRegisteredForAllUsers;
                buttons.Add(cog.MakeButton(all ? Loc.GetString("Extras.Bfi.Settings.Layer.RemoveAllUsers") : Loc.GetString("Extras.Bfi.Settings.Layer.RegisterAllUsers"), CogButtonKind.Secondary, async () =>
                {
                    bool ok = await svc.SetAllUsersAsync(remove: BfiPresenterService.IsLayerRegisteredForAllUsers);
                    layerNote.Text = LayerStatus();
                    if (ok) cog.SetSuccess(Loc.GetString("Extras.Bfi.Settings.Done"));
                    else cog.SetError(Loc.GetString("Extras.Bfi.Settings.Layer.NotChanged"));
                }, Loc.GetString("Extras.Bfi.Settings.Layer.AllUsers.Tooltip")));
                if (BfiPresenterService.LayerOrderWrong)
                    buttons.Add(cog.MakeButton(Loc.GetString("Extras.Bfi.Settings.Layer.FixOrder"), CogButtonKind.Secondary, async () =>
                    {
                        bool ok = await Task.Run(svc.FixLayerOrder);
                        if (!ok && BfiPresenterService.IsLayerRegisteredForAllUsers) ok = await svc.SetAllUsersAsync(remove: false);
                        layerNote.Text = LayerStatus();
                        if (ok) cog.SetSuccess(Loc.GetString("Extras.Bfi.Settings.Layer.OrderFixed"));
                        else cog.SetError(Loc.GetString("Extras.Bfi.Settings.Layer.OrderFixFailed"));
                    }));
                buttons.Add(cog.MakeButton(Loc.GetString("Extras.Bfi.Settings.Layer.Remove"), CogButtonKind.Danger, async () =>
                {
                    var err = await svc.UninstallLayerAsync();
                    layerNote.Text = LayerStatus();
                    if (err == null) cog.SetSuccess(Loc.GetString("Extras.Bfi.Settings.Layer.Removed"));
                    else cog.SetError(err);
                }, Loc.GetString("Extras.Bfi.Settings.Layer.Remove.Tooltip")));
            }
            if (buttons.Count > 0) cog.AddButtons(buttons.ToArray());
        }

        var openIni = cog.MakeOpenFileButton(Loc.GetString("Extras.Bfi.Settings.OpenIni"), iniPath != null && File.Exists(iniPath) ? iniPath : null);
        cog.AddButtons(openIni,
            cog.MakeButton(Loc.GetString("Extras.Bfi.Settings.SettingsGuide"), CogButtonKind.Secondary, async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(BfiPresenterService.SettingsGuideUrl)); }),
            cog.MakeButton(Loc.GetString("Extras.Bfi.Settings.TestedGamesButton"), CogButtonKind.Secondary, async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(BfiPresenterService.TestedGamesUrl)); }));

        await cog.ShowAsync();
    }

    /// <summary>
    /// The game's Driver Settings section reads the NVIDIA profile when it's built: rebuild it after BFI Presenter
    /// changed G-SYNC, VSync, the frame limit, power or Smooth Motion, or it keeps showing the old values.
    /// </summary>
    internal void RefreshDriverSettingsIfShown(GameCardViewModel card) =>
        _window.DispatcherQueue?.TryEnqueue(() =>
        {
            if (_currentDetailCard == card) BuildDriverProfileSection(card, card.GameName);
        });

    /// <summary>A problem with the game's setup for BFI, optionally with a fix (run off the UI thread).</summary>
    private sealed record BfiSetupIssue(string Text, Func<bool>? Fix = null, string? FixedText = null);

    /// <summary>The author's per-game requirements, checked against what RHI can read. Background thread (NVAPI).</summary>
    private List<BfiSetupIssue> CheckBfiSetup(GameCardViewModel card, string installPath)
    {
        var issues = new List<BfiSetupIssue>();
        var gn = card.GameName;
        var p = _dlssPresetService;
        if (p.IsSupported && !string.IsNullOrEmpty(installPath))
        {
            try
            {
                if (p.GetPerGameGSyncEnabled(gn, installPath))
                    issues.Add(new(Loc.GetString("Extras.Bfi.Settings.Issue.Gsync"),
                        () => p.SetPerGameGSyncEnabled(gn, installPath, false), Loc.GetString("Extras.Bfi.Settings.Issue.GsyncFixed")));
                var vs = p.GetVSyncMode(gn, installPath);
                if (vs is VSyncForceOff or VSyncFastSync)
                    issues.Add(new(Loc.GetString(vs == VSyncFastSync ? "Extras.Bfi.Settings.Issue.VsyncFastSync" : "Extras.Bfi.Settings.Issue.VsyncForceOff"),
                        () => p.SetVSyncMode(gn, installPath, VSyncForceOn), Loc.GetString("Extras.Bfi.Settings.Issue.VsyncFixed")));
                var own = p.GetPerGameFpsLimit(gn, installPath);
                uint limit = own ?? p.GetGlobalFpsLimit();
                if (limit > 0)
                    issues.Add(new(Loc.GetString(own == null ? "Extras.Bfi.Settings.Issue.FpsLimitGlobal" : "Extras.Bfi.Settings.Issue.FpsLimit", limit),
                        () => p.SetPerGameFpsLimit(gn, installPath, 0), Loc.GetString("Extras.Bfi.Settings.Issue.FpsLimitFixed")));
                if (p.GetSmoothMotionEnable(gn, installPath) == 1)
                    issues.Add(new(Loc.GetString("Extras.Bfi.Settings.Issue.SmoothMotion"),
                        () => p.SetSmoothMotionEnable(gn, installPath, 0), Loc.GetString("Extras.Bfi.Settings.Issue.SmoothMotionFixed")));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or TimeoutException)
            {
                CrashReporter.Log($"[CheckBfiSetup] NVIDIA profile read failed — {ex.Message}");
            }
        }
        if (card.IsUlInstalled)
            issues.Add(new(Loc.GetString("Extras.Bfi.Settings.Issue.ReLimiter")));
        if (card.IsDcInstalled)
            issues.Add(new(Loc.GetString("Extras.Bfi.Settings.Issue.DisplayCommander")));
        var encoreAs = _window.ViewModel.GetRtxEncoreInstalledAs(gn, card.Source ?? "");
        var encore = App.Services.GetRequiredService<RtxEncoreService>();
        if (!string.IsNullOrEmpty(installPath) && encore.IsInstalledIn(installPath, encoreAs))
        {
            var cfg = encore.ConfigPath(installPath, encoreAs!);
            var limit = RtxEncoreConfig.Get(cfg, "nativeMenuFeatures", "reflexFrameLimit");
            if (limit != null && limit != "0")
                issues.Add(new(Loc.GetString("Extras.Bfi.Settings.Issue.RtxEncoreFps", limit)));
            if (RtxEncoreConfig.Get(cfg, "smoothMotion", "smoothMotionEnabled") == "true")
                issues.Add(new(Loc.GetString("Extras.Bfi.Settings.Issue.RtxEncoreSmooth")));
        }
        if (card.IsOsInstalled && !string.IsNullOrEmpty(installPath))
        {
            // OptiScaler.ini [Framerate] FramerateLimit: auto / 0 = no limit
            var osLimit = Dlssg20_30Service.ReadIniValue(Path.Combine(installPath, "OptiScaler.ini"), "Framerate", "FramerateLimit");
            if (osLimit != null && !osLimit.Equals("auto", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(osLimit, NumberStyles.Float, CultureInfo.InvariantCulture, out var osFps) && osFps > 0)
                issues.Add(new(Loc.GetString("Extras.Bfi.Settings.Issue.OptiScaler", osLimit)));
        }
        return issues;
    }

    /// <summary>Number box for one BFIPresenter.ini key: saves on Enter and when focus leaves.</summary>
    private static TextBox BuildBfiNumberBox(CogDialog cog, string? iniPath, string gameName, string key, string label,
        double min, double max, string def)
    {
        var current = iniPath != null ? BfiPresenterService.GetIni(iniPath, key) : null;
        var box = CogDialog.MakeTextBox(current ?? def, def);
        box.IsEnabled = iniPath != null;
        string saved = box.Text;
        void Save()
        {
            if (iniPath == null) return;
            var text = box.Text.Trim();
            if (text == saved) return;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < min || v > max)
            {
                cog.SetError(Loc.GetString("Extras.Bfi.Settings.NumberRange", label, min.ToString(CultureInfo.InvariantCulture), max.ToString(CultureInfo.InvariantCulture)));
                box.Text = saved;
                return;
            }
            try
            {
                var value = v.ToString(CultureInfo.InvariantCulture);
                BfiPresenterService.SetIni(iniPath, key, value);
                saved = value;
                box.Text = value;
                CrashReporter.Log($"[BfiPresenterCog] '{gameName}': {key} = {value}");
                cog.SetSuccess(Loc.GetString("Extras.RtxEncore.Nr.Saved"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                cog.SetError(Loc.GetString("Extras.Bfi.Settings.SaveFailed", label, ex.Message));
                box.Text = saved;
            }
        }
        box.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { Save(); e.Handled = true; } };
        box.LostFocus += (_, _) => Save();
        return box;
    }
}
