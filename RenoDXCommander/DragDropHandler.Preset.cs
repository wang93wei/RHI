// DragDropHandler.Preset.cs — Preset drop processing: validate, store, game selection, deploy, shader confirmation.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DragDropHandler
{
    /// <summary>
    /// Processes a dropped .ini file: validate → store → game selection → deploy → shader confirmation.
    /// </summary>
    public async Task ProcessDroppedPreset(string iniPath)
    {
        var fileName = Path.GetFileName(iniPath);
        _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Received '{fileName}'");

        // ── Step 1: Read and validate ─────────────────────────────────────────
        string content;
        try
        {
            content = File.ReadAllText(iniPath);
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Failed to read '{iniPath}' — {ex.Message}");
            var errDialog = new ContentDialog
            {
                Title = Loc.GetString("Dialog.ReadError"),
                Content = Loc.GetString("Dialog.ReadError.Content", ex.Message),
                CloseButtonText = Loc.GetString("Dialog.Ok"),
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(errDialog);
            return;
        }

        if (!PresetValidator.IsReShadePreset(content))
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] '{fileName}' is not a recognised ReShade preset");
            var errDialog = new ContentDialog
            {
                Title = Loc.GetString("Dialog.NotAReshadePreset"),
                Content = Loc.GetString("Dialog.ThisFileIsNotA"),
                CloseButtonText = Loc.GetString("Dialog.Ok"),
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(errDialog);
            return;
        }

        // ── Step 2: Copy to presets folder ────────────────────────────────────
        try
        {
            Directory.CreateDirectory(PresetPopupHelper.PresetsDir);
            var destPreset = Path.Combine(PresetPopupHelper.PresetsDir, fileName);
            File.Copy(iniPath, destPreset, overwrite: true);
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Stored '{fileName}' in presets folder");
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Failed to copy to presets folder — {ex.Message}");
            var errDialog = new ContentDialog
            {
                Title = Loc.GetString("Dialog.StorageError"),
                Content = Loc.GetString("Dialog.StorageError.Content", ex.Message),
                CloseButtonText = Loc.GetString("Dialog.Ok"),
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(errDialog);
            return;
        }

        // ── Step 3: Game selection dialog ─────────────────────────────────────
        var cards = ViewModel.AllCards?.ToList() ?? new();
        if (cards.Count == 0)
        {
            var noGamesDialog = new ContentDialog
            {
                Title = Loc.GetString("Dialog.NoGamesAvailable"),
                Content = Loc.GetString("Dialog.NoGamesAreCurrentlyDetected"),
                CloseButtonText = Loc.GetString("Dialog.Ok"),
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(noGamesDialog);
            return;
        }

        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = Loc.GetString("Dialog.SelectAGame"),
        };

        var sortedCards = cards.OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var card in sortedCards)
            combo.Items.Add(new ComboBoxItem { Content = card.GameName, Tag = card });

        // Auto-select the currently selected game in the sidebar
        if (ViewModel.SelectedGame != null)
        {
            for (int i = 0; i < sortedCards.Count; i++)
            {
                if (string.Equals(sortedCards[i].GameName, ViewModel.SelectedGame.GameName, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Dialog.InstallToGameFolder", fileName),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
        });
        panel.Children.Add(combo);

        var pickDialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.InstallReshadePreset"),
            Content = panel,
            PrimaryButtonText = Loc.GetString("Dialog.Next"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var pickResult = await DialogService.ShowSafeAsync(pickDialog);
        if (pickResult != ContentDialogResult.Primary) return;

        if (combo.SelectedItem is not ComboBoxItem selected || selected.Tag is not GameCardViewModel targetCard)
        {
            var noSelection = new ContentDialog
            {
                Title = Loc.GetString("Dialog.NoGameSelected"),
                Content = Loc.GetString("Dialog.PleaseSelectAGameTo"),
                CloseButtonText = Loc.GetString("Dialog.Ok"),
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(noSelection);
            return;
        }

        var gameName = targetCard.GameName;
        var installPath = targetCard.InstallPath;

        // ── Step 4: Copy preset to game folder ───────────────────────────────
        try
        {
            var destGame = Path.Combine(installPath, fileName);
            File.Copy(iniPath, destGame, overwrite: true);
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Deployed '{fileName}' to '{installPath}'");
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Failed to deploy preset — {ex.Message}");
            var errDialog = new ContentDialog
            {
                Title = Loc.GetString("Dialog.DeployFailed"),
                Content = Loc.GetString("Dialog.DeployFailed.Content", ex.Message),
                CloseButtonText = Loc.GetString("Dialog.Ok"),
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(errDialog);
            return;
        }

        // ── Step 5: Shader confirmation dialog ───────────────────────────────
        var shaderDialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.InstallShaders"),
            Content = Loc.GetString("Dialog.AlsoInstallTheRequiredShaders"),
            PrimaryButtonText = Loc.GetString("Dialog.Yes"),
            CloseButtonText = Loc.GetString("Dialog.No"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var shaderResult = await DialogService.ShowSafeAsync(shaderDialog);
        if (shaderResult == ContentDialogResult.Primary)
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] User chose to install shaders for '{gameName}'");
            await ViewModel.ApplyPresetShadersAsync(gameName, new[] { iniPath }, targetCard.Source ?? "");

            // Rebuild overrides panel so the shader toggle reflects the new "Select" mode
            if (ViewModel.SelectedGame is { } selectedCard
                && selectedCard.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase))
            {
                _window.BuildOverridesPanel(selectedCard);
            }
        }
        else
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] User declined shader install for '{gameName}'");
        }
    }
}
