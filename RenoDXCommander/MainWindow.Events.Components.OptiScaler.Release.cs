// MainWindow.Events.Components.OptiScaler.Release.cs -- two parts of the OptiScaler ⚙:
//   * Release: pin a game to a release of Stable / DLSS NR / F5 / a recommended fork (Nightly keeps its own
//     Nightly Build picker). Shares the per-game OsNightlyBuild value; staged by OsReleaseService.
//   * The version line at the top: installed vs latest, Check now (asks GitHub straight away) and Update.
// Asked for in issue #163 (no way to force a check or pick a version).

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private string LatestReleaseItem => Loc.GetString("Dialog.OptiScaler.NightlyLatest");

    // Release lists already fetched this session, per version (the ⚙ is reopened often)
    private static readonly Dictionary<string, List<OsRelease>> OsReleaseLists = new(StringComparer.Ordinal);

    /// <summary>Release cell: pin a release (only changeable while OptiScaler isn't installed on this game).</summary>
    private OsCell BuildOsReleaseCell(OsCogContext ctx, string variant, bool isInstalled)
    {
        var card = ctx.Card;
        var store = ctx.Store;
        var pinned = ViewModel.GetOsPinnedRelease(card.GameName, store);

        var items = new List<string> { LatestReleaseItem };
        if (pinned != null) items.Add(pinned);
        var combo = CogDialog.MakeCombo(items, pinned != null ? 1 : 0, isEnabled: !isInstalled);
        combo.Opacity = isInstalled ? CogDialog.DisabledOpacity : 1.0;
        var cell = new OsCell(Loc.GetString("OptiScaler.Release.Label"), isInstalled
            ? Loc.GetString("OptiScaler.Release.HelpInstalled")
            : Loc.GetString("OptiScaler.Release.Help"),
            combo, Ungated: true);

        bool filling = false;
        void Fill(List<OsRelease> releases)
        {
            filling = true;
            try
            {
                var tags = releases.Select(r => r.Tag).ToList();
                if (pinned != null && !tags.Contains(pinned)) tags.Insert(0, pinned);
                var list = new List<string> { LatestReleaseItem };
                list.AddRange(tags);
                combo.ItemsSource = list;
                combo.SelectedIndex = pinned != null ? 1 + tags.IndexOf(pinned) : 0;
            }
            finally { filling = false; }
        }

        if (OsReleaseLists.TryGetValue(variant, out var known)) Fill(known);
        else if (!isInstalled)
        {
            _ = Task.Run(async () =>
            {
                var releases = await OsReleaseService.ListAsync(variant).ConfigureAwait(false);
                if (releases == null) return;
                lock (OsReleaseLists) OsReleaseLists[variant] = releases;
                DispatcherQueue.TryEnqueue(() => Fill(releases));
            });
        }

        combo.SelectionChanged += async (s, ev) =>
        {
            if (filling || combo.SelectedItem is not string sel) return;
            try
            {
                if (sel == LatestReleaseItem)
                {
                    ViewModel.SetOsNightlyBuild(card.GameName, null, store);
                    CrashReporter.Log($"[OsCog] '{card.GameName}' follows the latest {variant} release");
                    return;
                }
                ViewModel.SetOsNightlyBuild(card.GameName, sel, store);
                CrashReporter.Log($"[OsCog] Pinned '{card.GameName}' to {variant} release {sel}");
                // Download it now so the install doesn't wait
                if (OsReleaseService.PinnedDir(variant, sel) == null)
                {
                    ctx.Cog.SetSuccess(Loc.GetString("OptiScaler.Release.Downloading", sel));
                    var dir = await Task.Run(() => OsReleaseService.EnsurePinnedAsync(variant, sel, null, LifetimeToken));
                    if (dir != null) ctx.Cog.SetSuccess(Loc.GetString("OptiScaler.Release.Ready", sel));
                    else ctx.Cog.SetError(Loc.GetString("OptiScaler.Release.DownloadFailed", sel));
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[OsCog] Release pin failed — {ex.Message}");
                ctx.Cog.SetError(Loc.GetString("OptiScaler.Release.PinFailed", ex.Message));
            }
        };
        return cell;
    }

    /// <summary>
    /// "Installed 0.1.29 · Latest 0.1.37" with Check now and Update. Checks once (cached) when the ⚙ opens;
    /// Check now asks GitHub again.
    /// </summary>
    private FrameworkElement BuildOsVersionLine(OsCogContext ctx, string variant, bool isInstalled)
    {
        var card = ctx.Card;
        var store = ctx.Store;
        var text = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
        };
        var check = CogDialog.StyledButton(Loc.GetString("OptiScaler.Release.CheckNow"), CogButtonKind.Secondary, compact: true);
        ToolTipService.SetToolTip(check, Loc.GetString("OptiScaler.Release.CheckNowTooltip"));
        var update = CogDialog.StyledButton(Loc.GetString("Status.UpdateShort"), CogButtonKind.Primary, compact: true);
        update.Tag = card; // the install handler reads the card from the sender's Tag
        update.Visibility = Visibility.Collapsed;
        ToolTipService.SetToolTip(update, Loc.GetString("OptiScaler.Release.UpdateTooltip"));

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(text);
        Grid.SetColumn(check, 1);
        row.Children.Add(check);
        Grid.SetColumn(update, 2);
        row.Children.Add(update);
        ctx.Ungated.Add(row);

        string installed = isInstalled ? (card.OsInstalledVersion ?? Loc.GetString("OptiScaler.Release.InstalledFallback")) : Loc.GetString("OptiScaler.Release.NotInstalled");
        var pinned = ViewModel.GetOsPinnedRelease(card.GameName, store);
        var nightlyPin = variant == OsVariants.Nightly ? ViewModel.GetOsNightlyBuild(card.GameName, store) : "";

        // Custom builds are never updated: no check
        if (OsVariants.IsCustom(variant))
        {
            text.Text = Loc.GetString("OptiScaler.Release.Line.Custom", installed);
            check.Visibility = Visibility.Collapsed;
            return row;
        }

        void Show(string? latest, bool checkedNow)
        {
            if (latest == null)
            {
                text.Text = checkedNow ? Loc.GetString("OptiScaler.Release.Line.CouldntCheckHint", installed) : Loc.GetString("OptiScaler.Release.Line.CouldntCheck", installed);
                update.Visibility = Visibility.Collapsed;
                return;
            }
            bool same = OsReleaseService.SameVersion(card.OsInstalledVersion, latest);
            string state;
            bool canUpdate = false;
            if (pinned != null || !string.IsNullOrEmpty(nightlyPin))
                state = Loc.GetString("OptiScaler.Release.State.Pinned", pinned ?? nightlyPin);
            else if (!isInstalled) state = Loc.GetString("OptiScaler.Release.State.Installs");
            else if (same) state = Loc.GetString("OptiScaler.Release.State.UpToDate");
            else { state = Loc.GetString("OptiScaler.Release.State.UpdateAvailable"); canUpdate = true; }
            text.Text = Loc.GetString("OptiScaler.Release.Line.Latest", installed, latest, state);
            update.Visibility = canUpdate ? Visibility.Visible : Visibility.Collapsed;
            if (canUpdate && checkedNow)
            {
                card.OsStatus = GameStatus.UpdateAvailable;
                ViewModel.NotifyUpdateButtonChanged();
            }
        }

        async Task<string?> FindLatestAsync(bool force)
        {
            if (variant == OsVariants.Nightly)
            {
                if (force || _optiScalerService.AvailableNightlyBuilds.Count == 0)
                    await _optiScalerService.FetchAvailableNightlyBuildsAsync();
                return _optiScalerService.AvailableNightlyBuilds.FirstOrDefault();
            }
            if (OsReleaseService.SourceFor(variant) is not { } src) return null;
            if (force)
                App.Services.GetRequiredService<GitHubETagCache>().Invalidate($"https://api.github.com/repos/{src.Repo}/releases?per_page=30");
            var releases = await Task.Run(() => OsReleaseService.ListAsync(variant));
            if (releases == null) return null;
            lock (OsReleaseLists) OsReleaseLists[variant] = releases;
            // Also set the service's own flag, so Update / Update All pick up the new release
            if (force && isInstalled)
            {
                switch (variant)
                {
                    case OsVariants.Stable: await _optiScalerService.CheckForUpdateAsync(); break;
                    case OsVariants.DlssNr: await _optiScalerService.CheckForDlssNrUpdateAsync(); break;
                    case OsVariants.F5: await _optiScalerService.CheckForF5UpdateAsync(); break;
                    default: if (OsVariants.IsFork(variant)) await OsForkService.CheckForUpdateAsync(variant); break;
                }
            }
            return releases.FirstOrDefault()?.Tag;
        }

        text.Text = Loc.GetString("OptiScaler.Release.Line.Checking", installed);
        _ = LoadAsync(false);
        async Task LoadAsync(bool force)
        {
            try
            {
                check.IsEnabled = false;
                if (force) text.Text = Loc.GetString("OptiScaler.Release.Line.Checking", installed);
                var latest = await FindLatestAsync(force);
                Show(latest, force);
                CrashReporter.Log($"[OsCog] {card.GameName} {variant}: installed {card.OsInstalledVersion ?? "-"}, latest {latest ?? "?"}{(force ? " (Check now)" : "")}");
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[OsCog] Version check failed — {ex.Message}");
                Show(null, force);
            }
            finally { check.IsEnabled = true; }
        }

        check.Click += async (s, e) => await LoadAsync(true);
        update.Click += (s, e) =>
        {
            ctx.Cog.Hide(); // the install shows its progress on the game
            InstallOsButton_Click(update, e);
        };
        return row;
    }
}
