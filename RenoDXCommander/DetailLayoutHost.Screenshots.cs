// DetailLayoutHost.Screenshots.cs -- Spotlight's Screenshots panel (the picture button beside the game's title):
// the game's ReShade screenshots as thumbnails, newest first, in the slide-in panel. Click = open in the default
// viewer; right-click = open, set as banner, copy, show in folder, delete (Recycle Bin). Same folder rules as the
// RHI overlay (ScreenshotLibraryService). Choose folder sets a per-game folder (other screenshot tools); Use default
// clears it.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

internal sealed partial class DetailLayoutHost
{
    internal const string ScreenshotsKey = "Screenshots";
    private const int ShotPage = 30;
    private const double ShotThumbWidth = 240;

    private StackPanel? _shotsRoot;
    private int _shotsSeq;

    /// <summary>The panel's content (OpenPanel). Fills in once the folder has been read (background).</summary>
    private FrameworkElement? BuildScreenshotsPanel()
    {
        if (_window.ViewModel.SelectedGame is not { } card) return null;
        _shotsRoot = new StackPanel { Spacing = 14 };
        LoadScreenshots(card);
        return _shotsRoot;
    }

    /// <summary>(Re)fills <see cref="_shotsRoot"/>: reads the folder in the background, then renders.</summary>
    private void LoadScreenshots(GameCardViewModel card)
    {
        if (_shotsRoot == null) return;
        _shotsRoot.Children.Clear();
        _shotsRoot.Children.Add(MutedText(Loc.GetString("Detail.Screenshots.Loading")));
        int seq = ++_shotsSeq;

        var rhiFolder = _window.ViewModel.BuildScreenshotSavePath(card.GameName);
        bool perGame = _window.ViewModel.Settings.PerGameScreenshotFolders;
        var custom = _window.ViewModel.GetScreenshotFolder(card.GameName, card.Source ?? "");
        var installPath = card.InstallPath;
        _ = Task.Run(() =>
        {
            string? folder = null;
            bool customMissing = false;
            List<FileInfo> shots = new();
            try
            {
                // A chosen folder that's gone is reported, not silently swapped for ReShade's
                customMissing = custom != null && !ScreenshotLibraryService.FolderExists(custom);
                folder = customMissing ? null : ScreenshotLibraryService.FindFolder(installPath, rhiFolder, custom);
                if (folder != null)
                {
                    // Other tools don't name files after the exe: show everything in the chosen folder
                    bool isCustom = custom != null && SameFolder(folder, custom);
                    bool own = isCustom || (perGame && rhiFolder != null && SameFolder(folder, rhiFolder)) || SameFolder(folder, installPath);
                    string? exe = isCustom || string.IsNullOrEmpty(installPath) ? null
                        : App.Services.GetRequiredService<IPeHeaderService>().FindGameExe(installPath);
                    shots = ScreenshotLibraryService.List(folder, exe, own);
                }
            }
            catch (Exception ex) { CrashReporter.Log($"[DetailLayoutHost.Screenshots] {card.GameName}: {ex.Message}"); }
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (seq == _shotsSeq && _panelKey == ScreenshotsKey) RenderShots(card, folder, shots, custom, customMissing);
            });
        });
    }

    /// <summary>Reloads the panel after the game's screenshot folder changed, if it's still showing that game.</summary>
    private void ReloadScreenshots(GameCardViewModel card)
    {
        if (_shotsRoot != null && _panelKey == ScreenshotsKey && ReferenceEquals(_window.ViewModel.SelectedGame, card))
            LoadScreenshots(card);
    }

    private async Task ChooseScreenshotFolderAsync(GameCardViewModel card, string? startAt)
    {
        try
        {
            var picked = await _window.PickFolderAsync(startAt);
            if (string.IsNullOrEmpty(picked)) return;
            _window.ViewModel.SetScreenshotFolder(card.GameName, picked, card.Source ?? "");
            ReloadScreenshots(card);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[DetailLayoutHost.ScreenshotsChooseFolder] {card.GameName}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void UseDefaultScreenshotFolder(GameCardViewModel card)
    {
        try
        {
            _window.ViewModel.SetScreenshotFolder(card.GameName, null, card.Source ?? "");
            ReloadScreenshots(card);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[DetailLayoutHost.ScreenshotsUseDefault] {card.GameName}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ReturnScreenshotsPanel()
    {
        _shotsSeq++;      // drop a load still running
        _shotsRoot = null;
    }

    private static bool SameFolder(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try { return Path.GetFullPath(a).TrimEnd('\\').Equals(Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static TextBlock MutedText(string text) => new()
    {
        Text = text,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        Foreground = TabsTheme.B(TabsTheme.TextMuted),
    };

    private void RenderShots(GameCardViewModel card, string? folder, List<FileInfo> shots, string? custom, bool customMissing)
    {
        if (_shotsRoot == null) return;
        _shotsRoot.Children.Clear();
        bool usingCustom = custom != null && !customMissing && folder != null && SameFolder(folder, custom);

        // Summary card: how many, where, Open folder / Choose folder / Use default
        var head = new StackPanel { Spacing = 8 };
        head.Children.Add(new TextBlock
        {
            Text = folder == null ? Loc.GetString("Overlay.Screenshots.Empty")
                 : shots.Count == 0 ? Loc.GetString("Overlay.Screenshots.Empty")
                 : shots.Count == 1 ? Loc.GetString("Overlay.Screenshots.One") : Loc.GetString("Overlay.Screenshots.Count", shots.Count),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TabsTheme.B(TabsTheme.TextPrimary),
        });
        if (customMissing)
            head.Children.Add(MutedText(Loc.GetString("Detail.Screenshots.CustomMissing", custom)));
        else if (folder != null)
        {
            if (usingCustom) head.Children.Add(MutedText(Loc.GetString("Detail.Screenshots.CustomFolder")));
            head.Children.Add(MutedText(folder));
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        Button AddButton(string text, string? tip, Action click)
        {
            var b = new Button { Content = text };
            TabsTheme.Apply(b, TabsTheme.IconButton, height: 34, fontSize: 12);
            if (tip != null) ToolTipService.SetToolTip(b, tip);
            b.Click += (_, _) => click();
            buttons.Children.Add(b);
            return b;
        }
        if (folder != null) AddButton(Loc.GetString("Detail.OpenFolder"), null, () => ShowInExplorer(folder, select: null));
        AddButton(Loc.GetString("Detail.Screenshots.ChooseFolder"),
            Loc.GetString("Detail.Screenshots.ChooseFolderTooltip"),
            () => _ = ChooseScreenshotFolderAsync(card, customMissing ? card.InstallPath : folder ?? card.InstallPath));
        if (custom != null)
            AddButton(Loc.GetString("Detail.Screenshots.UseDefault"), Loc.GetString("Detail.Screenshots.UseDefaultTooltip"), () => UseDefaultScreenshotFolder(card));
        head.Children.Add(buttons);

        if (customMissing)
            head.Children.Add(MutedText(Loc.GetString("Detail.Screenshots.ChooseAgainHint")));
        else if (shots.Count == 0)
            head.Children.Add(MutedText(usingCustom
                ? Loc.GetString("Detail.Screenshots.EmptyCustom")
                : folder == null
                ? Loc.GetString("Detail.Screenshots.EmptyNoFolder") + " " + Loc.GetString("Detail.Screenshots.OtherTool")
                : Loc.GetString("Detail.Screenshots.EmptyHint") + " " + Loc.GetString("Detail.Screenshots.OtherTool")));
        else
            head.Children.Add(MutedText(Loc.GetString("Detail.Screenshots.Intro")));
        _shotsRoot.Children.Add(TabsTheme.MakeCard(head, 16));
        if (shots.Count == 0) return;

        var grid = new Controls.AutoFitGrid { MinColumnWidth = ShotThumbWidth, ColumnSpacing = 10, RowSpacing = 10, StretchFewItems = false };
        _shotsRoot.Children.Add(grid);
        AddShotThumbs(card, grid, shots, 0);
    }

    /// <summary>Height / width of the game's monitor: screenshots have its shape.</summary>
    private double ScreenAspect()
    {
        try
        {
            var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(_window.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            var b = area.OuterBounds;
            if (b.Width > 0 && b.Height > 0) return (double)b.Height / b.Width;
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.COMException)
        {
            CrashReporter.Log($"[DetailLayoutHost.ScreenAspect] {ex.Message}");
        }
        return 9.0 / 16;
    }

    private void AddShotThumbs(GameCardViewModel card, Controls.AutoFitGrid grid, List<FileInfo> shots, int start)
    {
        double aspect = ScreenAspect();
        int decode = (int)Math.Ceiling(440 * Math.Max(1, grid.XamlRoot?.RasterizationScale ?? 1));
        int end = Math.Min(shots.Count, start + ShotPage);
        for (int i = start; i < end; i++)
            grid.Children.Add(MakeShotThumb(card, shots[i], aspect, decode));

        // "Show more" after the grid, replaced each time
        if (_shotsRoot!.Children.LastOrDefault() is Button { Tag: "ShotsMore" } old) _shotsRoot.Children.Remove(old);
        if (end < shots.Count)
        {
            var more = new Button { Content = Loc.GetString("Overlay.Screenshots.ShowMore", shots.Count - end), Tag = "ShotsMore", HorizontalAlignment = HorizontalAlignment.Left };
            TabsTheme.Apply(more, TabsTheme.IconButton, height: 36, fontSize: 13);
            more.Click += (_, _) => AddShotThumbs(card, grid, shots, end);
            _shotsRoot.Children.Add(more);
        }
    }

    private Button MakeShotThumb(GameCardViewModel card, FileInfo file, double aspect, int decode)
    {
        var image = new Image { Stretch = Stretch.UniformToFill };
        var fallback = new TextBlock
        {
            Text = Loc.GetString("Overlay.Screenshots.ThumbFallback", Path.GetExtension(file.Name).ToUpperInvariant().TrimStart('.')),
            FontSize = 12,
            Foreground = TabsTheme.B(TabsTheme.TextMuted),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        var bmp = new BitmapImage { DecodePixelWidth = decode };
        bmp.ImageFailed += (_, _) => fallback.Visibility = Visibility.Visible; // e.g. some HDR .jxr files
        try { bmp.UriSource = new Uri(file.FullName); image.Source = bmp; }
        catch (UriFormatException) { fallback.Visibility = Visibility.Visible; }

        var cell = new Grid { Height = Math.Round(ShotThumbWidth * aspect), Background = TabsTheme.B("#1A2130") };
        cell.SizeChanged += (_, e) =>
        {
            double h = Math.Round(e.NewSize.Width * aspect);
            if (e.NewSize.Width > 0 && Math.Abs(cell.Height - h) > 0.5) cell.Height = h;
        };
        cell.Children.Add(image);
        cell.Children.Add(fallback);

        var btn = new Button
        {
            Content = cell,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(1),
            BorderBrush = TabsTheme.B(TabsTheme.CardBorder),
            CornerRadius = new CornerRadius(8),
            Background = TabsTheme.B("#1A2130"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(btn, Loc.GetString("Detail.Screenshots.ThumbTooltip", file.Name, file.LastWriteTime.ToString("g")));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, Loc.GetString("Detail.Screenshots.ThumbAutomation", file.LastWriteTime.ToString("g")));
        btn.Click += (_, _) => OpenShotFile(file.FullName);
        btn.ContextFlyout = MakeShotMenu(card, file, btn);
        return btn;
    }

    private MenuFlyout MakeShotMenu(GameCardViewModel card, FileInfo file, Button thumb)
    {
        var menu = new MenuFlyout();
        void Add(string text, Action action, bool enabled = true)
        {
            var item = new MenuFlyoutItem { Text = text, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Add(Loc.GetString("Xaml.Open"), () => OpenShotFile(file.FullName));
        // HDR .jxr screenshots can't be shown as the banner
        bool bannerOk = !file.Extension.Equals(".jxr", StringComparison.OrdinalIgnoreCase);
        Add(bannerOk ? Loc.GetString("Detail.Screenshots.SetAsBanner") : Loc.GetString("Detail.Screenshots.SetAsBannerNoJxr"), () => _ = _window.SetBannerFromFileAsync(card, file.FullName), bannerOk);
        Add(Loc.GetString("Detail.Screenshots.Copy"), () => _ = CopyShotAsync(file.FullName));
        Add(Loc.GetString("Overlay.Screenshots.ShowInFolder"), () => ShowInExplorer(file.DirectoryName ?? "", select: file.FullName));
        menu.Items.Add(new MenuFlyoutSeparator());
        Add(Loc.GetString("Detail.Screenshots.Delete"), () =>
        {
            bool ok;
            try { ok = Overlay.OverlayNative.RecycleFile(file.FullName); }
            catch (Exception ex) { CrashReporter.Log($"[DetailLayoutHost.DeleteShot] {ex.Message}"); ok = false; }
            CrashReporter.Log($"[DetailLayoutHost.DeleteShot] {(ok ? "Moved to the Recycle Bin" : "Couldn't delete")}: {file.Name}");
            if (ok && thumb.Parent is Panel p) p.Children.Remove(thumb);
            else if (!ok) _window.ShowStatusBarMessage(Loc.GetString("Detail.Screenshots.DeleteFailed", file.Name), TimeSpan.FromSeconds(6));
        });
        return menu;
    }

    private static void OpenShotFile(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            CrashReporter.Log($"[DetailLayoutHost.OpenShotFile] {ex.Message}");
        }
    }

    private static void ShowInExplorer(string folder, string? select)
    {
        if (string.IsNullOrEmpty(folder)) return;
        try
        {
            var args = select != null ? $"/select,\"{select}\"" : $"\"{folder}\"";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            CrashReporter.Log($"[DetailLayoutHost.ShowInExplorer] {ex.Message}");
        }
    }

    private async Task CopyShotAsync(string path)
    {
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
            data.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file));
            data.SetStorageItems(new[] { file });
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            _window.ShowStatusBarMessage(Loc.GetString("Detail.Screenshots.Copied", Path.GetFileName(path)), TimeSpan.FromSeconds(4));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            CrashReporter.Log($"[DetailLayoutHost.CopyShot] {ex.Message}");
            _window.ShowStatusBarMessage(Loc.GetString("Detail.Screenshots.CopyFailed"), TimeSpan.FromSeconds(6));
        }
    }
}
