// DetailPanelBuilder.Extras.BfiPresenter.cs — Extras row for BFI Presenter (artzox/BFI-Presenter): black frame
// insertion clocked by the display. Per game only (never for every game: it flickers by design and needs a fixed
// refresh rate). The route follows the game: the DX add-on next to ReShade for Direct3D 12 / 11 / 9 and OpenGL,
// the Vulkan layer for Vulkan games and games run through DXVK. The install dialog offers the NVIDIA profile
// settings the author's setup asks for (as his Manager does). Settings: DetailPanelBuilder.Extras.BfiPresenterSettings.cs.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    /// <summary>The route a new install uses, or why it can't be installed (Route null).</summary>
    private (BfiRoute? Route, string? Blocked) BfiRouteFor(GameCardViewModel card)
    {
        if (card.IsDxvkInstalled)
        {
            // Vulkan layer through DXVK, but not for 32-bit games (the author: frames don't reach the screen one per
            // refresh there; use the 32-bit add-on for DX11 games instead)
            if (card.Is32Bit)
                return (null, Loc.GetString("Extras.Bfi.Blocked.Dxvk32Bit"));
            return (BfiRoute.Vulkan, null);
        }
        bool vulkan = card.RequiresVulkanInstall || card.GraphicsApi == GraphicsApiType.Vulkan;
        if (vulkan) return (BfiRoute.Vulkan, null);

        switch (card.GraphicsApi)
        {
            case GraphicsApiType.DirectX8:
                return (null, Loc.GetString("Extras.Bfi.Blocked.DirectX8"));
            case GraphicsApiType.DirectX10:
                return (null, Loc.GetString("Extras.Bfi.Blocked.DirectX10"));
        }
        if (!card.IsRsInstalled)
            return (null, Loc.GetString("Extras.Bfi.Blocked.NeedReShade"));
        if (card.UseNormalReShade)
            return (null, Loc.GetString("Extras.Bfi.Blocked.NoAddonsChannel"));
        if (card.GraphicsApi == GraphicsApiType.DirectX9
            && Version.TryParse(card.RsInstalledVersion ?? "", out var rs) && rs < new Version(6, 8))
            return (null, Loc.GetString("Extras.Bfi.Blocked.ReShadeTooOld", card.RsInstalledVersion ?? ""));
        if (card.GraphicsApi == GraphicsApiType.OpenGL && GpuInfoService.Cached is { Count: 0 })
            return (null, Loc.GetString("Extras.Bfi.Blocked.OpenGlNeedsNvidia"));
        return (BfiRoute.Dx, null);
    }

    private static string BfiRouteLabel(BfiRoute route, bool is32Bit) => route == BfiRoute.Vulkan
        ? Loc.GetString("Extras.Bfi.Route.VulkanLayer")
        : (is32Bit ? Loc.GetString("Extras.Bfi.Route.DxAddon32") : Loc.GetString("Extras.Bfi.Route.DxAddon"));

    private void BuildBfiPresenterRow(GameCardViewModel card, StackPanel body)
    {
        var vm          = _window.ViewModel;
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";
        var svc         = App.Services.GetRequiredService<BfiPresenterService>();

        var installedAs = vm.GetBfiPresenterInstalledAs(gameName, store);
        bool isInstalled = BfiPresenterService.IsInstalledIn(installPath, installedAs);
        var (route, blocked) = isInstalled ? (BfiPresenterService.RouteOf(installedAs!), null) : BfiRouteFor(card);

        string statusText  = isInstalled ? (svc.AnyStagedVersion is { } v ? "v" + v : Loc.GetString("Status.Installed")) : Loc.GetString("Status.Ready");
        string statusColor = isInstalled ? "#5ECB7D" : "#A0AABB";

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExtrasContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        var label = new TextBlock
        {
            Text = Loc.GetString("Extras.BfiPresenter"),
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var tip = Loc.GetString("Extras.Bfi.Tooltip");
        if (isInstalled) tip += Loc.GetString("Extras.Bfi.Tooltip.InstalledAs", BfiRouteLabel(route!.Value, card.Is32Bit));
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
            TextDecorations = isInstalled ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None,
        };
        if (isInstalled)
        {
            ToolTipService.SetToolTip(statusBlock, Loc.GetString("Extras.OpenGithubReleases.Tooltip"));
            statusBlock.PointerPressed += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(BfiPresenterService.ReleasesUrl));
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
        ToolTipService.SetToolTip(infoBtn, Loc.GetString("Extras.Bfi.OpenGithub.Tooltip"));
        infoBtn.Click += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(BfiPresenterService.RepoUrl));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        string btnLabel = isInstalled ? Loc.GetString("Extras.Bfi.Button.Reinstall") : Loc.GetString("Extras.Bfi.Button.Install");
        bool btnEnabled = isInstalled || route != null;
        var installBtn = new Button
        {
            Content = btnEnabled ? btnLabel : Loc.GetString("Extras.Bfi.Button.NotAvailable"),
            FontSize = 12, Height = 32, HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(8),
            Background  = isInstalled ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground  = isInstalled ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isInstalled ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            IsEnabled = btnEnabled,
            Opacity = btnEnabled ? 1.0 : 0.35,
        };
        if (!btnEnabled && blocked != null)
        {
            ToolTipService.SetToolTip(installBtn, blocked);
            // Disabled buttons show no tooltip: put the reason on the label too
            ToolTipService.SetToolTip(label, $"{tip}\n\n{blocked}");
        }
        else if (route != null)
            ToolTipService.SetToolTip(installBtn, Loc.GetString("Extras.Bfi.InstallRoute.Tooltip", BfiRouteLabel(route.Value, card.Is32Bit)));

        installBtn.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(installPath) || route == null) return;
            try
            {
                var xamlRoot = installBtn.XamlRoot ?? _window.Content.XamlRoot;
                var choice = await ShowBfiInstallDialogAsync(card, route.Value, isInstalled, xamlRoot);
                if (choice == null) return;
                installBtn.IsEnabled = false;
                installBtn.Content = Loc.GetString("Dialog.Downloading");
                var error = await InstallBfiPresenterAsync(card, route.Value, choice);
                if (error != null)
                {
                    installBtn.Content = Loc.GetString("Extras.InstallFailed.Button");
                    installBtn.IsEnabled = true;
                    ToolTipService.SetToolTip(installBtn, error);
                    await DialogService.ShowSafeAsync(new ContentDialog
                    {
                        Title = Loc.GetString("Extras.Bfi.NotInstalled.Title"),
                        Content = new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap },
                        CloseButtonText = Loc.GetString("Dialog.Ok"),
                        XamlRoot = xamlRoot,
                        RequestedTheme = ElementTheme.Dark,
                    });
                    return;
                }
                RequestExtrasRebuild(card);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or HttpRequestException or TaskCanceledException or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[BuildBfiPresenterRow] Install failed — {ex.Message}");
                installBtn.Content = Loc.GetString("Extras.InstallFailed.Button");
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        var cogBtn = CogDialog.MakeCogButton(Loc.GetString("Extras.Bfi.Cog.Tooltip"));
        cogBtn.Click += async (_, _) =>
        {
            try { await ShowBfiPresenterSettingsAsync(card); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[BuildBfiPresenterRow] Settings dialog failed — {ex.Message}");
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
            Opacity = isInstalled ? 1.0 : 0,
            IsHitTestVisible = isInstalled,
        };
        ToolTipService.SetToolTip(removeBtn, route == BfiRoute.Vulkan
            ? Loc.GetString("Extras.Bfi.Remove.Tooltip.Vulkan")
            : Loc.GetString("Extras.Bfi.Remove.Tooltip.Dx"));
        removeBtn.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(installPath) || string.IsNullOrEmpty(installedAs)) return;
            if (card.IsRunning)
            {
                ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.Bfi.Remove.Running"));
                return;
            }
            if (svc.Uninstall(installPath, installedAs))
            {
                vm.SetBfiPresenterInstalledAs(gameName, null, store);
                RequestExtrasRebuild(card);
            }
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    /// <summary>What the install dialog asked for.</summary>
    private sealed record BfiInstallChoice(
        bool GsyncOff, bool VsyncForceOn, bool FpsLimitOff, bool MaxPerformance, bool OpenGlLayered,
        bool FullBrightness, bool Lcd, bool AllUsers);

    /// <summary>
    /// Shown before every install: flicker and anti-cheat, the setup the author lists, and the NVIDIA profile settings
    /// RHI can set for the game (ticked by default, as his Manager offers them). Null = cancelled.
    /// </summary>
    private async Task<BfiInstallChoice?> ShowBfiInstallDialogAsync(GameCardViewModel card, BfiRoute route, bool reinstall, XamlRoot? xamlRoot)
    {
        xamlRoot ??= _window.Content.XamlRoot;
        if (xamlRoot == null) return null;
        bool nvidia = _dlssPresetService.IsSupported;
        bool openGl = card.GraphicsApi == GraphicsApiType.OpenGL;

        var content = new StackPanel { Spacing = 8, MaxWidth = 560 };
        void Para(string text, string brush = ResourceKeys.TextPrimaryBrush) => content.Children.Add(new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 20,
            Foreground = UIFactory.Brush(brush),
        });
        CheckBox Tick(string text, bool on, string tooltip)
        {
            var cb = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = on, FontSize = 12 };
            ToolTipService.SetToolTip(cb, tooltip);
            content.Children.Add(cb);
            return cb;
        }

        Para(Loc.GetString("Extras.Bfi.InstallDialog.Flicker"),
            ResourceKeys.AccentAmberBrush);
        Para(Loc.GetString("Extras.Bfi.InstallDialog.AntiCheat"));
        Para(route == BfiRoute.Vulkan
            ? Loc.GetString("Extras.Bfi.InstallDialog.RouteVulkan")
              + (card.IsDxvkInstalled ? Loc.GetString("Extras.Bfi.InstallDialog.DxvkNote") : "")
            : Loc.GetString("Extras.Bfi.InstallDialog.RouteDx", card.Is32Bit ? BfiPresenterService.Addon32 : BfiPresenterService.Addon64)
              + (card.GraphicsApi is GraphicsApiType.DirectX11 or GraphicsApiType.DirectX9 ? Loc.GetString("Extras.Bfi.InstallDialog.BorderlessNote") : ""));
        Para(Loc.GetString("Extras.Bfi.InstallDialog.Required"),
            ResourceKeys.TextSecondaryBrush);

        CheckBox? gsync = null, vsync = null, fps = null, power = null, ogl = null;
        if (nvidia)
        {
            content.Children.Add(new TextBlock { Text = Loc.GetString("Extras.Bfi.InstallDialog.NvidiaHeading"), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
            gsync = Tick(Loc.GetString("Extras.Bfi.InstallDialog.GsyncOff"), true, Loc.GetString("Extras.Bfi.InstallDialog.GsyncOff.Tooltip"));
            vsync = Tick(Loc.GetString("Extras.Bfi.InstallDialog.VsyncForceOn"), true, Loc.GetString("Extras.Bfi.InstallDialog.VsyncForceOn.Tooltip"));
            fps   = Tick(Loc.GetString("Extras.Bfi.InstallDialog.FpsLimitOff"), true, Loc.GetString("Extras.Bfi.InstallDialog.FpsLimitOff.Tooltip"));
            power = Tick(Loc.GetString("Extras.Bfi.InstallDialog.PowerMax"), true, Loc.GetString("Extras.Bfi.InstallDialog.PowerMax.Tooltip"));
            if (openGl)
                ogl = Tick(Loc.GetString("Extras.Bfi.InstallDialog.PresentMethod"), true, Loc.GetString("Extras.Bfi.InstallDialog.PresentMethod.Tooltip"));
        }

        content.Children.Add(new TextBlock { Text = Loc.GetString("Extras.Bfi.InstallDialog.SettingsHeading"), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        var bright = Tick(Loc.GetString("Extras.Bfi.InstallDialog.FullBrightness"), true,
            Loc.GetString("Extras.Bfi.InstallDialog.FullBrightness.Tooltip"));
        var lcd = Tick(Loc.GetString("Extras.Bfi.InstallDialog.Lcd"), false,
            Loc.GetString("Extras.Bfi.InstallDialog.Lcd.Tooltip"));
        CheckBox? allUsers = null;
        if (route == BfiRoute.Vulkan && !VulkanLayerService.IsRunningAsAdmin() && !BfiPresenterService.IsLayerRegisteredForAllUsers)
            allUsers = Tick(Loc.GetString("Extras.Bfi.InstallDialog.AllUsers"), false,
                Loc.GetString("Extras.Bfi.InstallDialog.AllUsers.Tooltip"));

        var dialog = new ContentDialog
        {
            Title = reinstall ? Loc.GetString("Extras.Bfi.Dialog.Reinstall") : Loc.GetString("Extras.Bfi.Dialog.Install"),
            Content = new ScrollViewer { Content = content, MaxHeight = 560 },
            PrimaryButtonText = reinstall ? Loc.GetString("NeuralRendering.Button.Reinstall") : Loc.GetString("Dialog.Install"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 640.0;
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return null;
        return new BfiInstallChoice(
            gsync?.IsChecked == true, vsync?.IsChecked == true, fps?.IsChecked == true, power?.IsChecked == true,
            ogl?.IsChecked == true, bright.IsChecked == true, lcd.IsChecked == true, allUsers?.IsChecked == true);
    }

    /// <summary>Downloads, installs and sets up BFI Presenter in the game. Returns an error message, or null.</summary>
    private async Task<string?> InstallBfiPresenterAsync(GameCardViewModel card, BfiRoute route, BfiInstallChoice choice)
    {
        var vm = _window.ViewModel;
        var svc = App.Services.GetRequiredService<BfiPresenterService>();
        var gameName = card.GameName;
        var store = card.Source ?? "";
        var installPath = card.InstallPath!;
        bool is32 = card.Is32Bit;
        var existing = vm.GetBfiPresenterInstalledAs(gameName, store);

        // The tested-games list: games that never resize their swap chain get PresenterResize (DX), as the Manager does
        var tested = BfiPresenterService.FindTested(await svc.GetTestedGamesAsync(), gameName);
        int? presenterResize = tested?.PresenterResize is int r && !(tested.PresenterResizeOnlyWithoutRenoDx && card.IsRdxInstalled) ? r : null;

        var result = await Task.Run(async () =>
        {
            await svc.CheckForUpdateAsync(forceRefresh: true).ConfigureAwait(false);
            if (!await svc.EnsureStagingAsync(route).ConfigureAwait(false))
                return (Rel: (string?)null, Error: (string?)Loc.GetString("Extras.Bfi.Error.Download"));

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (choice.FullBrightness) values["Gain"] = "1.0";
            if (choice.Lcd) values["LcdPhaseSwap"] = "1";

            string? rel;
            if (route == BfiRoute.Dx)
            {
                // A Vulkan-route install in the same game would leave its ini switching the layer on: remove it
                if (existing != null && BfiPresenterService.RouteOf(existing) == BfiRoute.Vulkan) svc.Uninstall(installPath, existing);
                rel = svc.InstallDx(installPath, is32);
                if (rel == null) return (null, Loc.GetString("Extras.Bfi.Error.CopyAddon"));
                var ini = BfiPresenterService.IniPathOf(installPath, rel)!;
                bool hasIni = File.Exists(ini);
                // Keep the user's own settings on a reinstall: only fill in what isn't there yet
                foreach (var kv in values)
                    if (!hasIni || BfiPresenterService.GetIni(ini, kv.Key) == null) BfiPresenterService.SetIni(ini, kv.Key, kv.Value);
                if (presenterResize is int pr && BfiPresenterService.GetIni(ini, "PresenterResize") is null or "0")
                    BfiPresenterService.SetIni(ini, "PresenterResize", pr.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                if (existing != null && BfiPresenterService.RouteOf(existing) == BfiRoute.Dx)
                    svc.Uninstall(installPath, existing, keepSettings: true);
                var layerError = svc.InstallOrUpdateLayer();
                if (layerError != null) return (null, layerError);
                var exe = App.Services.GetRequiredService<IPeHeaderService>().FindGameExe(installPath);
                var exeDir = exe != null ? Path.GetDirectoryName(exe)! : installPath;
                bool newIni = !File.Exists(Path.Combine(exeDir, BfiPresenterService.IniFileName));
                rel = svc.InstallVulkan(installPath, exeDir, newIni ? values : null);
                if (rel == null) return (null, Loc.GetString("Extras.Bfi.Error.WriteIni", exeDir));
            }
            return (rel, null);
        });
        if (result.Error != null) return result.Error;

        vm.SetBfiPresenterInstalledAs(gameName, result.Rel, store);
        if (route == BfiRoute.Vulkan && choice.AllUsers && !await svc.SetAllUsersAsync(remove: false))
            CrashReporter.Log("[InstallBfiPresenter] Not registered for all users (declined or failed)");

        // NVIDIA profile, off the UI thread (NVAPI)
        if (_dlssPresetService.IsSupported && (choice.GsyncOff || choice.VsyncForceOn || choice.FpsLimitOff || choice.MaxPerformance || choice.OpenGlLayered))
        {
            await Task.Run(() =>
            {
                var p = _dlssPresetService;
                if (choice.GsyncOff && !p.SetPerGameGSyncEnabled(gameName, installPath, false)) CrashReporter.Log("[InstallBfiPresenter] G-SYNC off not set");
                if (choice.VsyncForceOn && !p.SetVSyncMode(gameName, installPath, VSyncForceOn)) CrashReporter.Log("[InstallBfiPresenter] VSync not set");
                if (choice.FpsLimitOff && !p.SetPerGameFpsLimit(gameName, installPath, 0)) CrashReporter.Log("[InstallBfiPresenter] FPS limit not set");
                if (choice.MaxPerformance && !p.SetPowerManagementMode(gameName, installPath, PowerMaxPerformance)) CrashReporter.Log("[InstallBfiPresenter] Power mode not set");
                if (choice.OpenGlLayered && !p.SetVulkanPresentMethod(gameName, installPath, PresentLayeredOnDxgi)) CrashReporter.Log("[InstallBfiPresenter] Present method not set");
            });
            RefreshDriverSettingsIfShown(card);
        }
        CrashReporter.Log($"[InstallBfiPresenter] '{gameName}': {route} → '{result.Rel}' (tested: {tested?.Status ?? "not listed"})");
        return null;
    }

    private const uint VSyncForceOn        = 0x47814940;
    private const uint VSyncForceOff       = 0x08416747;
    private const uint VSyncFastSync       = 0x18888888;
    private const uint PowerMaxPerformance = 0x00000001;
    private const uint PresentLayeredOnDxgi = 0x00000001;
}
