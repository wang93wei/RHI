// SettingsHandler.OsCustomBuilds.cs -- "Custom OptiScaler builds" card at the end of Settings › Components:
// add any OptiScaler build from a download link (GitHub, Discord, anywhere), an archive, a folder or an
// OptiScaler.dll, then pick it per game in the OptiScaler ⚙ or as the Default version. Each build is a
// fixed copy RHI never updates (OsCustomBuildService). Built in C# (XAML constraint), inserted the first
// time Settings opens. Plan: docs/custom-optiscaler-builds-plan.md.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private StackPanel? _osCustomList;
    private TextBlock? _osCustomStatus;
    private TextBox? _osCustomLinkBox;
    private bool _osCustomBusy;
    private bool _osCustomCardFailed;

    private void EnsureOsCustomBuildsCard()
    {
        if (_osCustomList != null) { RefreshOsCustomBuildsCard(); return; }
        if (_osCustomCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Components") { section = sp; break; }
            if (section == null)
            {
                _osCustomCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureOsCustomBuildsCard] Components section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 10 };
            card.Child = body;

            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = Loc.GetString("Settings.OsBuild.Title"), Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.OsBuild.Description"),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            body.Children.Add(head);

            _osCustomList = new StackPanel { Spacing = 8 };
            body.Children.Add(_osCustomList);

            // Link row: [ paste a link ............ ] [Add link]
            var linkRow = new Grid { ColumnSpacing = 8 };
            linkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _osCustomLinkBox = new TextBox
            {
                PlaceholderText = Loc.GetString("Settings.OsBuild.LinkPlaceholder"),
                Style = FindStyle("SettingsTextBoxStyle"),
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_osCustomLinkBox, Loc.GetString("Settings.OsBuild.LinkAutomationName"));
            var addLink = MakeCustomFoldersButton(Loc.GetString("Settings.OsBuild.AddLink"), primary: true,
                Loc.GetString("Settings.OsBuild.AddLinkTooltip"));
            addLink.Click += async (s, e) =>
            {
                var link = _osCustomLinkBox.Text.Trim();
                if (link.Length == 0) { SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Error.PasteLinkFirst"), error: true); return; }
                if (!OsCustomBuildService.IsUrl(link, out _)) { SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Error.NotAWebLink"), error: true); return; }
                if (await AddOsCustomBuildAsync(link, replace: null)) _osCustomLinkBox.Text = "";
            };
            _osCustomLinkBox.KeyDown += (s, e) =>
            {
                if (e.Key != Windows.System.VirtualKey.Enter) return;
                e.Handled = true;
                // Buttons have no click helper: press it through its automation peer
                (Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(addLink)
                    as Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer)?.Invoke();
            };
            linkRow.Children.Add(_osCustomLinkBox);
            Grid.SetColumn(addLink, 1);
            linkRow.Children.Add(addLink);
            body.Children.Add(linkRow);

            var buttons = new WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            var addFile = MakeCustomFoldersButton(Loc.GetString("Settings.OsBuild.AddFile"), primary: false,
                Loc.GetString("Settings.OsBuild.AddFileTooltip"));
            addFile.Click += async (s, e) =>
            {
                var file = await PickOsCustomFileAsync();
                if (file != null) await AddOsCustomBuildAsync(file, replace: null);
            };
            buttons.Children.Add(addFile);
            var addFolder = MakeCustomFoldersButton(Loc.GetString("Settings.OsBuild.AddFolder"), primary: false,
                Loc.GetString("Settings.OsBuild.AddFolderTooltip"));
            addFolder.Click += async (s, e) =>
            {
                var folder = await _window.PickFolderAsync();
                if (!string.IsNullOrEmpty(folder)) await AddOsCustomBuildAsync(folder, replace: null);
            };
            buttons.Children.Add(addFolder);
            body.Children.Add(buttons);

            _osCustomStatus = new TextBlock
            {
                Style = FindStyle("SettingsNoteStyle"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            body.Children.Add(_osCustomStatus);

            section.Children.Add(card);
            OsCustomBuildService.Changed += () => _window.DispatcherQueue.TryEnqueue(RefreshOsCustomBuildsCard);
            RefreshOsCustomBuildsCard();
        }
        catch (Exception ex)
        {
            _osCustomCardFailed = true;
            CrashReporter.Log($"[SettingsHandler.EnsureOsCustomBuildsCard] Failed — {ex.Message}");
        }
    }

    /// <summary>Installed games whose OptiScaler version is this build.</summary>
    private List<GameCardViewModel> GamesUsingOsBuild(OsCustomBuild build) =>
        ViewModel.AllCards
            .Where(c => c.IsOsInstalled && ViewModel.GetOsVariant(c.GameName, c.Source ?? "") == build.Id)
            .ToList();

    private void RefreshOsCustomBuildsCard()
    {
        if (_osCustomList == null) return;
        try
        {
            _osCustomList.Children.Clear();
            var builds = OsCustomBuildService.All;
            if (builds.Count == 0)
            {
                _osCustomList.Children.Add(new TextBlock { Text = Loc.GetString("Settings.OsBuild.NoBuilds"), Style = FindStyle("SettingsNoteStyle") });
                return;
            }
            foreach (var build in builds)
                _osCustomList.Children.Add(MakeOsCustomRow(build));
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.RefreshOsCustomBuildsCard] {ex.Message}");
        }
    }

    private FrameworkElement MakeOsCustomRow(OsCustomBuild build)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = build.Name,
            Style = FindStyle("SettingsLabelStyle"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        int using_ = GamesUsingOsBuild(build).Count;
        var facts = new List<string>();
        if (!string.IsNullOrWhiteSpace(build.DllVersion)) facts.Add(build.DllVersion!);
        facts.Add(build.IsOs10 ? Loc.GetString("Settings.OsBuild.Fact.Os10Layout") : Loc.GetString("Settings.OsBuild.Fact.FlatLayout"));
        facts.Add(Loc.GetString("Settings.OsBuild.Fact.Added", build.AddedUtc.ToLocalTime().ToString("d MMM yyyy", System.Globalization.CultureInfo.CurrentCulture)));
        if (using_ > 0) facts.Add(using_ == 1 ? Loc.GetString("Settings.OsBuild.Fact.UsedByOne") : Loc.GetString("Settings.OsBuild.Fact.UsedByMany", using_));
        var detail = new TextBlock
        {
            Text = string.Join(" · ", facts),
            Style = FindStyle("SettingsHelpStyle"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTipService.SetToolTip(detail, Loc.GetString("Settings.OsBuild.Tooltip.Origin", build.Origin, build.Dir)
            + (build.LooksLikeOptiScaler ? "" : Loc.GetString("Settings.OsBuild.Tooltip.NotOptiScaler")));
        text.Children.Add(detail);
        row.Children.Add(text);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var nr = new CheckBox
        {
            Content = Loc.GetString("Settings.OsBuild.NeedsNrDll"),
            IsChecked = build.NeedsNrDll,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(nr, Loc.GetString("Settings.OsBuild.NeedsNrDllTooltip"));
        nr.Click += (s, e) =>
        {
            if (!OsCustomBuildService.SetNeedsNrDll(build.Slug, nr.IsChecked == true))
                SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Error.SaveFailed"), error: true);
        };
        actions.Children.Add(nr);

        var replace = MakeCustomFoldersButton(Loc.GetString("Dialog.OptiScaler.Replace"), primary: false,
            Loc.GetString("Settings.OsBuild.ReplaceTooltip"));
        var flyout = new MenuFlyout();
        var fromLink = new MenuFlyoutItem { Text = Loc.GetString("Settings.OsBuild.Replace.FromLink") };
        fromLink.Click += async (s, e) =>
        {
            var link = _osCustomLinkBox?.Text.Trim() ?? "";
            if (!OsCustomBuildService.IsUrl(link, out _)) { SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Error.PasteNewLinkFirst"), error: true); return; }
            if (await AddOsCustomBuildAsync(link, replace: build) && _osCustomLinkBox != null) _osCustomLinkBox.Text = "";
        };
        var fromFile = new MenuFlyoutItem { Text = Loc.GetString("Settings.OsBuild.Replace.FromFile") };
        fromFile.Click += async (s, e) =>
        {
            var file = await PickOsCustomFileAsync();
            if (file != null) await AddOsCustomBuildAsync(file, replace: build);
        };
        var fromFolder = new MenuFlyoutItem { Text = Loc.GetString("Settings.OsBuild.Replace.FromFolder") };
        fromFolder.Click += async (s, e) =>
        {
            var folder = await _window.PickFolderAsync();
            if (!string.IsNullOrEmpty(folder)) await AddOsCustomBuildAsync(folder, replace: build);
        };
        flyout.Items.Add(fromLink);
        flyout.Items.Add(fromFile);
        flyout.Items.Add(fromFolder);
        replace.Flyout = flyout;
        actions.Children.Add(replace);

        var rename = MakeCustomFoldersButton(Loc.GetString("Dialog.OptiScaler.Rename"), primary: false, Loc.GetString("Settings.OsBuild.RenameTooltip"));
        rename.Click += async (s, e) =>
        {
            var name = await AskOsCustomNameAsync(Loc.GetString("Settings.OsBuild.RenameTitle"), build.Name);
            if (name == null || name == build.Name) return;
            if (!OsCustomBuildService.Rename(build.Slug, name)) SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Error.RenameFailed"), error: true);
        };
        actions.Children.Add(rename);

        var remove = MakeCustomFoldersButton(Loc.GetString("Dialog.Remove"), primary: false, Loc.GetString("Settings.OsBuild.RemoveTooltip"));
        remove.Click += async (s, e) => await RemoveOsCustomBuildAsync(build);
        actions.Children.Add(remove);

        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        return row;
    }

    private void SetOsCustomStatus(string text, bool error)
    {
        if (_osCustomStatus == null) return;
        _osCustomStatus.Text = text;
        _osCustomStatus.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextSecondaryBrush);
        _osCustomStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task<string?> PickOsCustomFileAsync()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        return await Task.Run(() =>
        {
            var ofn = new NativeInterop.OpenFileName();
            ofn.structSize = System.Runtime.InteropServices.Marshal.SizeOf(ofn);
            ofn.hwndOwner = hwnd;
            ofn.filter = Loc.GetString("Settings.OsBuild.FileFilterBuilds") + "\0*.zip;*.7z;*.rar;*.dll\0" + Loc.GetString("Settings.OsBuild.FileFilterAll") + "\0*.*\0";
            ofn.file = new string(new char[260]);
            ofn.maxFile = ofn.file.Length;
            ofn.title = Loc.GetString("Settings.OsBuild.ChooseFileTitle");
            ofn.flags = 0x00080000 | 0x00001000; // OFN_EXPLORER | OFN_FILEMUSTEXIST
            return NativeInterop.GetOpenFileName(ref ofn) ? ofn.file.TrimEnd('\0') : null;
        });
    }

    private Task<string?> AskOsCustomNameAsync(string title, string suggested) =>
        OsCustomBuildDialogs.AskNameAsync(_window.Content.XamlRoot, title, suggested);

    /// <summary>Adds (or, with <paramref name="replace"/>, replaces) a build. True when it worked.</summary>
    private async Task<bool> AddOsCustomBuildAsync(string source, OsCustomBuild? replace)
    {
        if (_osCustomBusy) { SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Status.Busy"), error: false); return false; }
        string name;
        if (replace != null) name = replace.Name;
        else
        {
            var asked = await AskOsCustomNameAsync(Loc.GetString("Settings.OsBuild.NameTitle"), OsCustomBuildService.SuggestName(source));
            if (asked == null) return false;
            name = asked;
        }

        _osCustomBusy = true;
        try
        {
            var progress = new Progress<string>(m => SetOsCustomStatus(m, error: false));
            SetOsCustomStatus(replace != null ? Loc.GetString("Settings.OsBuild.Status.Replacing", replace.Name) : Loc.GetString("Settings.OsBuild.Status.Adding", name), error: false);
            var result = await Task.Run(() => OsCustomBuildService.ImportAsync(source, name, replace?.Slug, progress, _window.LifetimeToken));
            if (result.Build == null)
            {
                SetOsCustomStatus(result.Error ?? Loc.GetString("Settings.OsBuild.Error.AddFallback"), error: true);
                return false;
            }
            var done = replace != null ? Loc.GetString("Settings.OsBuild.Status.Replaced", result.Build.Name) : Loc.GetString("Settings.OsBuild.Status.Added", result.Build.Name);
            SetOsCustomStatus(result.Note != null ? done + " " + result.Note : done, error: result.Note != null);
            if (replace != null) await OfferOsCustomRedeployAsync(result.Build);
            return true;
        }
        finally { _osCustomBusy = false; }
    }

    /// <summary>After a build is replaced: offer to put the new files into the games that use it.</summary>
    private async Task OfferOsCustomRedeployAsync(OsCustomBuild build)
    {
        var games = GamesUsingOsBuild(build).Where(c => !c.IsHidden && !string.IsNullOrEmpty(c.InstallPath)).ToList();
        if (games.Count == 0) return;
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Settings.OsBuild.RedeployTitle"),
            Content = new TextBlock
            {
                Text = games.Count == 1
                     ? Loc.GetString("Settings.OsBuild.Redeploy.One", games[0].GameName, build.Name)
                     : Loc.GetString("Settings.OsBuild.Redeploy.Many", games.Count, build.Name),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = Loc.GetString("Status.UpdateShort"),
            CloseButtonText = Loc.GetString("Settings.OsBuild.NotNow"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _window.Content.XamlRoot,
        };
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return;
        var os = App.Services.GetRequiredService<IOptiScalerService>();
        int ok = 0;
        foreach (var card in games)
        {
            SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Status.UpdatingGame", card.GameName), error: false);
            try
            {
                await os.UpdateAsync(card, variantHint: build.Id);
                ok++;
            }
            catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.OsCustomRedeploy] {card.GameName}: {ex.Message}"); }
        }
        ViewModel.SaveLibraryPublic();
        SetOsCustomStatus(ok == games.Count
                ? (ok == 1 ? Loc.GetString("Settings.OsBuild.Status.UpdatedOne", ok) : Loc.GetString("Settings.OsBuild.Status.UpdatedMany", ok))
                : Loc.GetString("Settings.OsBuild.Status.UpdatedPartial", ok, games.Count),
            error: ok != games.Count);
    }

    private async Task RemoveOsCustomBuildAsync(OsCustomBuild build)
    {
        var games = GamesUsingOsBuild(build);
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Settings.OsBuild.RemoveTitle", build.Name),
            Content = new TextBlock
            {
                Text = games.Count == 0
                    ? Loc.GetString("Settings.OsBuild.RemoveNoGames")
                    : (games.Count == 1 ? Loc.GetString("Settings.OsBuild.RemoveOneGame", games.Count) : Loc.GetString("Settings.OsBuild.RemoveManyGames", games.Count)),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = Loc.GetString("Dialog.Remove"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _window.Content.XamlRoot,
        };
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return;
        // The Default version can't point at a build that's gone
        if (ViewModel.Settings.OsDefaultVariant == build.Id) ViewModel.SetOsDefaultVariant(OsVariants.Stable);
        if (!OsCustomBuildService.Remove(build.Slug)) SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Error.DeleteFailed"), error: true);
        else SetOsCustomStatus(Loc.GetString("Settings.OsBuild.Status.Removed", build.Name), error: false);
    }
}
