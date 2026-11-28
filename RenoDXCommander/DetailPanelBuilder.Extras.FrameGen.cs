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
        // Extras that can't be installed together (DetailPanelBuilder.Extras.Conflicts.cs)
        var blocker = ExtraBlocker(card, ExtraKind.Dlssg2030);

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

        if (blocker is { } fgBlocker)
        {
            ApplyExtraBlock(installBtn, label, ExtraKind.Dlssg2030, fgBlocker);
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
            await InstallDlssg2030CoreAsync(card, isInstalled, currentDllName, null, installBtn,
                installBtn.XamlRoot ?? _window.Content.XamlRoot);
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
            UninstallDlssg2030Core(card, currentDllName);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    /// <summary>
    /// The 20/30 FG Unlock install button's work (shared with Tune presets): fresh installs get the anti-cheat,
    /// closed-source and OptiScaler prompts, then the name picker unless <paramref name="fixedName"/> is given.
    /// <paramref name="installBtn"/> is the row's button (null from Tune). Returns null on success, "Cancelled", or an error.
    /// </summary>
    private async Task<string?> InstallDlssg2030CoreAsync(GameCardViewModel card, bool isInstalled, string? currentDllName,
        string? fixedName, Button? installBtn, XamlRoot? xamlRoot)
    {
        var svc         = App.Services.GetRequiredService<Dlssg20_30Service>();
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";
        try
        {
            // Fresh installs only: anti-cheat warning, closed-source notice, OptiScaler warning
            var st = _window.ViewModel.Settings;
            if (!isInstalled)
            {
                if (!await ConfirmAntiCheatAsync(card, "20/30 FG Unlock", AntiCheatNoticeKey.Dlssg2030, xamlRoot)) return TuneCancelled;
                if (!await ShowClosedSourceNoticeAsync("20/30 FG Unlock", "sdli1995",
                        () => st.Dlssg2030ClosedSourceDismissed, () => st.Dlssg2030ClosedSourceDismissed = true, xamlRoot))
                    return TuneCancelled;
                if (card.IsOsInstalled && !await ConfirmDlssg2030WithOptiScalerAsync(xamlRoot)) return TuneCancelled;
            }

            var chosen = fixedName ?? await ShowDlssg2030DllPickerAsync(card, currentDllName);
            if (chosen == null) return TuneCancelled;

            if (installBtn != null)
            {
                installBtn.IsEnabled = false;
                installBtn.Content   = Loc.GetString("Status.Installing");
            }

            // Only one proxy may be in the folder: if reinstalling under a different name,
            // put the old slot back first (the ini and dlssg_sm86\ folder are reused)
            if (isInstalled && !string.IsNullOrEmpty(currentDllName)
                && !currentDllName.Equals(chosen, StringComparison.OrdinalIgnoreCase))
                AuxInstallService.SentinelRestore(Path.Combine(installPath, currentDllName));

            var (staged, ok) = await Task.Run(async () =>
            {
                if (!svc.IsStagingReady || svc.HasUpdate)
                    await svc.EnsureStagingAsync().ConfigureAwait(false);
                if (!svc.IsStagingReady) return (false, false);
                // Same name as the current install: the file there is RHI's own proxy
                return (true, svc.Install(installPath, chosen,
                    replacesOwnCopy: isInstalled && string.Equals(currentDllName, chosen, StringComparison.OrdinalIgnoreCase)));
            });

            if (ok)
            {
                _window.ViewModel.SetDlssg2030InstalledAs(gameName, chosen, store);
                RequestExtrasRebuild(card);
                return null;
            }
            CrashReporter.Log($"[BuildDlssg2030Row] '{gameName}': {(staged ? "install" : "download")} failed");
            if (installBtn != null)
            {
                installBtn.Content   = staged ? Loc.GetString("Extras.InstallFailedSeeLog.Button") : Loc.GetString("Extras.DownloadFailedRetry.Button");
                installBtn.IsEnabled = true;
            }
            return staged ? Loc.GetString("Extras.Dlssg2030.Install.Error")
                          : Loc.GetString("Extras.Dlssg2030.Download.Error");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[BuildDlssg2030Row] Install failed — {ex.Message}");
            if (installBtn != null)
            {
                installBtn.Content   = Loc.GetString("Extras.InstallFailedSeeLog.Button");
                installBtn.IsEnabled = true;
            }
            return Loc.GetString("Extras.Dlssg2030.Install.ErrorDetail", ex.Message);
        }
    }

    /// <summary>The 20/30 FG Unlock remove button's work (shared with Tune presets).</summary>
    private void UninstallDlssg2030Core(GameCardViewModel card, string? currentDllName)
    {
        var svc = App.Services.GetRequiredService<Dlssg20_30Service>();
        svc.Uninstall(card.InstallPath ?? "", currentDllName);
        _window.ViewModel.SetDlssg2030InstalledAs(card.GameName, null, card.Source ?? "");
        RequestExtrasRebuild(card);
    }

    private async Task<string?> ShowDlssg2030DllPickerAsync(GameCardViewModel card, string? currentDllName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return null;

        string? chosen = null;

        var listPanel    = new StackPanel { Spacing = 4 };
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 360,
            Content   = listPanel,
        };

        foreach (var option in GetExtraDllOptions(card, Dlssg20_30Service.ProxyNames, currentDllName))
        {
            var name            = option.Name;
            bool isRecommended  = option.IsRecommended;
            bool isRhiOwned     = option.BlockedBy != null;
            bool isCurrent      = option.IsCurrent;

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
            AddExtraDllBadges(contentRow, btn, option, Loc.GetString("Extras.Dlssg2030"));
            if (isCurrent)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.Current"), "#182840", "#7AACDD", "#2A4468"));

            btn.Content = contentRow;

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
        var  blocker       = ExtraBlocker(card, ExtraKind.UniversalMfg);
        var currentDllName = _window.ViewModel.GetRtx40MfgInstalledAs(gameName, store);
        bool isPluginInstall = isInstalled && Rtx40MfgService.IsPluginInstall(currentDllName); // loaded by OptiScaler
        bool isHsfMods       = isInstalled && Rtx40MfgService.IsHsfModsInstall(currentDllName); // loaded by HSF's helper
        bool hasHsf          = !string.IsNullOrEmpty(installPath)
            && (HsfService.IsInstalledIn(installPath, false) || HsfService.IsInstalledIn(installPath, true));

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

        if (blocker is { } mfgBlocker)
        {
            ApplyExtraBlock(installBtn, label, ExtraKind.UniversalMfg, mfgBlocker);
        }
        else
        {
            // Cheap check for the tooltip only; the click handler checks OptiScaler's install record
            bool osPluginLikely = card.IsOsInstalled && !string.IsNullOrEmpty(installPath)
                && System.IO.Directory.Exists(System.IO.Path.Combine(installPath, "OptiScaler"));
            ToolTipService.SetToolTip(installBtn,
                isHsfMods ? Loc.GetString("Extras.Rtx40Mfg.Tooltip.HsfModsReinstall")
                : isPluginInstall ? Loc.GetString("Extras.Rtx40Mfg.Tooltip.PluginReinstall", currentDllName)
                : osPluginLikely ? (isInstalled
                    ? Loc.GetString("Extras.Rtx40Mfg.Tooltip.PluginMoveReinstall", currentDllName)
                    : Loc.GetString("Extras.Rtx40Mfg.Tooltip.PluginInstall"))
                : isInstalled ? Loc.GetString("Extras.Rtx40Mfg.Reinstall.Tooltip", currentDllName)
                : Loc.GetString("Extras.Rtx40Mfg.Install.Tooltip"));
        }

        installBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            await InstallUmfgCoreAsync(card, hasHsf, null, installBtn, installBtn.XamlRoot);
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog button
        var cogBtn = CogDialog.MakeCogButton(Loc.GetString("Extras.Rtx40Mfg.Settings.Tooltip"));
        cogBtn.Click += async (s, e) =>
        {
            try
            {
                var cog = new CogDialog(Loc.GetString("Extras.Rtx40Mfg.FullName"), _window.Content.XamlRoot);
                cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.Intro"));
                cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.ModeFollowGame"));
                cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.ModeFixed"));
                cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.ModeDynamic"));
                cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.PresetNote"));
                if (isInstalled)
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.CurrentlyInstalled", currentDllName));
                if (isInstalled && Rtx40MfgService.IsBinkName(currentDllName))
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.BinkNote", currentDllName, System.IO.Path.GetFileNameWithoutExtension(currentDllName)));

                // Stable OptiScaler (0.9) beside a proxy install: the mod doesn't support it
                if (isInstalled && !isPluginInstall && !isHsfMods && card.IsOsInstalled && !string.IsNullOrEmpty(installPath)
                    && !System.IO.Directory.Exists(System.IO.Path.Combine(installPath, "OptiScaler")))
                    cog.AddWarning(Loc.GetString("Extras.Rtx40Mfg.Settings.StableOsWarning"));

                if (isPluginInstall)
                {
                    cog.AddSection(Loc.GetString("Extras.Rtx40Mfg.Settings.OsSection"));
                    cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.OsIntro"));
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.OsNote"));
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.OsRtx30Note"));
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.OsXefgNote"));
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.OsFrameLimitNote"));
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.OsXemfgNote"));
                    cog.AddButtons(cog.MakeButton(Loc.GetString("Extras.Rtx40Mfg.Settings.OsAuthorBuild.Button"), CogButtonKind.Secondary,
                        async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dashdogy/OptiScaler/releases")); },
                        Loc.GetString("Extras.Rtx40Mfg.Settings.OsAuthorBuild.Tooltip")));
                }

                if (isHsfMods || hasHsf)
                {
                    cog.AddSection(Loc.GetString("Extras.Rtx40Mfg.Settings.HsfSection"));
                    cog.AddIntro(isHsfMods
                        ? Loc.GetString("Extras.Rtx40Mfg.Settings.HsfModsIntro")
                        : Loc.GetString("Extras.Rtx40Mfg.Settings.HsfIntro"));
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.HsfNoXessNote"));
                }

                cog.AddSection(Loc.GetString("Extras.Rtx40Mfg.Settings.XessSection"));
                if (isHsfMods || hasHsf)
                    cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.XessHsfIntro"));
                else if (isPluginInstall)
                    cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.XessViaOs"));
                else
                {
                    cog.AddIntro(Loc.GetString("Extras.Rtx40Mfg.Settings.XessIntro"));
                    cog.AddNote(Loc.GetString("Extras.Rtx40Mfg.Settings.XessFilesNote"));
                }
                if (isInstalled && !isPluginInstall && !isHsfMods && !hasHsf && !string.IsNullOrEmpty(installPath))
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

                AddUmfgRtxHairSection(cog, card, mfgSvc, isInstalled, isHsfMods);
                AddUmfgDlss5Section(cog, card, mfgSvc, isInstalled, isHsfMods);

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
        removeBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            await UninstallUmfgCoreAsync(card, currentDllName, removeBtn);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    /// <summary>
    /// The Universal MFG Unlocker install button's work (shared with Tune presets): anti-cheat confirm, the HSF mods
    /// case, the ReShade warning, then OptiScaler plugin placement or the name picker (skipped when
    /// <paramref name="fixedName"/> is given). <paramref name="installBtn"/> is the row's button (null from Tune).
    /// Returns null on success, "Cancelled", or an error.
    /// </summary>
    private async Task<string?> InstallUmfgCoreAsync(GameCardViewModel card, bool hasHsf, string? fixedName,
        Button? installBtn, XamlRoot? xamlRoot)
    {
        var mfgSvc      = App.Services.GetRequiredService<Rtx40MfgService>();
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";
        var vm          = _window.ViewModel;
        void Busy() { if (installBtn != null) { installBtn.IsEnabled = false; installBtn.Content = Loc.GetString("Status.Installing"); } }

        // With an OptiScaler 10 build installed by RHI the mod goes in OptiScaler\plugins (no name to pick).
        // Read the live location too: it can have moved since this panel was built.
        bool asPlugin    = await Task.Run(() => vm.IsOptiScalerPluginHost(card));
        var liveDllName  = vm.GetRtx40MfgInstalledAs(gameName, store);
        bool liveInstalled = !string.IsNullOrEmpty(liveDllName) && mfgSvc.IsInstalledIn(installPath, liveDllName);

        // Games on RHI's anti-cheat list: warn before a new install (not on reinstall / update)
        if (!liveInstalled && !await ConfirmAntiCheatAsync(card, "Universal MFG Unlocker",
                AntiCheatNoticeKey.UniversalMfg, xamlRoot))
            return TuneCancelled;

        // Installed for HSF's helper (HSF_streamline\mods), or a 32-bit game with HSF (HSF's guide: never next to
        // a 32-bit exe): into HSF_streamline\mods, no name to pick
        if ((liveInstalled && Rtx40MfgService.IsHsfModsInstall(liveDllName)) || (hasHsf && card.Is32Bit))
        {
            Busy();
            var hsfError = await vm.InstallMfgForHsfAsync(card, null);
            if (hsfError != null) CrashReporter.Log($"[BuildRtx40MfgRow] HSF mods reinstall failed — {hsfError}");
            RequestExtrasRebuild(card);
            return hsfError;
        }

        // XeMFG standalone can't start while ReShade hooks DXGI (the mod reports menu_factory_not_dxgi).
        // Through OptiScaler it works, so no warning there.
        if (!asPlugin && card.IsRsInstalled && !card.RequiresVulkanInstall
            && !vm.Settings.UmfgReShadeWarningDismissed
            && !await ShowUmfgReShadeWarningAsync(xamlRoot))
            return TuneCancelled;

        string? chosen;
        if (asPlugin)
            chosen = Rtx40MfgService.PluginRelPath;
        else
        {
            chosen = fixedName ?? await ShowMfgDllPickerAsync(card, Rtx40MfgService.IsPluginInstall(liveDllName) ? null : liveDllName);
            if (chosen == null) return TuneCancelled;
        }

        Busy();
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
            {
                var oldName = liveDllName;
                await Task.Run(() => mfgSvc.Uninstall(installPath, oldName)); // hashes RHI's RTX Hair files
            }

            bool ok = await Task.Run(async () =>
            {
                if (!mfgSvc.IsStagingReady || mfgSvc.HasUpdate)
                    await mfgSvc.EnsureStagingAsync().ConfigureAwait(false);
                if (!asPlugin) // standalone XeMFG; through OptiScaler it uses OptiScaler's XeSS files
                    await mfgSvc.EnsureXessStagingAsync().ConfigureAwait(false);
                // dxcompiler.dll / dxil.dll for RTX Hair (DirectX 12 games on RTX 30 / 40)
                await mfgSvc.PrepareRtxHairStagingAsync(installPath, chosen).ConfigureAwait(false);
                return mfgSvc.Install(installPath, chosen);
            });

            if (ok)
            {
                _window.ViewModel.SetRtx40MfgInstalledAs(gameName, chosen, store);
                card.SetMfgState(mfgAdaInstalled: card.MfgAdaInstalled, mfgAdaRtx40Conflict: card.MfgAdaInstalled, rtx40MfgInstalled: true);
                RequestExtrasRebuild(card);
                return null;
            }
            if (installBtn != null)
            {
                installBtn.Content   = Loc.GetString("Extras.DownloadFailedRetry.Button");
                installBtn.IsEnabled = true;
            }
            return Loc.GetString("Extras.Rtx40Mfg.Download.Error");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[BuildRtx40MfgRow] Install failed — {ex.Message}");
            if (installBtn != null)
            {
                installBtn.Content   = Loc.GetString("Extras.InstallFailed.Button");
                installBtn.IsEnabled = true;
            }
            return Loc.GetString("Extras.Rtx40Mfg.Install.ErrorDetail", ex.Message);
        }
    }

    /// <summary>The Universal MFG Unlocker remove button's work (shared with Tune presets). Returns null, or the error.</summary>
    private async Task<string?> UninstallUmfgCoreAsync(GameCardViewModel card, string? fallbackDllName, Button? removeBtn)
    {
        var mfgSvc      = App.Services.GetRequiredService<Rtx40MfgService>();
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";
        try
        {
            if (removeBtn != null) removeBtn.IsEnabled = false;
            // Live location: it may have moved in or out of OptiScaler\plugins since the panel was built
            var liveName = _window.ViewModel.GetRtx40MfgInstalledAs(gameName, store) ?? fallbackDllName;
            await Task.Run(() =>
            {
                mfgSvc.Uninstall(installPath, liveName); // hashes RHI's RTX Hair files
                mfgSvc.RemoveNrDll(installPath);          // the DLSS 5 NR DLL, if the mod installed it
            });
            _window.ViewModel.SetRtx40MfgInstalledAs(gameName, null, store);
            card.SetMfgState(mfgAdaInstalled: card.MfgAdaInstalled, mfgAdaRtx40Conflict: false, rtx40MfgInstalled: false);
            RequestExtrasRebuild(card);
            return null;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[BuildRtx40MfgRow] Remove failed — {ex.Message}");
            if (removeBtn != null) removeBtn.IsEnabled = true;
            return Loc.GetString("Extras.Rtx40Mfg.Remove.ErrorDetail", ex.Message);
        }
    }

    private async Task<string?> ShowMfgDllPickerAsync(GameCardViewModel card, string? currentDllName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return null;

        string? chosen = null;

        var listPanel    = new StackPanel { Spacing = 4 };
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
            Content   = listPanel,
        };

        // Bink names keep the game's own Bink DLL as <name>Hooked.dll, so they get no ASI Loader hooked option, and
        // they need the game's Bink DLL: without one they're disabled
        var installDir = card.InstallPath;
        var binkMissing = await Task.Run(() => MfgDllNames.Where(Rtx40MfgService.IsBinkName)
            .Where(n => !string.Equals(n, currentDllName, StringComparison.OrdinalIgnoreCase)
                && (!File.Exists(Path.Combine(installDir, n))
                    || File.Exists(Rtx40MfgService.BinkHookedPath(Path.Combine(installDir, n))))) // already another mod's Bink slot
            .ToHashSet(StringComparer.OrdinalIgnoreCase));
        foreach (var option in GetExtraDllOptions(card, MfgDllNames, currentDllName, noHooked: Rtx40MfgService.IsBinkName))
        {
            var name            = option.Name;
            bool isRecommended  = option.IsRecommended;
            bool noGameBink     = option.BlockedBy == null && binkMissing.Contains(name);
            bool isRhiOwned     = option.BlockedBy != null || noGameBink;
            bool isDxgiConflict = string.Equals(name, "dxgi.dll", StringComparison.OrdinalIgnoreCase)
                               && !string.IsNullOrEmpty(card.RsInstalledFile);
            bool isCurrent      = option.IsCurrent;

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
            AddExtraDllBadges(contentRow, btn, option, Loc.GetString("Extras.Rtx40Mfg.FullName"));
            if (!isRhiOwned && isDxgiConflict)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.MayConflict"), "#2A1A10", "#CC9955", "#5A3A18"));
            if (isCurrent)
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Badge.Current"), "#182840", "#7AACDD", "#2A4468"));

            btn.Content = contentRow;

            if (noGameBink)
            {
                contentRow.Children.Add(MakeBadge(Loc.GetString("Extras.Rtx40Mfg.Badge.NoGameBink"), "#2A1818", "#CC6666", "#5A2828"));
                ToolTipService.SetToolTip(btn, File.Exists(Rtx40MfgService.BinkHookedPath(Path.Combine(installDir, name)))
                    ? Loc.GetString("Extras.Rtx40Mfg.BinkTaken.Tooltip", System.IO.Path.GetFileNameWithoutExtension(name))
                    : Loc.GetString("Extras.Rtx40Mfg.NoBinkDll.Tooltip", name, System.IO.Path.GetFileNameWithoutExtension(name)));
            }
            else if (!isRhiOwned && Rtx40MfgService.IsBinkName(name) && !isCurrent)
                ToolTipService.SetToolTip(btn, Loc.GetString("Extras.Rtx40Mfg.BinkRename.Tooltip", name, System.IO.Path.GetFileNameWithoutExtension(name)));
            if (!isRhiOwned && isDxgiConflict)
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
