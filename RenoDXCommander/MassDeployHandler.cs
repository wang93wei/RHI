using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

/// <summary>
/// Handles mass INI deployment and preset installation across all games.
/// Extracted from SettingsHandler to isolate the deployment concern.
/// </summary>
public class MassDeployHandler
{
    private readonly MainWindow _window;
    private readonly IOptiScalerService _optiScalerService;
    private ILocalizationService Loc => App.Services.GetRequiredService<ILocalizationService>();

    public MassDeployHandler(MainWindow window)
    {
        _window = window;
        _optiScalerService = App.Services.GetRequiredService<IOptiScalerService>();
    }

    public async void MassDeployRsIni_Click(object sender, RoutedEventArgs e)
    {
        var eligible = _window.ViewModel.AllCards.Where(c => c.RsStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath)).ToList();
        var confirmDialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.ConfirmMassDeployment"),
            Content = Loc.GetString("Dialog.MassDeploy.ConfirmReshade", eligible.Count),
            PrimaryButtonText = Loc.GetString("Dialog.Deploy"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        if (await DialogService.ShowSafeAsync(confirmDialog) != ContentDialogResult.Primary) return;

        int count = 0;
        foreach (var card in eligible)
        {
            try
            {
                var screenshotPath = _window.BuildScreenshotSavePath(card.GameName);
                var overlayHotkey = _window.ViewModel.Settings.OverlayHotkey;
                var screenshotHotkey = _window.ViewModel.Settings.ScreenshotHotkey;
                if (card.RequiresVulkanInstall)
                    AuxInstallService.MergeRsVulkanIni(card.InstallPath, card.GameName, screenshotPath, overlayHotkey, screenshotHotkey);
                else
                    AuxInstallService.MergeRsIni(card.InstallPath, screenshotPath, overlayHotkey, screenshotHotkey);
                count++;
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MassDeployRsIni] Failed for '{card.GameName}' — {ex.Message}");
            }
        }
        CrashReporter.Log($"[MassDeployRsIni] Deployed reshade.ini to {count} game(s)");
        await ShowDeployResult("reshade.ini", count);
    }

    public async void MassDeployUlIni_Click(object sender, RoutedEventArgs e)
    {
        var eligible = _window.ViewModel.AllCards.Where(c => c.UlStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath)).ToList();
        var confirmDialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.ConfirmMassDeployment"),
            Content = Loc.GetString("Dialog.MassDeploy.ConfirmRelimiter", eligible.Count),
            PrimaryButtonText = Loc.GetString("Dialog.Deploy"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        if (await DialogService.ShowSafeAsync(confirmDialog) != ContentDialogResult.Primary) return;

        int count = 0;
        foreach (var card in eligible)
        {
            try
            {
                AuxInstallService.CopyUlIni(card.InstallPath);
                count++;
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MassDeployUlIni] Failed for '{card.GameName}' — {ex.Message}");
            }
        }
        CrashReporter.Log($"[MassDeployUlIni] Deployed relimiter.ini to {count} game(s)");
        await ShowDeployResult("relimiter.ini", count);
    }

    public async void MassDeployDcIni_Click(object sender, RoutedEventArgs e)
    {
        var eligible = _window.ViewModel.AllCards.Where(c => c.DcStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath)).ToList();
        var confirmDialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.ConfirmMassDeployment"),
            Content = Loc.GetString("Dialog.MassDeploy.ConfirmDisplayCommander", eligible.Count),
            PrimaryButtonText = Loc.GetString("Dialog.Deploy"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        if (await DialogService.ShowSafeAsync(confirmDialog) != ContentDialogResult.Primary) return;

        int count = 0;
        foreach (var card in eligible)
        {
            try
            {
                AuxInstallService.CopyDcIni(card.InstallPath);
                count++;
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MassDeployDcIni] Failed for '{card.GameName}' — {ex.Message}");
            }
        }
        CrashReporter.Log($"[MassDeployDcIni] Deployed DisplayCommander.ini to {count} game(s)");
        await ShowDeployResult("DisplayCommander.ini", count);
    }

    public async void MassDeployOsIni_Click(object sender, RoutedEventArgs e)
    {
        int count = 0;
        var sourceIni = Services.OptiScalerService.OsIniPath;
        if (!File.Exists(sourceIni))
        {
            CrashReporter.Log("[MassDeployOsIni] No OptiScaler.ini found in INIs folder — aborting");
            await ShowDeployResult("OptiScaler.ini", 0);
            return;
        }

        var eligible = _window.ViewModel.AllCards.Where(c => c.OsStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath)).ToList();
        var confirmDialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.ConfirmMassDeployment"),
            Content = Loc.GetString("Dialog.MassDeploy.ConfirmOptiScaler", eligible.Count),
            PrimaryButtonText = Loc.GetString("Dialog.Deploy"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        if (await DialogService.ShowSafeAsync(confirmDialog) != ContentDialogResult.Primary) return;

        foreach (var card in eligible)
        {
            try
            {
                _optiScalerService.CopyIniToGame(card, _window.ViewModel.Settings.OsHotkey);
                count++;
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MassDeployOsIni] Failed for '{card.GameName}' — {ex.Message}");
            }
        }
        CrashReporter.Log($"[MassDeployOsIni] Deployed OptiScaler.ini to {count} game(s)");
        await ShowDeployResult("OptiScaler.ini", count);
    }

    private async Task ShowDeployResult(string iniName, int count)
    {
        var message = count > 0
            ? Loc.GetString("Dialog.MassDeploy.Deployed", iniName, count)
            : Loc.GetString("Dialog.MassDeploy.NoGamesWithComponent");
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.MassIniDeployment"),
            Content = message,
            CloseButtonText = Loc.GetString("Dialog.Ok"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(dialog);
    }

    public async Task MassPresetInstall_ClickAsync(XamlRoot xamlRoot)
    {
        // ── 1. Show preset picker ────────────────────────────────────────────
        var selectedPresets = await PresetPopupHelper.ShowAsync(xamlRoot);
        if (selectedPresets == null || selectedPresets.Count == 0) return;

        // ── 2. Show game picker — list all games with ReShade installed ──────
        var rsGames = _window.ViewModel.AllCards
            .Where(c => c.RsStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath))
            .OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rsGames.Count == 0)
        {
            var noGamesDialog = new ContentDialog
            {
                Title = Loc.GetString("Dialog.NoGamesAvailable"),
                Content = Loc.GetString("Dialog.NoGamesWithReshadeInstalled"),
                CloseButtonText = Loc.GetString("Dialog.Ok"),
                XamlRoot = xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(noGamesDialog);
            return;
        }

        var gamePanel = new StackPanel { Spacing = 4 };
        var gameCheckBoxes = new List<(GameCardViewModel Card, CheckBox Box)>();

        // Select All / Deselect All buttons
        var selectAllBtn = new Button
        {
            Content = Loc.GetString("Dialog.SelectAll"),
            FontSize = 11,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 8, 8),
        };
        var deselectAllBtn = new Button
        {
            Content = Loc.GetString("Dialog.DeselectAll"),
            FontSize = 11,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 8),
        };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
        btnRow.Children.Add(selectAllBtn);
        btnRow.Children.Add(deselectAllBtn);
        gamePanel.Children.Add(btnRow);

        foreach (var card in rsGames)
        {
            var cb = new CheckBox
            {
                Content = card.GameName,
                IsChecked = false,
                FontSize = 12,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
                Margin = new Thickness(0, 2, 0, 2),
            };
            gameCheckBoxes.Add((card, cb));
            gamePanel.Children.Add(cb);
        }

        selectAllBtn.Click += (s, ev) => { foreach (var (_, cb) in gameCheckBoxes) cb.IsChecked = true; };
        deselectAllBtn.Click += (s, ev) => { foreach (var (_, cb) in gameCheckBoxes) cb.IsChecked = false; };

        var gameScrollViewer = new ScrollViewer
        {
            Content = gamePanel,
            MaxHeight = 400,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var gameDialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.MassDeploy.SelectGames", string.Join(", ", selectedPresets)),
            Content = gameScrollViewer,
            PrimaryButtonText = Loc.GetString("Dialog.Deploy"),
            IsPrimaryButtonEnabled = false,
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
            MinWidth = 500,
        };

        // Enable Deploy only when at least one game is ticked
        foreach (var (_, box) in gameCheckBoxes)
        {
            box.Checked += (s, ev) => gameDialog.IsPrimaryButtonEnabled = gameCheckBoxes.Any(cb => cb.Box.IsChecked == true);
            box.Unchecked += (s, ev) => gameDialog.IsPrimaryButtonEnabled = gameCheckBoxes.Any(cb => cb.Box.IsChecked == true);
        }

        var gameResult = await DialogService.ShowSafeAsync(gameDialog);
        if (gameResult != ContentDialogResult.Primary) return;

        // ── 3. Deploy presets to selected games ──────────────────────────────
        var selectedGames = gameCheckBoxes
            .Where(cb => cb.Box.IsChecked == true)
            .Select(cb => cb.Card)
            .ToList();

        int totalDeployed = 0;
        foreach (var card in selectedGames)
        {
            try
            {
                int count = PresetPopupHelper.DeployPresets(selectedPresets, card.InstallPath);
                totalDeployed += count;
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MassPresetInstall] Failed for '{card.GameName}' — {ex.Message}");
            }
        }
        CrashReporter.Log($"[MassPresetInstall] Deployed {selectedPresets.Count} preset(s) to {selectedGames.Count} game(s) ({totalDeployed} total copies)");

        if (totalDeployed == 0) return;

        // ── 4. Offer shader installation ─────────────────────────────────────
        var shaderDialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.InstallShaders"),
            Content = Loc.GetString("Dialog.MassDeploy.AlsoInstallShaders", selectedGames.Count),
            PrimaryButtonText = Loc.GetString("Dialog.Yes"),
            CloseButtonText = Loc.GetString("Dialog.No"),
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var shaderResult = await DialogService.ShowSafeAsync(shaderDialog);
        if (shaderResult == ContentDialogResult.Primary)
        {
            var presetPaths = selectedPresets.Select(f => Path.Combine(PresetPopupHelper.PresetsDir, f)).ToList();
            foreach (var card in selectedGames)
            {
                try
                {
                    await _window.ViewModel.ApplyPresetShadersAsync(card.GameName, presetPaths, card.Source ?? "");
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MassPresetInstall] Shader install failed for '{card.GameName}' — {ex.Message}");
                }
            }
            CrashReporter.Log($"[MassPresetInstall] Applied preset shaders to {selectedGames.Count} game(s)");

            // Rebuild overrides panel if the currently selected game was one of the targets
            if (_window.ViewModel.SelectedGame is { } selectedCard
                && selectedGames.Any(c => c.GameName.Equals(selectedCard.GameName, StringComparison.OrdinalIgnoreCase)))
            {
                _window.BuildOverridesPanel(selectedCard);
            }
        }
    }
}
