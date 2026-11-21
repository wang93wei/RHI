// MainWindow.Restore.cs -- the second half of restoring a backup: once the first scan has finished, a review lists
// every game in the backup with what RHI had installed there (all pre-ticked when the game is on this PC), then
// installs the ticked components one game at a time through the normal install code, puts the game's config files
// back and restores the NVIDIA driver settings. Also starts the automatic backup after the first scan.
// RHI's data was already restored before startup (BackupArchive.ApplyData). Plan: docs/backup-restore-plan.md.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private bool _restoreReviewStarted;
    private bool _startupBackupDone;

    /// <summary>One component to install in one game (a ticked box in the review).</summary>
    private sealed record RestoreItem(GameCardViewModel Card, BackupGame Game, string ComponentId);

    /// <summary>
    /// Called from the constructor: runs the restore review / automatic backup once the store scan has found
    /// every game (LibraryScanned), never after the cache phase alone.
    /// </summary>
    private void SetUpRestoreAndBackupHooks()
    {
        ViewModel.LibraryScanned += () =>
        {
            if (IsShuttingDown) return;
            _ = AfterScanAsync();
        };

        // A restore is waiting: the review only opens once every game is found (a minute or more on a fresh
        // install), so say so instead of leaving an empty list with no explanation
        if (MainViewModel.PendingRestorePlan() != null)
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                ShowStatusBarMessage(Loc.GetString("Backup.Status.RestoredSearching"),
                    TimeSpan.FromMinutes(5)));
    }

    private async Task AfterScanAsync()
    {
        try
        {
            if (!_restoreReviewStarted && MainViewModel.PendingRestorePlan() is { } plan)
            {
                _restoreReviewStarted = true;
                await Task.Delay(1500); // let the list settle (selection, detail panel) before the dialog
                if (IsShuttingDown || ViewModel.IsLoading) { _restoreReviewStarted = false; return; }
                await ShowRestoreReviewAsync(plan);
                return;
            }
            if (!_startupBackupDone && !_restoreReviewStarted)
            {
                _startupBackupDone = true;
                await ViewModel.MaybeAutoBackupAsync("startup");
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.AfterScanAsync] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private bool IsComponentInstalled(GameCardViewModel card, string id) => id switch
    {
        BackupComponents.ReShade => card.IsRsInstalled,
        BackupComponents.Dxvk => card.IsDxvkInstalled,
        BackupComponents.ReFramework => card.IsRefInstalled,
        BackupComponents.RenoDx => card.IsRdxInstalled,
        BackupComponents.Luma => card.IsLumaInstalled,
        BackupComponents.ReLimiter => card.IsUlInstalled,
        BackupComponents.DisplayCommander => card.IsDcInstalled,
        BackupComponents.DofFix => card.IsDofFixInstalled,
        BackupComponents.OptiScaler => card.IsOsInstalled,
        _ => false,
    };

    // ── Review ───────────────────────────────────────────────────────────────────

    private async Task ShowRestoreReviewAsync(RestorePlan plan)
    {
        var cards = ViewModel.AllCards.ToList();
        var matches = BackupMatching.Match(plan.Games, cards.Select(c => (c.GameName, c.Source ?? "")).ToList());
        CrashReporter.Log($"[MainWindow.ShowRestoreReviewAsync] Backup of {plan.Games.Count} games; {matches.Count(m => m >= 0)} found on this PC");

        var componentBoxes = new List<(CheckBox Box, RestoreItem Item)>();
        var configBoxes = new List<(CheckBox Box, GameCardViewModel Card, BackupGame Game)>();

        var list = new StackPanel { Spacing = 10 };
        var order = Enumerable.Range(0, plan.Games.Count)
            .OrderBy(i => matches[i] < 0)                       // found games first
            .ThenBy(i => plan.Games[i].Name, StringComparer.OrdinalIgnoreCase);
        foreach (var i in order)
        {
            var game = plan.Games[i];
            var card = matches[i] >= 0 ? cards[matches[i]] : null;
            bool folderOk = card != null && !string.IsNullOrEmpty(card.InstallPath) && Directory.Exists(card.InstallPath);

            var row = new StackPanel { Spacing = 4 };
            var title = new TextBlock
            {
                Text = game.Name,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            row.Children.Add(title);
            string note = card == null ? Loc.GetString("Backup.Review.NotFound")
                : !folderOk ? Loc.GetString("Backup.Review.FolderMissing")
                : card.InstallPath;
            row.Children.Add(new TextBlock
            {
                Text = note,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = UIFactory.Brush(folderOk ? ResourceKeys.TextTertiaryBrush : ResourceKeys.AccentAmberBrush),
            });

            var boxes = new WrapPanel { HorizontalSpacing = 14, VerticalSpacing = 2 };
            foreach (var comp in game.Components.OrderBy(c => BackupComponents.OrderOf(c.Id)))
            {
                bool installed = card != null && IsComponentInstalled(card, comp.Id);
                var box = new CheckBox
                {
                    Content = BackupComponents.Label(comp.Id) + (installed ? " " + Loc.GetString("Backup.Review.InstalledSuffix") : ""),
                    IsChecked = folderOk && !installed,
                    IsEnabled = folderOk && !installed,
                    MinWidth = 0,
                };
                if (comp.Detail != null) ToolTipService.SetToolTip(box, Loc.GetString("Backup.Review.Was", comp.Detail));
                boxes.Children.Add(box);
                if (folderOk && !installed) componentBoxes.Add((box, new RestoreItem(card!, game, comp.Id)));
            }
            if (game.ConfigFiles.Count > 0)
            {
                var box = new CheckBox
                {
                    Content = game.ConfigFiles.Count == 1 ? Loc.GetString("Backup.Review.ConfigFileOne") : Loc.GetString("Backup.Review.ConfigFiles", game.ConfigFiles.Count),
                    IsChecked = folderOk,
                    IsEnabled = folderOk,
                    MinWidth = 0,
                };
                ToolTipService.SetToolTip(box, string.Join("\n", game.ConfigFiles)
                    + "\n\n" + Loc.GetString("Backup.Review.ConfigTooltip"));
                boxes.Children.Add(box);
                if (folderOk) configBoxes.Add((box, card!, game));
            }
            row.Children.Add(boxes);
            list.Children.Add(row);
        }

        CheckBox? nvidiaBox = null;
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Backup.Review.Content", $"{plan.BackupCreatedUtc.ToLocalTime():d MMMM yyyy}"),
            TextWrapping = TextWrapping.Wrap,
        });
        if (!GitHubTokenHandler.HasToken)
        {
            // Many installs in a row can use up GitHub's 60 requests an hour without a token
            content.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Backup.Review.TokenTip"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush),
            });
        }
        if (plan.HasNvidia)
        {
            nvidiaBox = new CheckBox { Content = Loc.GetString("Backup.Nvidia.Checkbox"), IsChecked = true };
            ToolTipService.SetToolTip(nvidiaBox, Loc.GetString("Backup.Nvidia.Tooltip"));
            content.Children.Add(nvidiaBox);
        }
        content.Children.Add(new ScrollViewer
        {
            Content = list,
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0, 0, 14, 0),
        });
        content.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Backup.Review.Extras"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
        });

        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Backup.Review.Title"),
            Content = content,
            PrimaryButtonText = Loc.GetString("Dialog.Restore"),
            SecondaryButtonText = Loc.GetString("Backup.Review.DontRestore"),
            CloseButtonText = Loc.GetString("Dialog.Later"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 820.0;
        var result = await DialogService.ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Secondary)
        {
            CrashReporter.Log("[MainWindow.ShowRestoreReviewAsync] User chose not to restore installs");
            BackupArchive.ClearRestore(MainViewModel.RhiDataDir);
            ShowStatusBarMessage(Loc.GetString("Backup.Status.KeepSettings"), TimeSpan.FromSeconds(8));
            return;
        }
        if (result != ContentDialogResult.Primary)
        {
            CrashReporter.Log("[MainWindow.ShowRestoreReviewAsync] Later: the review comes back on the next start");
            ShowStatusBarMessage(Loc.GetString("Backup.Status.Postponed"), TimeSpan.FromSeconds(10));
            return;
        }

        var items = componentBoxes.Where(b => b.Box.IsChecked == true).Select(b => b.Item).ToList();
        var configs = configBoxes.Where(b => b.Box.IsChecked == true).Select(b => (b.Card, b.Game)).ToList();
        await RunRestoreAsync(items, configs, nvidiaBox?.IsChecked == true);
    }

    // ── Running it ───────────────────────────────────────────────────────────────

    private async Task RunRestoreAsync(List<RestoreItem> items, List<(GameCardViewModel Card, BackupGame Game)> configs, bool nvidia)
    {
        var rhiDir = MainViewModel.RhiDataDir;
        var games = items.Select(i => i.Card).Concat(configs.Select(c => c.Card)).Distinct().ToList();
        var installed = new List<string>();
        var failed = new List<string>();
        int configCount = 0;
        CrashReporter.Log($"[MainWindow.RunRestoreAsync] {items.Count} installs in {games.Count} games, {configs.Count} config sets, NVIDIA={nvidia}");

        using (ViewModel.BeginActivity(Loc.GetString("Backup.Activity.Restoring")))
        {
            int n = 0;
            foreach (var card in games)
            {
                if (IsShuttingDown) return;
                n++;
                ShowStatusBarMessage(Loc.GetString("Backup.Activity.RestoringProgress", n, games.Count, card.GameName), TimeSpan.FromMinutes(10));

                var cfg = configs.FirstOrDefault(c => ReferenceEquals(c.Card, card));
                // Config files already in the folder before the installs are the user's: kept
                var preExisting = cfg.Game != null
                    ? await Task.Run(() => BackupArchive.ExistingConfigs(cfg.Game, card.InstallPath))
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in items.Where(i => ReferenceEquals(i.Card, card)).OrderBy(i => BackupComponents.OrderOf(i.ComponentId)))
                {
                    var label = $"{card.GameName}: {BackupComponents.Label(item.ComponentId)}";
                    if (IsComponentInstalled(card, item.ComponentId)) continue; // came with an earlier step (Luma's ReShade)
                    try
                    {
                        await InstallForRestoreAsync(card, item.ComponentId);
                        if (IsComponentInstalled(card, item.ComponentId)) installed.Add(label);
                        else failed.Add(label);
                    }
                    catch (Exception ex)
                    {
                        CrashReporter.Log($"[MainWindow.RunRestoreAsync] {label} — {ex.GetType().Name}: {ex.Message}");
                        failed.Add(label);
                    }
                }

                if (cfg.Game != null)
                    configCount += await Task.Run(() => BackupArchive.RestoreGameConfigs(rhiDir, cfg.Game, card.InstallPath, preExisting));
            }

            if (nvidia)
            {
                ShowStatusBarMessage(Loc.GetString("Backup.Activity.RestoringNvidia"), TimeSpan.FromMinutes(2));
                try
                {
                    var json = await File.ReadAllTextAsync(Path.Combine(rhiDir, "restore", BackupArchive.NvidiaEntry));
                    var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (data != null && data.Count > 0)
                    {
                        var presetService = App.Services.GetRequiredService<DlssPresetService>();
                        int count = await Task.Run(() => presetService.ImportProfiles(data));
                        CrashReporter.Log($"[MainWindow.RunRestoreAsync] NVIDIA: imported {count} profiles");
                    }
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException or InvalidOperationException)
                {
                    CrashReporter.Log($"[MainWindow.RunRestoreAsync] NVIDIA settings not restored — {ex.Message}");
                    failed.Add(Loc.GetString("Backup.Nvidia.Label"));
                }
            }
        }

        BackupArchive.ClearRestore(rhiDir);
        ViewModel.SaveLibraryPublic();
        ShowStatusBarMessage(Loc.GetString("Backup.Status.Finished"), TimeSpan.FromSeconds(8));
        if (ViewModel.SelectedGame is { } selected) PopulateDetailPanel(selected);
        await ShowRestoreSummaryAsync(installed, failed, configCount);
    }

    private async Task InstallForRestoreAsync(GameCardViewModel card, string id)
    {
        CrashReporter.Log($"[MainWindow.InstallForRestoreAsync] '{card.GameName}' → {id}");
        switch (id)
        {
            case BackupComponents.ReShade:
                await ViewModel.InstallReShadeCommand.ExecuteAsync(card);
                break;
            case BackupComponents.Dxvk:
                await ViewModel.InstallDxvkAsync(card, Content.XamlRoot);
                break;
            case BackupComponents.ReFramework:
                await ViewModel.InstallREFrameworkCommand.ExecuteAsync(card);
                break;
            case BackupComponents.RenoDx:
                if (card.Mod == null || card.IsExternalOnly)
                {
                    CrashReporter.Log($"[MainWindow.InstallForRestoreAsync] '{card.GameName}': no RenoDX mod RHI can download");
                    return;
                }
                await ViewModel.InstallModCommand.ExecuteAsync(card);
                break;
            case BackupComponents.Luma:
                await ViewModel.InstallLumaAsync(card);
                break;
            case BackupComponents.ReLimiter:
                await ViewModel.InstallUlAsync(card);
                break;
            case BackupComponents.DisplayCommander:
                await ViewModel.InstallDcAsync(card);
                break;
            case BackupComponents.DofFix:
                await InstallDofFixAsync(card);
                break;
            case BackupComponents.OptiScaler:
                await _installEventHandler.InstallOptiScalerAsync(card, null);
                break;
        }
    }

    private async Task ShowRestoreSummaryAsync(List<string> installed, List<string> failed, int configCount)
    {
        var text = installed.Count == 0 && failed.Count == 0
            ? Loc.GetString("Backup.Summary.Nothing")
            : Loc.GetString(installed.Count == 1 ? "Backup.Summary.InstalledOne" : "Backup.Summary.Installed", installed.Count);
        if (configCount > 0) text += " " + Loc.GetString(configCount == 1 ? "Backup.Summary.PutBackOne" : "Backup.Summary.PutBack", configCount);
        if (failed.Count > 0)
            text += "\n\n" + Loc.GetString("Backup.Summary.FailedIntro") + "\n"
                  + string.Join("\n", failed.Take(30)) + (failed.Count > 30 ? "\n" + Loc.GetString("Backup.Summary.FailedMore", failed.Count - 30) : "");
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Backup.Summary.Title"),
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                MaxHeight = 400,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            CloseButtonText = Loc.GetString("Dialog.Ok"),
            XamlRoot = Content.XamlRoot,
        };
        await DialogService.ShowSafeAsync(dialog);
    }
}
