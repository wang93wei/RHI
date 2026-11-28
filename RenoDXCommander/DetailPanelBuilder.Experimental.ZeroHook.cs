// DetailPanelBuilder.Experimental.ZeroHook.cs — Experimental › Overlays and Hooks row for ZeroHook (GS-ZeroHook/ZeroHook):
// a third-party tool that blocks overlay hooks and background bloat machine-wide and shows which DLLs a running game
// has loaded. System-wide: the row is the same for every game (nothing depends on the card). RHI downloads the author's
// release to a fixed path (%LocalAppData%\RHI\zerohook, ZeroHook's Auto-Run task points at it) and opens it; ZeroHook
// itself does the blocking. Files / updates / removal through ZeroHookService.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private void BuildZeroHookRow(GameCardViewModel card, StackPanel body)
    {
        var svc          = App.Services.GetRequiredService<IZeroHookService>();
        bool downloaded  = svc.IsDownloaded;
        bool pulled      = FeatureFlags.IsExperimentalHidden(FeatureFlags.ExperimentalZeroHook);
        string? version  = downloaded ? svc.InstalledVersion : null;

        string statusText  = svc.IsUpdating ? Loc.GetString("Status.Updating")
                           : !downloaded ? Loc.GetString("Extras.ZeroHook.Status.NotDownloaded")
                           : version != null ? Loc.GetString("Extras.ZeroHook.Status.Version", version) : Loc.GetString("Extras.ZeroHook.Status.Downloaded");
        string statusColor = svc.IsUpdating ? "#E8C46A" : downloaded ? "#5ECB7D" : "#A0AABB";

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExperimentalContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        var label = new TextBlock
        {
            Text = "ZeroHook",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var tip = Loc.GetString("Extras.ZeroHook.Tooltip");
        ToolTipService.SetToolTip(label, tip);
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var statusBlock = new TextBlock
        {
            Text = statusText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UIFactory.GetBrush(statusColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
        };
        if (downloaded) ToolTipService.SetToolTip(statusBlock, Loc.GetString("Extras.ZeroHook.Downloaded.Tooltip", svc.ExePath));
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
        ToolTipService.SetToolTip(infoBtn, Loc.GetString("Extras.ZeroHook.Info.Tooltip"));
        infoBtn.Click += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(ZeroHookService.RepoUrl));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Taken out by the manifest: only shown when downloaded, and only to be removed
        bool btnEnabled = !pulled;
        var mainBtn = new Button
        {
            Content = pulled ? "⚠  " + Loc.GetString("Extras.ZeroHook.NotOffered") : downloaded ? Loc.GetString("Xaml.Open") : "⬇  " + Loc.GetString("Extras.ZeroHook.DownloadAndOpen"),
            FontSize = 12, Height = 32, HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(8),
            Background  = downloaded ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground  = downloaded ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = downloaded ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            IsEnabled = btnEnabled,
            Opacity = btnEnabled ? 1.0 : 0.35,
        };
        if (pulled)
        {
            string pulledText = Loc.GetString("Extras.ZeroHook.Pulled.Tooltip");
            ToolTipService.SetToolTip(mainBtn, pulledText);
            ToolTipService.SetToolTip(label, $"{tip}\n\n{pulledText}"); // disabled buttons show no tooltip
        }
        else
            ToolTipService.SetToolTip(mainBtn, downloaded
                ? Loc.GetString("Extras.ZeroHook.Open.Tooltip")
                : Loc.GetString("Extras.ZeroHook.Download.Tooltip"));

        mainBtn.Click += async (_, _) =>
        {
            var xamlRoot = mainBtn.XamlRoot ?? _window.Content.XamlRoot;
            var oldContent = mainBtn.Content;
            try
            {
                mainBtn.IsEnabled = false;
                if (!svc.IsDownloaded)
                {
                    mainBtn.Content = Loc.GetString("Status.Downloading");
                    // Created on the UI thread, so the reports come back on it
                    var progress = new Progress<double>(f => mainBtn.Content = Loc.GetString("Extras.ZeroHook.DownloadingProgress", f.ToString("P0")));
                    bool ok = await Task.Run(() => svc.EnsureDownloadedAsync(progress));
                    if (!ok)
                    {
                        mainBtn.Content = oldContent;
                        mainBtn.IsEnabled = true;
                        await ShowZeroHookMessageAsync(xamlRoot, Loc.GetString("Dialog.ZeroHook.DownloadFailed.Title"),
                            svc.LastError ?? Loc.GetString("Dialog.ZeroHook.DownloadFailed.Content"));
                        return;
                    }
                }
                mainBtn.Content = Loc.GetString("Extras.ZeroHook.Opening");
                var (result, error) = await Task.Run(() => svc.Launch());
                if ((result is ZeroHookLaunchResult.Failed or ZeroHookLaunchResult.NotDownloaded) && error != null)
                    await ShowZeroHookMessageAsync(xamlRoot, Loc.GetString("Dialog.ZeroHook.OpenFailed.Title"), error!);
                // Give it a moment to start, then show "Running" / the new version
                if (result == ZeroHookLaunchResult.Started) await Task.Delay(1500);
                RequestExperimentalRebuild(card);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or HttpRequestException or TaskCanceledException or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[BuildZeroHookRow] Download / open failed — {ex.Message}");
                mainBtn.Content = oldContent;
                mainBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(mainBtn, 3);
        row.Children.Add(mainBtn);

        // No ⚙: ZeroHook's settings live in ZeroHook (column 4 stays empty)

        var removeBtn = new Button
        {
            Width = 36, Height = 32, Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Opacity = downloaded ? 1.0 : 0,
            IsHitTestVisible = downloaded,
        };
        ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.ZeroHook.Remove.Tooltip"));
        removeBtn.Click += async (_, _) =>
        {
            var xamlRoot = removeBtn.XamlRoot ?? _window.Content.XamlRoot;
            try
            {
                var confirm = new ContentDialog
                {
                    Title = Loc.GetString("Dialog.ZeroHook.Remove.Title"),
                    Content = new TextBlock
                    {
                        Text = Loc.GetString("Dialog.ZeroHook.Remove.Content"),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 460,
                    },
                    PrimaryButtonText = Loc.GetString("Dialog.Remove"),
                    CloseButtonText = Loc.GetString("Dialog.Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = xamlRoot,
                    RequestedTheme = ElementTheme.Dark,
                };
                if (await DialogService.ShowSafeAsync(confirm) != ContentDialogResult.Primary) return;
                removeBtn.IsEnabled = false;
                var refusal = await Task.Run(() => svc.Remove());
                if (refusal != null)
                {
                    removeBtn.IsEnabled = true;
                    await ShowZeroHookMessageAsync(xamlRoot, Loc.GetString("Dialog.ZeroHook.RemoveFailed.Title"), refusal);
                    return;
                }
                RequestExperimentalRebuild(card);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[BuildZeroHookRow] Remove failed — {ex.Message}");
                removeBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);

        // "Running" needs a process scan: done off the UI thread every 2 s while the row is on screen, and the status
        // updated in place both ways (ZeroHook opened late behind its UAC prompt, or closed). The same TextBlock is
        // moved into the Tabs / Spotlight pill and Spotlight's slide-in panel, so this works in every layout.
        // Not Loaded / Unloaded: when Spotlight moves the section, the old place's Unloaded can arrive after the new
        // place's Loaded, which stopped the watch while the row was still shown (status stuck on "Running").
        if (downloaded && !svc.IsUpdating)
            _ = WatchZeroHookRunningAsync(svc, statusBlock, statusText);
    }

    /// <summary>
    /// Keeps the row's status at "Running" / its version while it's in the window (its parent chain reaches the
    /// window's content; checked live, not from Loaded / Unloaded events or XamlRoot, which can stay set). Stops once the
    /// TextBlock has been out of the window for 3 checks in a row (about 6 s: a rebuilt page made a new row, or
    /// the panel closed), which also covers the moment before it's first shown. Runs on the UI thread between scans.
    /// </summary>
    private async Task WatchZeroHookRunningAsync(IZeroHookService svc, TextBlock statusBlock, string idleText)
    {
        var token = _window.LifetimeToken;
        int away = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!IsInWindow(statusBlock))
                {
                    if (++away >= 3)
                    {
                        if (CrashReporter.VerboseLogging) CrashReporter.Log("[BuildZeroHookRow] Running watch stopped (row no longer shown)");
                        break;
                    }
                }
                else
                {
                    away = 0;
                    bool running = await Task.Run(svc.IsRunning);
                    if (token.IsCancellationRequested) break;
                    var text = running ? Loc.GetString("Detail.Running") : idleText;
                    if (statusBlock.Text != text) statusBlock.Text = text;
                }
                await Task.Delay(2000, token);
            }
        }
        catch (OperationCanceledException) { /* RHI closing */ }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            CrashReporter.Log($"[BuildZeroHookRow] Running check failed — {ex.Message}");
        }
    }

    /// <summary>True when <paramref name="el"/>'s parents lead up to the window's content right now.</summary>
    private bool IsInWindow(DependencyObject el)
    {
        var root = _window.Content;
        if (root == null) return false;
        for (var cur = el; cur != null; cur = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(cur))
            if (ReferenceEquals(cur, root)) return true;
        return false;
    }

    private static async Task ShowZeroHookMessageAsync(XamlRoot? xamlRoot, string title, string text)
    {
        if (xamlRoot == null) return;
        await DialogService.ShowSafeAsync(new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
            CloseButtonText = Loc.GetString("Dialog.Ok"),
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        });
    }
}
