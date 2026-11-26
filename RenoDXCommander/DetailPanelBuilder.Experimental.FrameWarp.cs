// DetailPanelBuilder.Experimental.FrameWarp.cs — Experimental › Reprojection row for FrameWarp / XPAR
// (xPoiler/Reshade-Asynchronous-Reprojection): asynchronous camera reprojection, so the camera moves at the display's
// refresh rate whatever the game renders at. Install / remove / Latewarp go through MainViewModel.FrameWarp.cs, files
// and staging through FrameWarpService. Installing warns first (Install anyway / Cancel) about frame limiters, the NVIDIA
// frame rate limit, GPU scheduling and the VC++ runtime, and can turn the NVIDIA limit off for the game.
// FrameWarp's own settings are in its XPAR panel in ReShade: it puts them back to its defaults after every update, so
// the ⚙ only has what RHI owns.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private void BuildFrameWarpRow(GameCardViewModel card, StackPanel body)
    {
        var vm          = _window.ViewModel;
        var installPath = card.InstallPath ?? "";
        bool isInstalled = FrameWarpService.IsInstalledIn(installPath);
        bool isOurs      = isInstalled && FrameWarpService.IsRhiInstalled(installPath);
        bool pulled      = FeatureFlags.IsExperimentalHidden(FeatureFlags.ExperimentalFrameWarp);
        var block = pulled
            ? (Loc.GetString("Extras.FrameWarp.NotOffered.Title"), Loc.GetString("Extras.FrameWarp.NotOffered.Body"))
            : isInstalled && isOurs ? null : vm.FrameWarpBlock(card);

        string statusText  = !isInstalled ? Loc.GetString("Status.Ready")
                           : !isOurs ? Loc.GetString("Extras.FrameWarp.Status.Manual")
                           : FrameWarpService.InstalledVersion(installPath) is { } v ? v : Loc.GetString("Status.Installed");
        string statusColor = !isInstalled ? "#A0AABB" : isOurs ? "#5ECB7D" : "#E8C46A";

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExperimentalContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        var label = new TextBlock
        {
            Text = "FrameWarp",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var tip = Loc.GetString("Extras.FrameWarp.Tooltip");
        if (card.IsUlInstalled || card.IsDcInstalled)
            tip += Loc.GetString("Extras.FrameWarp.Tooltip.LimiterInstalled", card.IsUlInstalled ? "ReLimiter" : "Display Commander");
        if (isInstalled && !isOurs) tip += Loc.GetString("Extras.FrameWarp.Tooltip.ManualInstall");
        ToolTipService.SetToolTip(label, tip);
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var statusBlock = new TextBlock
        {
            Text = statusText,
            FontSize = 12,
            Foreground = UIFactory.GetBrush(statusColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            TextDecorations = isOurs ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None,
        };
        if (isOurs)
        {
            ToolTipService.SetToolTip(statusBlock, Loc.GetString("Extras.FrameWarp.Releases.Tooltip"));
            statusBlock.PointerPressed += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(FrameWarpService.ReleasesUrl));
            AttachLinkCursor(statusBlock);
        }
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        var infoBtn = new Button
        {
            Content = Loc.GetString("Dialog.Info"), FontSize = 11, Padding = new Thickness(6, 2, 6, 2), Width = 36, Height = 32,
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
        };
        ToolTipService.SetToolTip(infoBtn, Loc.GetString("Extras.FrameWarp.Info.Tooltip"));
        infoBtn.Click += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(FrameWarpService.RepoUrl));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        bool btnEnabled = block == null;
        var installBtn = new Button
        {
            Content = !btnEnabled ? "⚠  " + block!.Value.Item1
                    : isOurs ? Loc.GetString("Extras.FrameWarp.Reinstall.Button") : Loc.GetString("Extras.FrameWarp.Install.Button"),
            FontSize = 12, Height = 32, HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(8),
            Background  = isOurs ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground  = isOurs ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isOurs ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            IsEnabled = btnEnabled,
            Opacity = btnEnabled ? 1.0 : 0.35,
        };
        if (!btnEnabled)
        {
            ToolTipService.SetToolTip(installBtn, block!.Value.Item2);
            ToolTipService.SetToolTip(label, $"{tip}\n\n{block.Value.Item2}");
        }
        else
            ToolTipService.SetToolTip(installBtn, Loc.GetString("Extras.FrameWarp.Install.Tooltip"));

        installBtn.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            try
            {
                if (card.IsRunning)
                {
                    ToolTipService.SetToolTip(installBtn, Loc.GetString("Extras.FrameWarp.GameRunning.Tooltip"));
                    return;
                }
                var xamlRoot = installBtn.XamlRoot ?? _window.Content.XamlRoot;
                installBtn.IsEnabled = false;
                var (lines, limit) = await Task.Run(() => vm.FrameWarpWarnings(card));
                if (lines.Count > 0)
                {
                    var (go, turnOff) = await ConfirmFrameWarpWarningsAsync(card, lines, limit, xamlRoot);
                    if (!go) { installBtn.IsEnabled = true; return; }
                    if (turnOff) await Task.Run(() => vm.TurnOffDriverFpsLimitForGame(card));
                }
                installBtn.Content = Loc.GetString("Status.Downloading");
                var error = await vm.InstallFrameWarpAsync(card);
                if (error != null)
                {
                    installBtn.Content = Loc.GetString("Extras.InstallFailed.Button");
                    installBtn.IsEnabled = true;
                    ToolTipService.SetToolTip(installBtn, error);
                    await DialogService.ShowSafeAsync(new ContentDialog
                    {
                        Title = Loc.GetString("Extras.FrameWarp.InstallFailed.Title"),
                        Content = new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap },
                        CloseButtonText = Loc.GetString("Dialog.Ok"),
                        XamlRoot = xamlRoot,
                        RequestedTheme = ElementTheme.Dark,
                    });
                    return;
                }
                RequestExperimentalRebuild(card);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or HttpRequestException or TaskCanceledException or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[BuildFrameWarpRow] Install failed — {ex.Message}");
                installBtn.Content = Loc.GetString("Extras.InstallFailed.Button");
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        var cogBtn = CogDialog.MakeCogButton(Loc.GetString("Extras.FrameWarp.Settings.Tooltip"));
        cogBtn.Click += async (_, _) =>
        {
            try { await ShowFrameWarpSettingsAsync(card); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[BuildFrameWarpRow] Settings dialog failed — {ex.Message}");
            }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        var removeBtn = new Button
        {
            Width = 36, Height = 32, Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Opacity = isOurs ? 1.0 : 0,
            IsHitTestVisible = isOurs,
        };
        ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.FrameWarp.Remove.Tooltip"));
        removeBtn.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            if (card.IsRunning)
            {
                ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.FrameWarp.GameRunning.Tooltip"));
                return;
            }
            try
            {
                removeBtn.IsEnabled = false;
                await vm.UninstallFrameWarpAsync(card);
                RequestExperimentalRebuild(card);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                CrashReporter.Log($"[BuildFrameWarpRow] Remove failed — {ex.Message}");
                removeBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    /// <summary>
    /// The warnings before installing (Install anyway / Cancel, Cancel default), with a tick to turn the NVIDIA frame rate
    /// limit off for this game when it's on (ticked by default). Returns (go ahead, turn the limit off).
    /// </summary>
    private async Task<(bool Go, bool TurnOffLimit)> ConfirmFrameWarpWarningsAsync(GameCardViewModel card, List<string> lines, uint limit, XamlRoot? xamlRoot)
    {
        if (xamlRoot == null) return (true, false);
        var content = new StackPanel { Spacing = 8, MaxWidth = 480 };
        foreach (var l in lines)
            content.Children.Add(new TextBlock { Text = l, TextWrapping = TextWrapping.Wrap });
        CheckBox? turnOff = null;
        if (limit > 0)
        {
            turnOff = new CheckBox { Content = Loc.GetString("Extras.FrameWarp.Warnings.TurnOffLimit"), IsChecked = true, Margin = new Thickness(0, 4, 0, 0) };
            content.Children.Add(turnOff);
        }
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Extras.FrameWarp.Warnings.Title"),
            Content = content,
            PrimaryButtonText = Loc.GetString("Extras.FrameWarp.Warnings.InstallAnyway"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        bool go = await DialogService.ShowSafeAsync(dialog) == ContentDialogResult.Primary;
        bool off = go && turnOff?.IsChecked == true;
        CrashReporter.Log($"[BuildFrameWarpRow] '{card.GameName}': {lines.Count} warning(s) — {(go ? "installing anyway" : "cancelled")}{(off ? ", NVIDIA limit off" : "")}");
        return (go, off);
    }

    internal async Task ShowFrameWarpSettingsAsync(GameCardViewModel card)
    {
        var vm = _window.ViewModel;
        var installPath = card.InstallPath ?? "";
        bool isInstalled = FrameWarpService.IsInstalledIn(installPath);
        bool isOurs = isInstalled && FrameWarpService.IsRhiInstalled(installPath);
        bool reopen = false;
        var cog = new CogDialog("FrameWarp", _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Extras.FrameWarp.Intro"));
        cog.AddWarning(Loc.GetString("Extras.FrameWarp.Warning.SinglePlayer"));
        cog.AddNote(Loc.GetString("Extras.FrameWarp.Note.Experimental"));

        // ── This game: what gets in FrameWarp's way ──────────────────────────
        cog.AddSection(Loc.GetString("Overlay.Game.Title"));
        var (lines, limit) = await Task.Run(() => vm.FrameWarpWarnings(card));
        if (lines.Count == 0) cog.AddNote(Loc.GetString("Extras.FrameWarp.NoWarnings.Note"));
        foreach (var l in lines) cog.AddWarning(l);
        if (limit > 0)
            cog.AddButtons(cog.MakeButton(Loc.GetString("Extras.FrameWarp.TurnOffNvidia.Button"), CogButtonKind.Secondary, async () =>
            {
                bool ok = await Task.Run(() => vm.TurnOffDriverFpsLimitForGame(card));
                if (ok) { cog.SetSuccess(Loc.GetString("Extras.FrameWarp.TurnOffNvidia.Success")); reopen = true; cog.Hide(); }
                else cog.SetError(Loc.GetString("Extras.FrameWarp.TurnOffNvidia.Error"));
            }, Loc.GetString("Extras.FrameWarp.TurnOffNvidia.Tooltip")));
        cog.AddNote(Loc.GetString("Extras.FrameWarp.Note.NvidiaBackgroundCap"));

        // ── Unreal Engine: Engine.ini and launch options ─────────────────────
        var engineIni = isOurs && FrameWarpService.ReadMarker(installPath).TryGetValue("engine_ini", out var ei) && ei.Length > 0 ? ei
                      : FrameWarpService.FindEngineIni(installPath);
        if (engineIni != null)
        {
            cog.AddSection(Loc.GetString("Dialog.Engine.Unreal"));
            cog.AddNote(isOurs
                ? Loc.GetString("Extras.FrameWarp.EngineIni.Added", FrameWarpService.EngineCvar, engineIni)
                : Loc.GetString("Extras.FrameWarp.EngineIni.Adds", FrameWarpService.EngineCvar, engineIni));
            bool hasSl = card.HasStreamline || (!string.IsNullOrEmpty(installPath) && await Task.Run(() => HsfService.FindGameStreamline(installPath)));
            if (hasSl)
            {
                cog.AddNote(Loc.GetString("Extras.FrameWarp.UeLaunchOptions.Note", FrameWarpService.UeLaunchOptions));
                cog.AddButtons(
                    cog.MakeButton(Loc.GetString("Extras.FrameWarp.LaunchArgs.Add.Button"), CogButtonKind.Secondary, () =>
                    {
                        var args = vm.AddFrameWarpLaunchOptions(card);
                        cog.SetSuccess(Loc.GetString("Extras.FrameWarp.LaunchArgs.Added", args));
                    }),
                    cog.MakeButton(Loc.GetString("Extras.FrameWarp.LaunchArgs.Copy.Button"), CogButtonKind.Secondary, () =>
                    {
                        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                        dp.SetText(FrameWarpService.UeLaunchOptions);
                        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                        cog.SetSuccess(Loc.GetString("Extras.FrameWarp.Copied"));
                    }));
            }
        }

        // ── NVIDIA Latewarp ──────────────────────────────────────────────────
        cog.AddSection("NVIDIA Latewarp");
        bool nvidia = GpuInfoService.Cached is { Count: > 0 };
        if (!isOurs)
            cog.AddNote(Loc.GetString("Extras.FrameWarp.Latewarp.NotInstalled"));
        else if (!nvidia)
            cog.AddNote(Loc.GetString("Extras.FrameWarp.Latewarp.NoNvidia"));
        else
        {
            bool lwOn = FrameWarpService.IsLatewarpInstalled(installPath);
            cog.AddNote(Loc.GetString("Extras.FrameWarp.Latewarp.Optional.Note"));
            ComboBox? lwCombo = null;
            bool lwReverting = false;
            lwCombo = cog.AddCombo("NVIDIA Latewarp", Loc.GetString("Extras.FrameWarp.Latewarp.Tooltip"),
                new[] { LocOpt.T("Off"), LocOpt.T("On") }, lwOn ? 1 : 0, idx =>
                {
                    if (lwReverting) return true;
                    if (card.IsRunning) { cog.SetError(Loc.GetString("Extras.FrameWarp.GameRunning.Tooltip")); return false; }
                    if (idx == 1) cog.SetSuccess(Loc.GetString("Extras.FrameWarp.Latewarp.Downloading"));
                    _ = Task.Run(async () =>
                    {
                        var err = await vm.SetFrameWarpLatewarpAsync(card, idx == 1);
                        _window.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (err == null)
                            {
                                cog.SetSuccess(idx == 1 ? Loc.GetString("Extras.FrameWarp.Latewarp.Installed") : Loc.GetString("Extras.FrameWarp.Latewarp.Removed"));
                                return;
                            }
                            // Put the dropdown back to what's really installed
                            lwReverting = true;
                            if (lwCombo != null) lwCombo.SelectedIndex = FrameWarpService.IsLatewarpInstalled(installPath) ? 1 : 0;
                            lwReverting = false;
                            cog.SetError(err);
                        });
                    });
                    return true;
                });
        }

        // ── Using it ─────────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.FrameWarp.Section.UsingIt"));
        cog.AddNote(Loc.GetString("Extras.FrameWarp.UsingIt.Note"));
        cog.AddNote(Loc.GetString("Extras.FrameWarp.SettingsPersist.Note"));
        cog.AddNote(Loc.GetString("Extras.FrameWarp.RefreshRate.Note"));

        cog.AddSection(Loc.GetString("Dialog.SectionFiles"));
        cog.AddButtons(
            cog.MakeButton(Loc.GetString("Extras.FrameWarp.OpenGitHub.Button"), CogButtonKind.Secondary,
                async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(FrameWarpService.RepoUrl)); }),
            cog.MakeButton(Loc.GetString("Extras.FrameWarp.OpenLogs.Button"), CogButtonKind.Secondary, () =>
            {
                var logs = FrameWarpService.LogsDir(installPath);
                if (!Directory.Exists(logs)) { cog.SetError(Loc.GetString("Extras.FrameWarp.NoLogs.Error")); return; }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{logs}\"") { UseShellExecute = true });
            }),
            cog.MakeOpenFileButton(Loc.GetString("Extras.FrameWarp.OpenReShadeIni.Button"), isInstalled ? Path.Combine(installPath, "ReShade.ini") : null));
        await cog.ShowAsync();
        if (reopen) await ShowFrameWarpSettingsAsync(card);
    }
}
