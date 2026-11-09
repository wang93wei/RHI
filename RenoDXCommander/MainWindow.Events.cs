// MainWindow.Events.cs — Button click handlers and user-initiated event handlers.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    // ── Header buttons ────────────────────────────────────────────────────────────

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _crashReporter.Log("[MainWindow.RefreshButton_Click] User clicked Refresh");
        _ = RefreshWithScrollRestore();
    }

    private async void FullRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _crashReporter.Log("[MainWindow.FullRefreshButton_Click] User clicked Full Refresh");

            var dialog = new ContentDialog
            {
                Title = Loc.GetString("Xaml.FullRefresh"),
                Content = Loc.GetString("Dialog.FullRefresh.Content"),
                PrimaryButtonText = Loc.GetString("Dialog.Continue"),
                CloseButtonText = Loc.GetString("Dialog.Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
                RequestedTheme  = ElementTheme.Dark,
            };

            var result = await DialogService.ShowSafeAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            _ = FullRefreshWithScrollRestore();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.FullRefreshButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _crashReporter.Log("[MainWindow.CheckForUpdatesButton_Click] User clicked Check For Updates");

            // Show progress dialog
            var progressPanel = new StackPanel { Spacing = 8 };
            var progressRow = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 12 };
            var progressRing = new ProgressRing { IsActive = true, Width = 20, Height = 20 };
            var progressText = new TextBlock { Text = Loc.GetString("Dialog.FetchingManifest"), FontSize = 13, Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush) };
            progressRow.Children.Add(progressRing);
            progressRow.Children.Add(progressText);
            progressPanel.Children.Add(progressRow);

            var progressDialog = new ContentDialog
            {
                Title = Loc.GetString("Dialog.CheckingForUpdates"),
                Content = progressPanel,
                XamlRoot = Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };

            await using var progressSession = await DialogService.ShowProgressAsync(progressDialog, 5);
            if (progressSession == null) return;

            try
            {
                // Force bypass cooldown for the update check
                ViewModel.ForceNextUpdateCheck();

                // Trigger a Refresh (which fetches manifests + wiki + runs update checks)
                // A Refresh waits for a scan that's already running (e.g. the startup scan): say so
                // instead of showing "Checking components..." with nothing happening.
                if (ViewModel.IsLibraryOperationRunning)
                {
                    progressText.Text = Loc.GetString("Dialog.WaitingForScan");
                    _crashReporter.Log("[MainWindow.CheckForUpdatesButton_Click] A scan is running — waiting for it to finish");
                }
                else progressText.Text = Loc.GetString("Dialog.CheckingComponents");
                await ViewModel.RefreshAsync(() =>
                    DispatcherQueue?.TryEnqueue(() => progressText.Text = Loc.GetString("Dialog.CheckingComponents")));

                // A manual check restarts the 4-hour background countdown (shown on the Settings page)
                ViewModel.RestartPeriodicUpdateCheckTimer();

                // Trigger silent auto-install of any updates found
                ViewModel.TriggerAutoUpdate();

                // Check app update
                DispatcherQueue?.TryEnqueue(() => progressText.Text = Loc.GetString("Dialog.CheckingAppVersion"));
                // The update prompt needs the same modal slot; close progress first.
                await progressSession.DisposeAsync();
                await _dialogService.CheckForAppUpdateAsync();
            }
            catch (Exception ex)
            {
                _crashReporter.Log($"[CheckForUpdatesButton_Click] Error: {ex.Message}");
            }
            finally
            {
                await progressSession.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.CheckForUpdatesButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void BrowseAddonWatchFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderPath = await PickFolderAsync();
            if (!string.IsNullOrEmpty(folderPath))
            {
                AddonWatchFolderBox.Text = folderPath;
                ViewModel.Settings.AddonWatchFolder = folderPath;
                _addonFileWatcher.SetWatchPath(folderPath);
                ViewModel.SaveSettingsPublic();
                _crashReporter.Log($"[MainWindow] Addon watch folder set to: {folderPath}");
            }
        }
        catch (Exception ex) { _crashReporter.Log($"[MainWindow.BrowseAddonWatchFolder] {ex.Message}"); }
    }

    private void ResetAddonWatchFolder_Click(object sender, RoutedEventArgs e)
    {
        AddonWatchFolderBox.Text = "";
        ViewModel.Settings.AddonWatchFolder = "";
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        _addonFileWatcher.SetWatchPath(defaultPath);
        ViewModel.SaveSettingsPublic();
        _crashReporter.Log("[MainWindow] Addon watch folder reset to default Downloads");
    }

    private async void LumaCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;

            var cog = new CogDialog("Luma", this.Content.XamlRoot);

            // ── Engine.ini Settings ───────────────────────────────────────────────
            // Only for Unreal Engine games that have a Luma mod
            if (card.LumaMod == null)
            {
                cog.AddEmpty(Loc.GetString("Dialog.Luma.NoMod"));
            }
            else if (card.EngineHint?.Contains("Unreal") != true)
            {
                cog.AddEmpty(Loc.GetString("Dialog.Luma.NotUnreal"));
            }
            else
            {
                cog.AddSection(Loc.GetString("Dialog.Luma.UnrealEngineSection"));

                // ── HDR on First Boot ──────────────────────────────────────────────
                var currentHdr = AuxInstallService.GetLumaReshadeIniValue(card.InstallPath, "EnableHDR");
                // Default = key absent or never set by RHI; Off = "0"; On = "1"
                cog.AddCombo(Loc.GetString("Dialog.HdrOnFirstBoot"),
                    Loc.GetString("Dialog.Luma.HdrTooltip"),
                    new[] { LocOpt.T("Default"), LocOpt.T("Off"), LocOpt.T("On") },
                    currentHdr == null ? 0 : currentHdr == "0" ? 1 : 2,
                    i =>
                    {
                        if (i == 0) // Default — remove keys, let Luma manage them
                        {
                            AuxInstallService.RemoveLumaReshadeIniValue(card.InstallPath, "EnableHDR");
                            AuxInstallService.RemoveLumaReshadeIniValue(card.InstallPath, "DisplayMode");
                            cog.SetSuccess(Loc.GetString("Status.Luma.HdrResetOk"));
                        }
                        else
                        {
                            bool hdrOn = i == 2;
                            AuxInstallService.SetLumaReshadeIniValue(card.InstallPath, "EnableHDR", hdrOn ? "1" : "0");
                            AuxInstallService.SetLumaReshadeIniValue(card.InstallPath, "DisplayMode", hdrOn ? "1" : "0");
                            cog.SetSuccess(hdrOn ? Loc.GetString("Status.Luma.HdrEnabled") : Loc.GetString("Status.Luma.HdrDisabled"));
                        }
                        CrashReporter.Log($"[MainWindow.LumaCogButton_Click] HDR on First Boot = {i} for '{card.GameName}'");
                    });

                // ── TAA Settings ───────────────────────────────────────────────────
                cog.AddCombo(Loc.GetString("Dialog.TaaSettings"),
                    Loc.GetString("Dialog.Luma.TaaTooltip"),
                    new[] { LocOpt.T("Off"), LocOpt.T("On") },
                    ViewModel.IsLumaTaaEnabled(card.GameName) ? 1 : 0,
                    i =>
                    {
                        var taaKeys = new (string Section, string Key, string Value)[]
                        {
                            ("SystemSettings", "r.DefaultFeature.AntiAliasing", "2"),
                            ("SystemSettings", "r.PostProcessAAQuality", "4"),
                        };
                        if (i == 1)
                        {
                            AuxInstallService.ApplyEngineIniCustomKeys(
                                card.InstallPath, taaKeys, card.EngineIniProjectOverride, card.GameName, card.Source);
                            ViewModel.SetLumaTaaEnabled(card.GameName, true);
                            cog.SetSuccess(Loc.GetString("Status.Luma.TaaWritten"));
                        }
                        else
                        {
                            AuxInstallService.RemoveEngineIniCustomKeys(
                                card.InstallPath,
                                taaKeys.Select(k => k.Key),
                                card.EngineIniProjectOverride, card.GameName, card.Source);
                            ViewModel.SetLumaTaaEnabled(card.GameName, false);
                            cog.SetSuccess(Loc.GetString("Status.Luma.TaaRemoved"));
                        }
                        CrashReporter.Log($"[MainWindow.LumaCogButton_Click] TAA Settings = {(i == 1 ? "On" : "Off")} for '{card.GameName}'");
                    });
            }

            await cog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.LumaCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void SupportDiscord_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://discordapp.com/channels/1296187754979528747/1475173660686815374"));
    }

    // ── Links menu handlers ───────────────────────────────────────────────────

    private void LinkRenoDxWiki_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/clshortfuse/renodx/wiki/Mods"));
    }

    private void LinkLumaWiki_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/Filoppi/Luma-Framework/wiki"));
    }

    private void LinkRhiGithub_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/RankFTW/RHI"));
    }

    private void LinkReLimiterGithub_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/RankFTW/ReLimiter"));
    }

    // ── Donate dialog ─────────────────────────────────────────────────────────

    /// <summary>Button text for a donation link: Ko-fi, Patreon, or Donate for any other page.</summary>
    private string DonationLinkLabel(string url) =>
        url.Contains("ko-fi.com", StringComparison.OrdinalIgnoreCase) ? "Ko-fi"
        : url.Contains("patreon.com", StringComparison.OrdinalIgnoreCase) ? "Patreon"
        : Loc.GetString("Xaml.Donate");

    private async void DonateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var donationUrls = GameCardViewModel.GetAllDonationUrls();
            var patreonUrls = GameCardViewModel.GetAllPatreonUrls();
            // Always prefer live manifest roles (most up to date after a fetch).
            // Fall back to the merged static dict if manifest hasn't loaded yet.
            var manifestRoles = ViewModel.Manifest?.AuthorRoles;
            var roles = (manifestRoles != null && manifestRoles.Count > 0)
                ? (IReadOnlyDictionary<string, string>)manifestRoles
                : GameCardViewModel.AuthorRoles;

            var entries = donationUrls.Keys.Concat(patreonUrls.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (entries.Count == 0)
            {
                await DialogService.ShowSafeAsync(new ContentDialog
                {
                    Title = Loc.GetString("Donate.Empty.Title"),
                    Content = Loc.GetString("Donate.Empty.Content"),
                    CloseButtonText = Loc.GetString("Dialog.Close"),
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = ElementTheme.Dark,
                });
                return;
            }

            // ── Build the dialog content ──────────────────────────────────────────
            var panel = new StackPanel { Spacing = 14, Width = 680 };

            // Early-access notice: amber callout
            var warnText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = TabsTheme.B("#E8D3A0"),
            };
            warnText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text = Loc.GetString("Donate.EarlyAccess"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = TabsTheme.B(TabsTheme.AmberPill.Fg),
            });
            warnText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text = Loc.GetString("Donate.LinkFirst"),
            });
            panel.Children.Add(new Border
            {
                Background = TabsTheme.B(TabsTheme.AmberPill.Bg),
                BorderBrush = TabsTheme.B(TabsTheme.AmberPill.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Child = warnText,
            });

            // One small card per author, two across
            var grid = new Controls.AutoFitGrid { MinColumnWidth = 300, MaxColumns = 2, ColumnSpacing = 10, RowSpacing = 10 };

            foreach (var name in entries)
            {
                // Ko-fi (donationUrls; may hold a Patreon link for older entries) and Patreon (patreonUrls)
                var links = new List<(string Label, string Url)>();
                if (donationUrls.TryGetValue(name, out var url) && !string.IsNullOrWhiteSpace(url))
                    links.Add((DonationLinkLabel(url), url));
                if (patreonUrls.TryGetValue(name, out var patreonUrl) && !string.IsNullOrWhiteSpace(patreonUrl)
                    && !links.Any(l => l.Label == "Patreon"))
                    links.Add(("Patreon", patreonUrl));
                if (links.Count == 0) continue;
                roles.TryGetValue(name, out var role);

                // [Name / role] [Ko-fi] [Patreon]
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock
                {
                    Text = name,
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = TabsTheme.B(TabsTheme.TextPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                if (!string.IsNullOrEmpty(role))
                {
                    var roleBlock = new TextBlock
                    {
                        Text = role,
                        FontSize = 11,
                        Foreground = TabsTheme.B(TabsTheme.TextMuted),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                    ToolTipService.SetToolTip(roleBlock, role);
                    text.Children.Add(roleBlock);
                }
                row.Children.Add(text);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
                foreach (var (label, linkUrl) in links)
                {
                    if (!Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri)) continue;
                    var linkBtn = new Button { Content = label, VerticalAlignment = VerticalAlignment.Center };
                    TabsTheme.Apply(linkBtn, new TabsTheme.Look("#2A1418", "#5A2A30", "#FF8A88", "#351A1F", true), height: 32, fontSize: 12);
                    linkBtn.Padding = new Thickness(12, 0, 12, 0);
                    linkBtn.Click += (s, ev) => _ = Windows.System.Launcher.LaunchUriAsync(uri);
                    ToolTipService.SetToolTip(linkBtn, linkUrl);
                    buttons.Children.Add(linkBtn);
                }
                Grid.SetColumn(buttons, 1);
                row.Children.Add(buttons);

                var card = TabsTheme.MakeCard(row, 0);
                card.Padding = new Thickness(12, 10, 10, 10);
                grid.Children.Add(card);
            }
            panel.Children.Add(grid);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel,
                MaxHeight = 540,
                Padding = new Thickness(0, 0, 16, 0),
            };

            var dialog = new ContentDialog
            {
                Title = CogDialog.MakeDialogTitle(
                    Loc.GetString("Donate.Title"),
                    Loc.GetString("Donate.Intro")),
                Content = scroll,
                CloseButtonText = Loc.GetString("Dialog.Close"),
                XamlRoot = Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            CogDialog.ApplyDialogLook(dialog, 780);

            await DialogService.ShowSafeAsync(dialog);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.DonateButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }


    // ── Per-component install flyout click handlers ──

    internal async void CardComponentInstall_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button btn || btn.Tag is not GameCardViewModel card) return;
            var component = btn.DataContext as string;

            // Ensure install path exists (same pattern as CardInstallButton_Click)
            if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
            {
                var folder = await PickFolderAsync();
                if (folder == null) return;
                card.InstallPath = folder;
                ViewModel.SaveLibraryPublic();
            }

            switch (component)
            {
                case "RDX":
                    await ViewModel.InstallModCommand.ExecuteAsync(card);
                    break;
                case "RS":
                    await ViewModel.InstallReShadeCommand.ExecuteAsync(card);
                    break;
                case "Luma":
                    await ViewModel.InstallLumaAsync(card);
                    break;
                case "UL":
                    await ViewModel.InstallUlAsync(card);
                    break;
                case "DC":
                    await ViewModel.InstallDcAsync(card);
                    break;
                case "REF":
                    await ViewModel.InstallREFrameworkCommand.ExecuteAsync(card);
                    break;
                case "OS":
                    _installEventHandler.InstallOsButton_Click(sender, e);
                    break;
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.CardComponentInstall_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    internal async void CardCopyRsIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            if (string.IsNullOrEmpty(card.InstallPath)) return;
            try
            {
                var screenshotPath = BuildScreenshotSavePath(card.GameName);
                var overlayHotkey = ViewModel.Settings.OverlayHotkey;
                var screenshotHotkey = ViewModel.Settings.ScreenshotHotkey;
                var installPath = card.InstallPath;
                var gameName = card.GameName;
                var requiresVulkan = card.RequiresVulkanInstall;
                var useUeExtended = card.UseUeExtended;
                var status = card.Status;

                await Task.Run(() =>
                {
                    if (requiresVulkan)
                    {
                        AuxInstallService.MergeRsVulkanIni(installPath, gameName, screenshotPath, overlayHotkey, screenshotHotkey);
                        VulkanFootprintService.Create(installPath);
                    }
                    else
                        AuxInstallService.MergeRsIni(installPath, screenshotPath, overlayHotkey, screenshotHotkey);

                    // Apply [renodx] section if UE-Extended is installed
                    if (useUeExtended && status == GameStatus.Installed)
                        AuxInstallService.ApplyRenoDxNativeHdrSettings(installPath);
                });

                // Deploy shaders for Vulkan games (no DLL install, so shaders go with INI) — on UI thread
                if (requiresVulkan)
                    ViewModel.DeployShadersForCard(gameName);

                card.RsActionMessage = Loc.GetString("Status.ReshadeIniMerged");
            }
            catch (Exception ex)
            {
                card.RsActionMessage = Loc.GetString("Status.FailedShort", ex.Message);
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.CardCopyRsIni_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    internal async void CardCopyUlIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            if (string.IsNullOrEmpty(card.InstallPath)) return;
            try
            {
                var installPath = card.InstallPath;
                await Task.Run(() => AuxInstallService.CopyUlIni(installPath));
                card.UlActionMessage = Loc.GetString("Status.RelimiterIniCopied");
            }
            catch (Exception ex)
            {
                card.UlActionMessage = Loc.GetString("Status.FailedShort", ex.Message);
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.CardCopyUlIni_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    internal async void CardCopyDcIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            if (string.IsNullOrEmpty(card.InstallPath)) return;
            try
            {
                var installPath = card.InstallPath;
                await Task.Run(() => AuxInstallService.CopyDcIni(installPath));
                card.DcActionMessage = Loc.GetString("Status.DisplayCommanderIniCopied");
                card.FadeMessage(m => card.DcActionMessage = m, card.DcActionMessage);
            }
            catch (Exception ex)
            {
                card.DcActionMessage = Loc.GetString("Status.FailedShort", ex.Message);
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.CardCopyDcIni_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    internal async void CardCopyOsIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            if (string.IsNullOrEmpty(card.InstallPath)) return;
            try
            {
                var sourceIni = Services.OptiScalerService.OsIniPath;
                if (!File.Exists(sourceIni))
                {
                    card.OsActionMessage = Loc.GetString("Status.OptiScalerIniMissing");
                    return;
                }
                var installPath = card.InstallPath;
                var destIni = Path.Combine(installPath, Services.OptiScalerService.IniFileName);
                await Task.Run(() =>
                {
                    File.Copy(sourceIni, destIni, overwrite: true);
                    Services.OptiScalerService.EnforceLoadReshade(destIni);
                });
                card.OsActionMessage = Loc.GetString("Status.OptiScalerIniCopied");
                card.FadeMessage(m => card.OsActionMessage = m, card.OsActionMessage);
            }
            catch (Exception ex)
            {
                card.OsActionMessage = Loc.GetString("Status.FailedShort", ex.Message);
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.CardCopyOsIni_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ── Card action button handlers ───────────────────────────────────────────────

    internal async void CardInstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as FrameworkElement)?.Tag is not GameCardViewModel card) return;

            // Ensure install path exists
            if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
            {
                var folder = await PickFolderAsync();
                if (folder == null) return;
                card.InstallPath = folder;
                ViewModel.SaveLibraryPublic();
            }
            // Chain: RenoDX → RE Framework → ReShade (skip components that are N/A)
            if (card.Mod?.SnapshotUrl != null)
                await ViewModel.InstallModCommand.ExecuteAsync(card);
            if (card.RefRowVisibility == Visibility.Visible)
                await ViewModel.InstallREFrameworkCommand.ExecuteAsync(card);
            if (card.ReShadeRowVisibility == Visibility.Visible)
                await ViewModel.InstallReShadeCommand.ExecuteAsync(card);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.CardInstallButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void CardOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null || string.IsNullOrEmpty(card.InstallPath)) return;
        if (System.IO.Directory.Exists(card.InstallPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(card.InstallPath) { UseShellExecute = true });
    }

    internal void GameRow_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is GameCardViewModel)
            CardMoreMenu_Click(fe, e);
    }

    internal void CardMoreMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement anchor || anchor.Tag is not GameCardViewModel card)
            return;

        // Don't change ViewModel.SelectedGame here: every item below acts on `card` directly,
        // and changing it left the detail panel showing one game while Launch used another.
        var menu = new MenuFlyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight,
        };

        // ── Open Folder ──
        var openFolderItem = new MenuFlyoutItem
        {
            Text = Loc.GetString("CardMenu.OpenFolder"),
            Tag = card,
        };
        openFolderItem.Click += CardOpenFolder_Click;
        menu.Items.Add(openFolderItem);

        // ── Favourite ──
        var favItem = new MenuFlyoutItem
        {
            Text = Loc.GetString(card.IsFavourite ? "CardMenu.Unfavourite" : "CardMenu.Favourite"),
            Tag  = card,
        };
        favItem.Click += (s, ev) => ViewModel.ToggleFavouriteCommand.Execute(card);
        menu.Items.Add(favItem);

        // ── Hide / Show ──
        var hideItem = new MenuFlyoutItem
        {
            Text = Loc.GetString(card.IsHidden ? "CardMenu.Show" : "CardMenu.Hide"),
            Tag = card,
        };
        hideItem.Click += (s, ev) => ViewModel.ToggleHideGameCommand.Execute(card);
        menu.Items.Add(hideItem);

        // ── Add to collection (tick per collection, New collection…) ──
        menu.Items.Add(BuildCollectionsMenu(card));

        // ── Game notes (also on the RHI overlay's Info and links page) ──
        var gameNotesItem = new MenuFlyoutItem { Text = Loc.GetString("CardMenu.GameNotes"), Tag = card };
        gameNotesItem.Click += async (s, ev) => await EditGameNotesAsync(card);
        menu.Items.Add(gameNotesItem);

        // ── Artwork (Spotlight: custom cover and banner) ──
        if (_detailPanelBuilder.Layout.IsSpotlight)
            menu.Items.Add(BuildArtworkMenu(card));

        menu.Items.Add(new MenuFlyoutSeparator());

        // ── Discussion / Instructions (conditional) ──
        if (card.HasNameUrl)
        {
            var discussionItem = new MenuFlyoutItem
            {
                Text = Loc.GetString("CardMenu.DiscussionInstructions"),
                Tag = card,
            };
            discussionItem.Click += async (s, ev) =>
            {
                if (card.NameUrl != null)
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(card.NameUrl));
            };
            menu.Items.Add(discussionItem);
        }

        // ── View Notes (conditional) ──
        if (card.HasNotes)
        {
            var notesItem = new MenuFlyoutItem
            {
                Text = Loc.GetString("CardMenu.ViewNotes"),
                Tag = card,
            };
            notesItem.Click += async (s, ev) =>
            {
                // Create a temporary Button to pass through ShowAddonInfoDialogAsync
                // which expects a Button with Tag (card) and DataContext (AddonType)
                var tempBtn = new Button { Tag = card, DataContext = card.IsLumaMode ? AddonType.Luma : AddonType.RenoDX };
                await _dialogService.ShowAddonInfoDialogAsync(tempBtn, ev);
            };
            menu.Items.Add(notesItem);
        }

        // ── Create Desktop Shortcut ──
        menu.Items.Add(new MenuFlyoutSeparator());
        var shortcutItem = new MenuFlyoutItem
        {
            Text = Loc.GetString("CardMenu.CreateDesktopShortcut"),
            Tag  = card,
        };
        shortcutItem.Click += async (s, ev) => await CreateDesktopShortcutAsync(card);
        menu.Items.Add(shortcutItem);

        menu.ShowAt(anchor);
    }

    private async Task CreateDesktopShortcutAsync(GameCardViewModel card)
    {
        try
        {
            var http = App.Services.GetRequiredService<System.Net.Http.HttpClient>();
            var pcgw = App.Services.GetRequiredService<IPcgwService>();
            var lnkPath = await ShortcutService.CreateGameShortcutAsync(
                card.GameName, card, _gameNameService, ViewModel.Manifest, http, pcgw);

            var msg = lnkPath != null
                ? Loc.GetString("Status.DesktopShortcutCreated", card.GameName)
                : Loc.GetString("Status.DesktopShortcutFailed", card.GameName);
            ShowStatusBarMessage(msg, TimeSpan.FromSeconds(8));
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[MainWindow.CreateDesktopShortcutAsync] {ex.Message}");
        }
    }

    internal async void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _dialogService.ShowAddonInfoDialogAsync(sender, e);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.InfoButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    internal async void CardInfoLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var card = GetCardFromSender(sender);
            if (card?.NameUrl != null)
            {
                try { await Windows.System.Launcher.LaunchUriAsync(new Uri(card.NameUrl)); }
                catch (Exception ex) { _crashReporter.Log($"[MainWindow.CardInfoLink_Click] Failed — {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.CardInfoLink_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    internal async void ExternalLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var card = GetCardFromSender(sender);
            if (card == null) return;

            // ── Nexus direct download intercept (dev-unlocked, premium only) ──────
            if (FeatureFlags.NexusMods && card.NexusUrl != null)
            {
                var nexusDl = App.Services.GetRequiredService<NexusDownloadService>();
                if (nexusDl.IsApiKeyConfigured && nexusDl.IsPremium)
                {
                    await ViewModel.InstallNexusModAsync(card);
                    return;
                }
            }

            // ── Default: open browser ─────────────────────────────────────────────
            // When IsExternalOnly the ExternalUrl has already been resolved correctly
            // (e.g. forced to Discord by ApplyCardOverrides). Use it directly so a
            // NexusUrl on the underlying mod can't override the intended destination.
            var url = card.IsExternalOnly ? card.ExternalUrl : (card.NexusUrl ?? card.DiscordUrl ?? card.ExternalUrl);

            // For Nexus URLs, resolve the latest file ID and open directly to the download page
            // URL format: nexusmods.com/{domain}/mods/{id}?tab=files&file_id={fileId}&nmm=1
            // This skips the mod page and files tab, landing directly on the "Slow download" button.
            if (!string.IsNullOrEmpty(url) && url.Contains("nexusmods.com", StringComparison.OrdinalIgnoreCase)
                && FeatureFlags.NexusMods)
            {
                var parsed = NexusUpdateService.ParseNexusUrl(url);
                if (parsed.HasValue)
                {
                    try
                    {
                        var nexusDl = App.Services.GetRequiredService<NexusDownloadService>();
                        var latestFile = await nexusDl.GetLatestMainFileAsync(parsed.Value.Domain, parsed.Value.ModId).ConfigureAwait(false);
                        if (latestFile != null)
                            url = $"https://www.nexusmods.com/{parsed.Value.Domain}/mods/{parsed.Value.ModId}?tab=files&file_id={latestFile.FileId}&nmm=1";
                    }
                    catch (Exception ex) { CrashReporter.Log($"[MainWindow.ExternalLink_Click] Failed to resolve latest Nexus file, using base URL — {ex.Message}"); }
                }
            }
            else if (!string.IsNullOrEmpty(url) && url.Contains("nexusmods.com", StringComparison.OrdinalIgnoreCase)
                && !url.Contains("?tab=", StringComparison.OrdinalIgnoreCase))
            {
                url = url.TrimEnd('/') + "?tab=files";
            }
            if (!string.IsNullOrEmpty(url))
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));

            // Reset Nexus baseline when user clicks the update button — they're acknowledging the update
            // NOTE: This runs on the UI thread (LaunchUriAsync returns on the original context).
            // The ConfigureAwait(false) above is only for the Nexus API call — after that we're back
            // on the UI thread via the await chain. But to be explicit and safe, wrap UI mutations.
            if (card.Status == GameStatus.UpdateAvailable && card.IsExternalOnly)
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    var nexusService = App.Services.GetRequiredService<INexusUpdateService>();
                    nexusService.ResetBaseline(card.GameName);
                    card.Status = GameStatus.Installed;
                    card.NotifyAll();
                    ViewModel.NotifyUpdateButtonChanged();
                });
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.ExternalLink_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void PcgwLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var card = GetCardFromSender(sender);
            if (card?.PcgwUrl != null)
                await Windows.System.Launcher.LaunchUriAsync(new Uri(card.PcgwUrl));
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.PcgwLink_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ── Nexus Mods button forwarders (XAML routes clicks to MainWindow) ──────────

    private void NexusConnectBtn_Click(object sender, RoutedEventArgs e)
        => _settingsHandler.NexusConnectBtn_Click(sender, e);

    private void NexusDisconnectBtn_Click(object sender, RoutedEventArgs e)
        => _settingsHandler.NexusDisconnectBtn_Click(sender, e);

    private void NxmRegisterBtn_Click(object sender, RoutedEventArgs e)
        => _settingsHandler.NxmRegisterBtn_Click(sender, e);

    // ── GitHub OAuth button forwarders ────────────────────────────────────────


    private void GitHubDisconnectBtn_Click(object sender, RoutedEventArgs e)
        => _settingsHandler.GitHubDisconnectBtn_Click(sender, e);

    private void GitHubPatSaveBtn_Click(object sender, RoutedEventArgs e)
        => _settingsHandler.GitHubPatSaveBtn_Click(sender, e);

    private void GitHubPatClearBtn_Click(object sender, RoutedEventArgs e)
        => _settingsHandler.GitHubPatClearBtn_Click(sender, e);

    private async void NexusModsLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var card = GetCardFromSender(sender);
            if (card?.NexusModsUrl != null)
                await Windows.System.Launcher.LaunchUriAsync(new Uri(card.NexusModsUrl));
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.NexusModsLink_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void UwFixLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var card = GetCardFromSender(sender);
            if (card?.UwFixUrl != null)
                await Windows.System.Launcher.LaunchUriAsync(new Uri(card.UwFixUrl));
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.UwFixLink_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void UltraPlusLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var card = GetCardFromSender(sender);
            if (card?.UltraPlusUrl != null)
                await Windows.System.Launcher.LaunchUriAsync(new Uri(card.UltraPlusUrl));
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.UltraPlusLink_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ── Detail panel handlers ─────────────────────────────────────────────────────

    private void DetailScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateDetailPanelWidth(e.NewSize.Width);

    /// <summary>
    /// Classic: an 850 px column (24 px padding). Tabs (mockup A): the content fills the area with
    /// 32 px side padding, capped so very wide windows stay readable.
    /// </summary>
    internal void UpdateDetailPanelWidth(double viewerWidth = -1)
    {
        if (viewerWidth < 0) viewerWidth = DetailScrollViewer.ActualWidth;
        bool tabs = _detailPanelBuilder?.Layout.IsTabs == true;
        double maxWidth = tabs ? 1600 : 850;
        DetailPanel.Padding = tabs ? new Thickness(32, 24, 32, 32) : new Thickness(24, 18, 24, 24);
        DetailPanel.Spacing = tabs ? 20 : 16;
        var available = viewerWidth - (tabs ? 0 : 48);
        DetailPanel.Width = available > maxWidth ? maxWidth : (available > 0 ? available : double.NaN);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchQuery = SearchBox.Text;
        // Always show the clear (✕) button
        VisualStateManager.GoToState(SearchBox, "ButtonVisible", true);

        // Show/hide the save filter button based on whether there's a non-whitespace query
        SaveFilterButton.Visibility = string.IsNullOrWhiteSpace(SearchBox.Text)
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;

        // Refresh custom chip styles (active filter may have been deactivated by the query change)
        RebuildCustomFilterChips();
    }

    private async void SaveFilterButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentQuery = SearchBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(currentQuery)) return;

            var nameBox = new TextBox { PlaceholderText = Loc.GetString("Dialog.FilterName"), Text = currentQuery, Width = 350 };
            var errorText = new TextBlock
            {
                Text = "",
                Foreground = Brush(ResourceKeys.AccentRedBrush),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
            };

            var dialog = new ContentDialog
            {
                Title = Loc.GetString("Dialog.SaveCustomFilter"),
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = Loc.GetString("Dialog.SaveCustomFilter.Prompt", currentQuery),
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brush(ResourceKeys.TextSecondaryBrush),
                        },
                        nameBox,
                        errorText,
                    }
                },
                PrimaryButtonText = Loc.GetString("Dialog.Save"),
                CloseButtonText = Loc.GetString("Dialog.Cancel"),
                XamlRoot = Content.XamlRoot,
                Background = Brush(ResourceKeys.SurfaceToolbarBrush),
                RequestedTheme = ElementTheme.Dark,
            };

            // Validate inline before closing the dialog
            dialog.PrimaryButtonClick += (s, args) =>
            {
                var name = nameBox.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(name))
                {
                    errorText.Text = Loc.GetString("Dialog.PleaseEnterAFilterName");
                    errorText.Visibility = Visibility.Visible;
                    args.Cancel = true;
                    return;
                }
                if (ViewModel.Filter.CustomFilterNameExists(name))
                {
                    errorText.Text = Loc.GetString("Dialog.SaveCustomFilter.Exists", name);
                    errorText.Visibility = Visibility.Visible;
                    args.Cancel = true;
                    return;
                }
            };

            var result = await DialogService.ShowSafeAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            var filterName = nameBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(filterName)) return;

            ViewModel.Filter.AddCustomFilter(filterName, currentQuery);
            RebuildCustomFilterChips();

            // Clear search box and auto-select the new filter
            SearchBox.Text = "";
            SaveFilterButton.Visibility = Visibility.Collapsed;
            ViewModel.Filter.ActivateCustomFilter(filterName);
            RebuildCustomFilterChips();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.SaveFilterButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Rebuilds the custom filter chip UI from <see cref="FilterViewModel.CustomFilters"/>.
    /// </summary>
    private void RebuildCustomFilterChips()
    {
        CustomFilterChipPanel.Children.Clear();

        foreach (var filter in ViewModel.Filter.CustomFilters)
        {
            var chipName = filter.Name;
            bool isActive = string.Equals(ViewModel.Filter.ActiveCustomFilterName, chipName, StringComparison.OrdinalIgnoreCase);

            var chip = new Button
            {
                Content = chipName,
                Tag = chipName,
                Background = new SolidColorBrush(
                    ((SolidColorBrush)Application.Current.Resources[
                        isActive ? ResourceKeys.CustomChipActiveBrush : ResourceKeys.CustomChipDefaultBrush]).Color),
                Foreground = isActive
                    ? new SolidColorBrush(Microsoft.UI.Colors.White)
                    : (SolidColorBrush)Application.Current.Resources[ResourceKeys.CustomChipTextBrush],
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 5, 10, 5),
                FontSize = 11,
            };

            chip.Click += CustomFilterChip_Click;
            ToolTipService.SetToolTip(chip, string.IsNullOrWhiteSpace(filter.Query)
                ? Loc.GetString("Dialog.CustomFilter.Tooltip")
                : Loc.GetString("Dialog.CustomFilter.TooltipWithQuery", filter.Query));

            // Right-click context menu with "Delete" option (Req 5.1–5.5)
            var flyout = new MenuFlyout();
            var deleteItem = new MenuFlyoutItem { Text = Loc.GetString("Dialog.Delete") };
            deleteItem.Click += (s, args) =>
            {
                ViewModel.Filter.RemoveCustomFilter(chipName);
                RebuildCustomFilterChips();
            };
            flyout.Items.Add(deleteItem);
            chip.ContextFlyout = flyout;

            CustomFilterChipPanel.Children.Add(chip);
        }
    }

    private void CustomFilterChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string name) return;

        ViewModel.Filter.ActivateCustomFilter(name);
        RebuildCustomFilterChips();
    }

    // ── Manual add game ───────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a game's page right after it was added by hand (Add Game button, exe drag-and-drop),
    /// so it doesn't have to be found in a long list. Clears the search / filter first if they
    /// hide it. Runs after the list has updated.
    /// </summary>
    private void OpenAddedGame(GameCardViewModel card)
    {
        DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            try
            {
                if (IsShuttingDown) return;
                if (!ViewModel.DisplayedGames.Contains(card))
                {
                    if (SearchBox.Text.Length > 0) SearchBox.Text = ""; // also ends a custom filter
                    ViewModel.SetFilterCommand.Execute("Detected");
                    RefreshFilterButtonStyles();
                }
                if (!ViewModel.DisplayedGames.Contains(card))
                {
                    _crashReporter.Log($"[MainWindow.OpenAddedGame] '{card.GameName}' isn't in the list; not selected");
                    return;
                }
                GameList.SelectedItem = card;
                GameList.ScrollIntoView(card);
                _crashReporter.Log($"[MainWindow.OpenAddedGame] Opened '{card.GameName}'");
            }
            catch (Exception ex)
            {
                _crashReporter.Log($"[MainWindow.OpenAddedGame] {ex.Message}");
            }
        });
    }

    private async void AddGameButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _crashReporter.Log("[MainWindow.AddGameButton_Click] Button clicked");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _crashReporter.Log($"[MainWindow.AddGameButton_Click] hwnd={hwnd}");

            // Use Win32 OpenFileDialog as primary method — WinRT FileOpenPicker has
            // COM threading issues on some systems when background work is active.
            string? filePath = null;
            try
            {
                filePath = await Task.Run(() =>
                {
                    var ofn = new NativeInterop.OpenFileName();
                    ofn.structSize = System.Runtime.InteropServices.Marshal.SizeOf(ofn);
                    ofn.hwndOwner = hwnd;
                    ofn.filter = Loc.GetString("Overrides.LaunchExe.FilterExecutables") + "\0*.exe\0" + Loc.GetString("Overrides.LaunchExe.FilterAllFiles") + "\0*.*\0";
                    ofn.file = new string(new char[260]);
                    ofn.maxFile = ofn.file.Length;
                    ofn.title = Loc.GetString("Overrides.LaunchExe.SelectTitle");
                    ofn.flags = 0x00080000 | 0x00001000; // OFN_EXPLORER | OFN_FILEMUSTEXIST

                    return NativeInterop.GetOpenFileName(ref ofn) ? ofn.file.TrimEnd('\0') : null;
                });
            }
            catch (Exception ex)
            {
                _crashReporter.Log($"[MainWindow.AddGameButton_Click] Win32 file dialog failed — {ex.GetType().Name}: {ex.Message}");
                return;
            }

            _crashReporter.Log($"[MainWindow.AddGameButton_Click] Dialog result: {(filePath != null ? filePath : "null (cancelled)")}");
            if (string.IsNullOrEmpty(filePath)) return;

            // Use the exe's parent folder as the install path
            var folder = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(folder)) return;

            // Pre-populate the game name from the folder name
            var suggestedName = Path.GetFileName(folder);

            // Step 2: Ask for the game name
            var nameBox = new TextBox { Text = suggestedName, Width = 350 };
            nameBox.SelectAll();
            var nameDialog = new ContentDialog
            {
                Title           = Loc.GetString("Dialog.NameThisGame"),
                Content         = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = Loc.GetString("Dialog.AddGame.Selected", filePath), TextWrapping = TextWrapping.Wrap, Foreground = Brush(ResourceKeys.TextSecondaryBrush), FontSize = 11 },
                        new TextBlock { Text = Loc.GetString("Dialog.EnterTheGameName"), TextWrapping = TextWrapping.Wrap, Foreground = Brush(ResourceKeys.TextSecondaryBrush) },
                        nameBox
                    }
                },
                PrimaryButtonText   = Loc.GetString("Dialog.AddGame"),
                CloseButtonText     = Loc.GetString("Dialog.Cancel"),
                XamlRoot            = Content.XamlRoot,
                Background          = Brush(ResourceKeys.SurfaceToolbarBrush),
                RequestedTheme      = ElementTheme.Dark,
            };
            var result = await DialogService.ShowSafeAsync(nameDialog);
            if (result != ContentDialogResult.Primary) return;

            var gameName = nameBox.Text.Trim();
            if (string.IsNullOrEmpty(gameName)) return;
            _crashReporter.Log($"[MainWindow.AddGameButton_Click] Adding game: {gameName} at {folder}");

            var game = new DetectedGame
            {
                Name = gameName, InstallPath = folder, Source = "Manual", IsManuallyAdded = true
            };
            ViewModel.AddManualGameCommand.Execute(game);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.AddGameButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ── Filter tabs ───────────────────────────────────────────────────────────────

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        ViewModel.SetFilterCommand.Execute(btn.Tag as string ?? "Detected");
        RefreshFilterButtonStyles();
    }

    internal void FavouriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not GameCardViewModel card) return;
        ViewModel.ToggleFavouriteCommand.Execute(card);

        // Refresh the detail panel icon to reflect the new state
        DetailPanelBuilder.ApplyFavouriteButtonStyle(DetailFavBtn, DetailFavIcon, card.IsFavourite);
    }

    // ── Card handlers ─────────────────────────────────────────────────────────────

    internal void InstallButton_Click(object sender, RoutedEventArgs e)
        => _installEventHandler.InstallButton_Click(sender, e);

    internal void UninstallButton_Click(object sender, RoutedEventArgs e)
        => _installEventHandler.UninstallButton_Click(sender, e);

    internal void InstallRsButton_Click(object sender, RoutedEventArgs e)
        => _installEventHandler.InstallRsButton_Click(sender, e);

    internal void UninstallRsButton_Click(object sender, RoutedEventArgs e)
        => _installEventHandler.UninstallRsButton_Click(sender, e);

    private void ChooseShadersButton_Click(object sender, RoutedEventArgs e)
        => _installEventHandler.ChooseShadersButton_Click(sender, e);

    private async void ReShadeAddonsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Req 10.1–10.5: First-time warning dialog
            if (!ViewModel.Settings.AddonWarningDismissed)
            {
                var warningDialog = new ContentDialog
                {
                    Title = Loc.GetString("Dialog.ReshadeAddons2"),
                    Content = new TextBlock
                    {
                        Text = Loc.GetString("Dialog.ReshadeAddons.Warning"),
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                        MaxWidth = 450,
                    },
                    PrimaryButtonText = Loc.GetString("Dialog.Continue"),
                    CloseButtonText = Loc.GetString("Dialog.Cancel"),
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = ElementTheme.Dark,
                };

                var result = await DialogService.ShowSafeAsync(warningDialog);

                if (result != ContentDialogResult.Primary)
                    return; // Req 10.5: Cancel — don't persist flag, don't open manager

                // Req 10.4: Persist dismissal flag so warning is not shown again
                ViewModel.Settings.AddonWarningDismissed = true;
                ViewModel.SaveSettingsPublic();
            }

            // Use the ViewModel's AddonPackService (initialized on startup)
            var addonService = _addonPackService;
            await addonService.EnsureLatestAsync();
            // Re-apply manifest overrides (EnsureLatestAsync repopulates from Addons.ini)
            (addonService as RenoDXCommander.Services.AddonPackService)?.ApplyManifestOverrides(ViewModel.Manifest);
            await AddonManagerDialog.ShowAsync(Content.XamlRoot, addonService,
                ViewModel.Settings.EnabledGlobalAddons,
                () => { ViewModel.SaveSettingsPublic(); ViewModel.DeployAllAddons(); });
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[MainWindow.ReShadeAddonsButton_Click] Failed — {ex.Message}");
        }
    }

}
