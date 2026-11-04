// DetailPanelBuilder.Extras.FrameGen.cs — Extras section: DLSSG 20/30 and Universal MFG Unlocker rows with their DLL pickers.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private static readonly string[] MfgDllNames =
    {
        "version.dll", "dinput8.dll", "winmm.dll", "d3d9.dll", "d3d10.dll",
        "d3d11.dll", "d3d12.dll", "dxgi.dll", "dsound.dll", "wininet.dll",
        "winhttp.dll", "binkw64.dll", "bink2w64.dll", "xinput1_1.dll",
        "xinput1_2.dll", "xinput1_3.dll", "xinput1_4.dll", "xinput9_1_0.dll",
        "xinputuap.dll",
    };

    /// <summary>
    /// Warns that XeMFG standalone doesn't start while ReShade is installed. Returns true to continue.
    /// "Don't show this again" is saved even when the user cancels.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> ShowUmfgReShadeWarningAsync(XamlRoot? xamlRoot)
    {
        xamlRoot ??= _window.Content.XamlRoot;
        if (xamlRoot == null) return true;

        var dontShowCheck = new CheckBox
        {
            Content = Loc.GetString("Dialog.DonTShowThisAgain"),
            FontSize = 12,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Extras.Rtx40Mfg.ReShadeWarning.Content"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 22,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        });
        content.Children.Add(dontShowCheck);

        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Extras.Rtx40Mfg.ReShadeWarning.Title"),
            Content = content,
            PrimaryButtonText = Loc.GetString("Dialog.Continue"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var result = await DialogService.ShowSafeAsync(dialog);
        if (dontShowCheck.IsChecked == true)
        {
            _window.ViewModel.Settings.UmfgReShadeWarningDismissed = true;
            _window.ViewModel.SaveSettingsPublic();
        }
        return result == ContentDialogResult.Primary;
    }

    private void BuildDlssg2030Row(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildDlssg2030Row({card.GameName})");
        var svc         = App.Services.GetRequiredService<Dlssg20_30Service>();
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";

        var currentDllName = _window.ViewModel.GetDlssg2030InstalledAs(gameName, store);
        bool isInstalled   = svc.IsInstalledIn(installPath, currentDllName);
        // Use cached state from card instead of File.Exists on UI thread
        bool addonConflict = card.MfgAdaInstalled;

        string statusText  = isInstalled ? (svc.StagedVersion ?? Loc.GetString("Status.Installed")) : Loc.GetString("Status.Ready");
        string statusColor = isInstalled ? "#5ECB7D" : "#A0AABB";

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExtrasContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        // Col 0 — label
        var label = new TextBlock
        {
            Text = Loc.GetString("Extras.Dlssg2030"),
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(label, Loc.GetString("Extras.Dlssg2030.Tooltip"));
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Col 1 — status (version SHA when installed)
        var statusBlock = new TextBlock
        {
            Text = statusText,
            FontSize = 12,
            Foreground = UIFactory.GetBrush(statusColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            TextDecorations = isInstalled ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None,
        };
        if (isInstalled)
        {
            ToolTipService.SetToolTip(statusBlock, Loc.GetString("Extras.InstalledAs.Tooltip", currentDllName));
            statusBlock.PointerPressed += (s, e) =>
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/sdli1995/dlssg_for_sm86"));
            AttachLinkCursor(statusBlock);
        }
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        // Col 2 — Info button
        var infoBtn = new Button
        {
            Content = Loc.GetString("Dialog.Info"),
            FontSize = 11,
            Padding = new Thickness(6, 2, 6, 2),
            Width = 36,
            Height = 32,
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        ToolTipService.SetToolTip(infoBtn, Loc.GetString("Extras.Dlssg2030.Info.Tooltip"));
        infoBtn.Click += (s, e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/sdli1995/dlssg_for_sm86"));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        var installBtn = new Button
        {
            Content = isInstalled ? Loc.GetString("Extras.Dlssg2030.Reinstall.Button") : Loc.GetString("Extras.Dlssg2030.Install.Button"),
            FontSize = 12,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            Background = isInstalled ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = isInstalled ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isInstalled ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
        };

        if (addonConflict)
        {
            installBtn.IsEnabled = false;
            installBtn.Opacity   = 0.35;
            installBtn.Content   = Loc.GetString("Extras.MfgAdaUnlock.UninstallFirst.Button");
            ToolTipService.SetToolTip(installBtn, Loc.GetString("Extras.Dlssg2030.MfgAdaConflict.Tooltip"));
        }
        else
        {
            ToolTipService.SetToolTip(installBtn, isInstalled
                ? Loc.GetString("Extras.Dlssg2030.Reinstall.Tooltip", currentDllName)
                : Loc.GetString("Extras.Dlssg2030.Install.Tooltip"));
        }

        installBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;

            var chosen = await ShowDlssg2030DllPickerAsync(card, currentDllName);
            if (chosen == null) return;

            installBtn.IsEnabled = false;
            installBtn.Content   = Loc.GetString("Status.Installing");
            try
            {
                // Only one proxy may be in the folder: if reinstalling under a different name,
                // put the old slot back first (the ini and dlssg_sm86\ folder are reused)
                if (isInstalled && !string.IsNullOrEmpty(currentDllName)
                    && !currentDllName.Equals(chosen, StringComparison.OrdinalIgnoreCase))
                    AuxInstallService.SentinelRestore(Path.Combine(installPath, currentDllName));

                bool ok = await Task.Run(async () =>
                {
                    if (!svc.IsStagingReady || svc.HasUpdate)
                        await svc.EnsureStagingAsync().ConfigureAwait(false);
                    return svc.Install(installPath, chosen);
                });

                if (ok)
                {
                    _window.ViewModel.SetDlssg2030InstalledAs(gameName, chosen, store);
                    RequestExtrasRebuild(card);
                }
                else
                {
                    installBtn.Content   = Loc.GetString("Extras.DownloadFailedRetry.Button");
                    installBtn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildDlssg2030Row] Install failed — {ex.Message}");
                installBtn.Content   = Loc.GetString("Extras.InstallFailed.Button");
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog button
        var cogBtn = CogDialog.MakeCogButton(Loc.GetString("Extras.Dlssg2030.Settings.Tooltip"));
        cogBtn.Click += async (s, e) =>
        {
            try { await ShowDlssg2030SettingsAsync(installPath, isInstalled); }
            catch (Exception ex) { CrashReporter.Log($"[BuildDlssg2030Row] Settings dialog failed — {ex.Message}"); }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        // Col 5 — Remove button
        var removeBtn = new Button
        {
            Width = 36, Height = 32, Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Opacity = isInstalled ? 1.0 : 0,
            IsHitTestVisible = isInstalled,
        };
        ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.Dlssg2030.Remove.Tooltip"));
        removeBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            svc.Uninstall(installPath, currentDllName);
            _window.ViewModel.SetDlssg2030InstalledAs(gameName, null, store);
            RequestExtrasRebuild(card);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    private async Task<string?> ShowDlssg2030DllPickerAsync(GameCardViewModel card, string? currentDllName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return null;

        var rhiOwned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(card.RsInstalledFile))  rhiOwned.Add(card.RsInstalledFile);
        if (!string.IsNullOrEmpty(card.OsInstalledFile))  rhiOwned.Add(card.OsInstalledFile);
        if (!string.IsNullOrEmpty(card.DcInstalledFile))  rhiOwned.Add(card.DcInstalledFile);

        string? chosen = null;

        var listPanel    = new StackPanel { Spacing = 4 };
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 360,
            Content   = listPanel,
        };

        foreach (var name in Dlssg20_30Service.ProxyNames)
        {
            bool isRecommended  = string.Equals(name, "version.dll", StringComparison.OrdinalIgnoreCase);
            bool isRhiOwned     = rhiOwned.Contains(name);
            bool isCurrent      = string.Equals(name, currentDllName, StringComparison.OrdinalIgnoreCase);

            var btn = new Button
            {
                HorizontalAlignment        = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding         = new Thickness(10, 6, 10, 6),
                CornerRadius    = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                IsEnabled       = !isRhiOwned,
                Opacity         = isRhiOwned ? 0.4 : 1.0,
                Background  = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBgBrush) : UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
                BorderBrush = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush) : UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            };

            var contentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            contentRow.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = isRhiOwned
                    ? UIFactory.Brush(ResourceKeys.TextTertiaryBrush)
                    : UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (isRecommended)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.Recommended"), "#1A3A20", "#6AE87A", "#2A5A30"));
            if (isRhiOwned)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.UsedByRhi"), "#2A1818", "#CC6666", "#5A2828"));
            if (isCurrent)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.Current"), "#182840", "#7AACDD", "#2A4468"));

            btn.Content = contentRow;
            if (isRhiOwned)
                ToolTipService.SetToolTip(btn, Loc.GetString("Extras.DllName.RhiOwnedShort.Tooltip"));

            btn.Tag    = name;
            btn.Click += (s, ev) =>
            {
                chosen = (s as Button)?.Tag as string;
                if (s is FrameworkElement fe)
                {
                    var dialog = FindParentContentDialog(fe);
                    dialog?.Hide();
                }
            };
            listPanel.Children.Add(btn);
        }

        var pickerDialog = new ContentDialog
        {
            Title = Loc.GetString("Extras.Dlssg2030.DllPicker.Title"),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = Loc.GetString("Extras.Dlssg2030.DllPicker.Content"),
                        FontSize = 11,
                        Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    scrollViewer,
                }
            },
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot        = _window.Content.XamlRoot,
        };

        await DialogService.ShowSafeAsync(pickerDialog);
        return chosen;
    }

    private void BuildRtx40MfgRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildRtx40MfgRow({card.GameName})");
        var mfgSvc       = App.Services.GetRequiredService<Rtx40MfgService>();
        var gameName     = card.GameName;
        var store        = card.Source ?? "";
        var installPath  = card.InstallPath ?? "";

        // Use cached state from card instead of File.Exists on UI thread
        bool isInstalled   = card.Rtx40MfgInstalled;
        bool addonConflict = card.MfgAdaInstalled;
        var currentDllName = _window.ViewModel.GetRtx40MfgInstalledAs(gameName, store);
        bool isPluginInstall = isInstalled && Rtx40MfgService.IsPluginInstall(currentDllName); // loaded by OptiScaler

        // Status
        string statusText  = isInstalled ? (mfgSvc.StagedVersion ?? Loc.GetString("Status.Installed")) : Loc.GetString("Status.Ready");
        string statusColor = isInstalled ? "#5ECB7D" : "#A0AABB";

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExtrasContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        // Col 0 — label
        var label = new TextBlock
        {
            Text = Loc.GetString("Extras.Rtx40Mfg.FullName"),
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap, // long name; wraps instead of clipping in the 120 px column
        };
        ToolTipService.SetToolTip(label, Loc.GetString("Extras.Rtx40Mfg.Tooltip"));
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Col 1 — status
        var statusBlock = new TextBlock
        {
            Text = statusText,
            FontSize = 12,
            Foreground = UIFactory.GetBrush(statusColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            TextDecorations = isInstalled ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None,
        };
        if (isInstalled)
        {
            ToolTipService.SetToolTip(statusBlock, Loc.GetString("Extras.InstalledAsPage.Tooltip", currentDllName));
            statusBlock.PointerPressed += (s, e) =>
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dashdogy/MFG-Unlocker/releases"));
            AttachLinkCursor(statusBlock);
        }
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        // Col 2 — Info button
        var infoBtn = new Button
        {
            Content = Loc.GetString("Dialog.Info"),
            FontSize = 11,
            Padding = new Thickness(6, 2, 6, 2),
            Width = 36,
            Height = 32,
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        ToolTipService.SetToolTip(infoBtn, Loc.GetString("Extras.Rtx40Mfg.Info.Tooltip"));
        infoBtn.Click += (s, e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dashdogy/MFG-Unlocker"));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        var installBtn = new Button
        {
            Content = isInstalled ? Loc.GetString("Extras.Rtx40Mfg.Reinstall.Button") : Loc.GetString("Extras.Rtx40Mfg.Install.Button"),
            FontSize = 12,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            Background = isInstalled
                ? UIFactory.GetBrush("#182840")
                : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = isInstalled
                ? UIFactory.GetBrush("#7AACDD")
                : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isInstalled
                ? UIFactory.GetBrush("#2A4468")
                : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
        };

        if (addonConflict)
        {
            installBtn.IsEnabled = false;
            installBtn.Opacity   = 0.35;
            installBtn.Content   = Loc.GetString("Extras.MfgAdaUnlock.UninstallFirst.Button");
            ToolTipService.SetToolTip(installBtn, Loc.GetString("Extras.Rtx40Mfg.MfgAdaConflict.Tooltip"));
        }
        else
        {
            // Cheap check for the tooltip only; the click handler checks OptiScaler's install record
            bool osPluginLikely = card.IsOsInstalled && !string.IsNullOrEmpty(installPath)
                && System.IO.Directory.Exists(System.IO.Path.Combine(installPath, "OptiScaler"));
            ToolTipService.SetToolTip(installBtn,
                isPluginInstall ? Loc.GetString("Extras.Rtx40Mfg.Tooltip.PluginReinstall", currentDllName)
                : osPluginLikely ? (isInstalled
                    ? Loc.GetString("Extras.Rtx40Mfg.Tooltip.PluginMoveReinstall", currentDllName)
                    : Loc.GetString("Extras.Rtx40Mfg.Tooltip.PluginInstall"))
                : isInstalled ? Loc.GetString("Extras.Rtx40Mfg.Reinstall.Tooltip", currentDllName)
                : Loc.GetString("Extras.Rtx40Mfg.Install.Tooltip"));
        }

        installBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            var vm = _window.ViewModel;

            // With an OptiScaler 10 build installed by RHI the mod goes in OptiScaler\plugins (no name to pick).
            // Read the live location too: it can have moved since this panel was built.
            bool asPlugin    = await Task.Run(() => vm.IsOptiScalerPluginHost(card));
            var liveDllName  = vm.GetRtx40MfgInstalledAs(gameName, store);
            bool liveInstalled = !string.IsNullOrEmpty(liveDllName) && mfgSvc.IsInstalledIn(installPath, liveDllName);

            // XeMFG standalone can't start while ReShade hooks DXGI (the mod reports menu_factory_not_dxgi).
            // Through OptiScaler it works, so no warning there.
            if (!asPlugin && card.IsRsInstalled && !card.RequiresVulkanInstall
                && !vm.Settings.UmfgReShadeWarningDismissed
                && !await ShowUmfgReShadeWarningAsync(installBtn.XamlRoot))
                return;

            string? chosen;
            if (asPlugin)
                chosen = Rtx40MfgService.PluginRelPath;
            else
            {
                chosen = await ShowMfgDllPickerAsync(card, Rtx40MfgService.IsPluginInstall(liveDllName) ? null : liveDllName);
                if (chosen == null) return;
            }

            installBtn.IsEnabled = false;
            installBtn.Content   = Loc.GetString("Status.Installing");
            try
            {
                // Proxy → plugin: move it so the old proxy name is remembered for when OptiScaler is removed
                if (asPlugin && liveInstalled && !Rtx40MfgService.IsPluginInstall(liveDllName))
                {
                    var from  = liveDllName!;
                    var moved = await Task.Run(() => mfgSvc.MoveToOptiScalerPlugin(installPath, from));
                    if (moved != null) liveDllName = moved;
                }

                // If reinstalling under a different name, remove the old one first
                if (liveInstalled && !string.IsNullOrEmpty(liveDllName)
                    && !liveDllName.Equals(chosen, StringComparison.OrdinalIgnoreCase))
                    mfgSvc.Uninstall(installPath, liveDllName);

                bool ok = await Task.Run(async () =>
                {
                    if (!mfgSvc.IsStagingReady || mfgSvc.HasUpdate)
                        await mfgSvc.EnsureStagingAsync().ConfigureAwait(false);
                    if (!asPlugin) // standalone XeMFG; through OptiScaler it uses OptiScaler's XeSS files
                        await mfgSvc.EnsureXessStagingAsync().ConfigureAwait(false);
                    return mfgSvc.Install(installPath, chosen);
                });

                if (ok)
                {
                    _window.ViewModel.SetRtx40MfgInstalledAs(gameName, chosen, store);
                    card.SetMfgState(mfgAdaInstalled: card.MfgAdaInstalled, mfgAdaRtx40Conflict: card.MfgAdaInstalled, rtx40MfgInstalled: true);
                    RequestExtrasRebuild(card);
                }
                else
                {
                    installBtn.Content   = Loc.GetString("Extras.DownloadFailedRetry.Button");
                    installBtn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildRtx40MfgRow] Install failed — {ex.Message}");
                installBtn.Content   = Loc.GetString("Extras.InstallFailed.Button");
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog button
        var cogBtn = CogDialog.MakeCogButton(Loc.GetString("Extras.Rtx40Mfg.Settings.Tooltip"));
        cogBtn.Click += async (s, e) =>
        {
            try
            {
                var cog = new CogDialog(Loc.GetString("Extras.Rtx40Mfg.Settings.Title"), _window.Content.XamlRoot);
                cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.Intro"));
                cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.ModeFollowGame"));
                cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.ModeFixed"));
                cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.ModeDynamic"));
                cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.PresetNote"));
                if (isInstalled)
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.CurrentlyInstalled", currentDllName));

                if (isPluginInstall)
                {
                    cog.AddSection(Loc.GetString("Extras.Rtx40Mfg.Settings.OsSection"));
                    cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.OsIntro"));
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.OsNote"));
                }

                cog.AddSection(Loc.GetString("Extras.Rtx40Mfg.Settings.XessSection"));
                if (isPluginInstall)
                    cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.XessViaOs"));
                else
                    cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.XessIntro"));
                if (isInstalled && !isPluginInstall && !string.IsNullOrEmpty(installPath))
                {
                    var xessDir = System.IO.Path.Combine(installPath, Rtx40MfgService.XessFolderName);
                    bool xessPresent = System.IO.File.Exists(System.IO.Path.Combine(xessDir, "libxess_fg.dll"))
                                    && System.IO.File.Exists(System.IO.Path.Combine(xessDir, "libxell.dll"));
                    if (xessPresent)
                        cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.XessInstalled"));
                    else
                        cog.AddWarning(Loc.GetString("Extras.Rtx40Mfg.Settings.XessMissing"));
                }
                if (string.Equals(card.RsInstalledFile, "dxgi.dll", StringComparison.OrdinalIgnoreCase))
                    cog.AddWarning(Loc.GetString("Extras.Rtx40Mfg.Settings.ReShadeDxgiWarning"));
                cog.AddButtons(cog.MakeButton(Loc.GetString("Extras.Rtx40Mfg.Settings.OpenGithub"), CogButtonKind.Secondary,
                    async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dashdogy/MFG-Unlocker")); }));
                await cog.ShowAsync();
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildRtx40MfgRow] Settings dialog failed — {ex.Message}");
            }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        // Col 5 — Remove button
        var removeBtn = new Button
        {
            Width = 36,
            Height = 32,
            Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Opacity = isInstalled ? 1.0 : 0,
            IsHitTestVisible = isInstalled,
        };
        ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.Rtx40Mfg.Remove.Tooltip"));
        removeBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            // Live location: it may have moved in or out of OptiScaler\plugins since the panel was built
            mfgSvc.Uninstall(installPath, _window.ViewModel.GetRtx40MfgInstalledAs(gameName, store) ?? currentDllName);
            _window.ViewModel.SetRtx40MfgInstalledAs(gameName, null, store);
            card.SetMfgState(mfgAdaInstalled: card.MfgAdaInstalled, mfgAdaRtx40Conflict: false, rtx40MfgInstalled: false);
            RequestExtrasRebuild(card);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    private async Task<string?> ShowMfgDllPickerAsync(GameCardViewModel card, string? currentDllName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return null;

        var rhiOwned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(card.RsInstalledFile))  rhiOwned.Add(card.RsInstalledFile);
        if (!string.IsNullOrEmpty(card.OsInstalledFile))  rhiOwned.Add(card.OsInstalledFile);
        if (!string.IsNullOrEmpty(card.DcInstalledFile))  rhiOwned.Add(card.DcInstalledFile);

        string? chosen = null;

        var listPanel    = new StackPanel { Spacing = 4 };
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
            Content   = listPanel,
        };

        foreach (var name in MfgDllNames)
        {
            bool isRecommended  = string.Equals(name, "version.dll", StringComparison.OrdinalIgnoreCase);
            bool isRhiOwned     = rhiOwned.Contains(name);
            bool isDxgiConflict = string.Equals(name, "dxgi.dll", StringComparison.OrdinalIgnoreCase)
                               && !string.IsNullOrEmpty(card.RsInstalledFile);
            bool isCurrent      = string.Equals(name, currentDllName, StringComparison.OrdinalIgnoreCase);

            var btn = new Button
            {
                HorizontalAlignment        = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding         = new Thickness(10, 6, 10, 6),
                CornerRadius    = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                IsEnabled       = !isRhiOwned,
                Opacity         = isRhiOwned ? 0.4 : 1.0,
                Background  = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBgBrush) : UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
                BorderBrush = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush) : UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            };

            var contentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            contentRow.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = isRhiOwned
                    ? UIFactory.Brush(ResourceKeys.TextTertiaryBrush)
                    : UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (isRecommended)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.Recommended"), "#1A3A20", "#6AE87A", "#2A5A30"));
            if (isRhiOwned)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.UsedByRhi"), "#2A1818", "#CC6666", "#5A2828"));
            else if (isDxgiConflict)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.MayConflict"), "#2A1A10", "#CC9955", "#5A3A18"));
            if (isCurrent)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.Current"), "#182840", "#7AACDD", "#2A4468"));

            btn.Content = contentRow;

            if (isRhiOwned)
                ToolTipService.SetToolTip(btn, Loc.GetString("Extras.DllName.RhiOwnedShort.Tooltip"));
            else if (isDxgiConflict)
                ToolTipService.SetToolTip(btn, Loc.GetString("Extras.DllName.DxgiConflict.Tooltip"));

            btn.Tag    = name;
            btn.Click += (s, ev) =>
            {
                chosen = (s as Button)?.Tag as string;
                if (s is FrameworkElement fe)
                {
                    var dialog = FindParentContentDialog(fe);
                    dialog?.Hide();
                }
            };

            listPanel.Children.Add(btn);
        }

        var pickerDialog = new ContentDialog
        {
            Title = Loc.GetString("Extras.Rtx40Mfg.DllPicker.Title"),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = Loc.GetString("Extras.Rtx40Mfg.DllPicker.Content"),
                        FontSize = 11,
                        Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    scrollViewer,
                }
            },
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot        = _window.Content.XamlRoot,
        };

        await DialogService.ShowSafeAsync(pickerDialog);
        return chosen;
    }
}
