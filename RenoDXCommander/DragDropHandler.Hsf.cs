// DragDropHandler.Hsf.cs: an HSF test build (a zip shared on Discord, holding hsf.addon64 / hsf.addon32 and
// HSF_streamline\) dropped on RHI. No buttons for this on purpose: drag and drop only. The build goes into the game
// the user picks (the selected one by default) through MainViewModel.InstallHsfBetaAsync, and RHI keeps it until the
// next HSF release (HsfService.Beta.cs). Builds set to XeMFG are offered Universal MFG Unlocker on every card.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DragDropHandler
{
    /// <summary>Installs the HSF build in <paramref name="packageRoot"/> (inside the extracted archive). True when installed.</summary>
    private async Task<bool> OfferHsfBetaAsync(string packageRoot, string archiveName)
    {
        var label = Path.GetFileNameWithoutExtension(archiveName);
        var xamlRoot = _window.Content.XamlRoot;
        var games = _window.ViewModel.AllCards
            .Where(c => !string.IsNullOrEmpty(c.InstallPath) && MainViewModel.HsfBlock(c) == null)
            .OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (games.Count == 0)
        {
            await ShowDropMessageAsync(Loc.GetString("DragDrop.Hsf.NoGame.Title"),
                Loc.GetString("DragDrop.Hsf.NoGame.Body"));
            return false;
        }

        var names = games.Select(c => c.GameName).ToList();
        int pick = _window.ViewModel.SelectedGame is { } sel ? games.IndexOf(sel) : -1;
        if (pick < 0) pick = games.FindIndex(c => HsfService.IsRhiInstalled(c.InstallPath!));
        if (pick < 0) pick = FuzzyMatchGameIndex(names, archiveName);
        var combo = new ComboBox { ItemsSource = names, SelectedIndex = Math.Max(0, pick), HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = Loc.GetString("DragDrop.Hsf.BuildIntro", archiveName),
            TextWrapping = TextWrapping.Wrap, FontSize = 13,
        });
        content.Children.Add(new TextBlock { Text = Loc.GetString("DragDrop.Hsf.InstallIn"), FontSize = 12 });
        content.Children.Add(combo);
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("DragDrop.Hsf.Confirm.Title"),
            Content = content,
            PrimaryButtonText = Loc.GetString("Dialog.Install"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return false;
        var card = games[Math.Max(0, combo.SelectedIndex)];

        _window.ShowStatusBarMessage(Loc.GetString("DragDrop.Hsf.Installing", label, card.GameName), TimeSpan.FromSeconds(30));
        var error = await _window.ViewModel.InstallHsfBetaAsync(card, packageRoot, label);
        if (error != null)
        {
            _window.ShowStatusBarMessage(Loc.GetString("DragDrop.Hsf.NotInstalled"), TimeSpan.FromSeconds(6));
            await ShowDropMessageAsync(Loc.GetString("DragDrop.Hsf.NotInstalled"), error);
            return false;
        }

        // A build set to XeMFG needs the unlocker for more than 2x on every card (asks only when neither unlocker is in)
        if (MainViewModel.HsfXeMfgNeedsUnlocker(card))
        {
            var builder = _window.DetailPanelBuilderInstance;
            var mfg = await builder.AskHsfMfgUnlockerAsync(card, xamlRoot);
            if (mfg.Install && await _window.ViewModel.InstallMfgForHsfAsync(card, mfg.ProxyName) is { } mfgError)
                await ShowDropMessageAsync(Loc.GetString("Extras.Hsf.MfgNotInstalled.Title"), mfgError);
        }

        _window.ShowStatusBarMessage(Loc.GetString("DragDrop.Hsf.Installed", label, card.GameName), TimeSpan.FromSeconds(8));
        if (ReferenceEquals(_window.ViewModel.SelectedGame, card)) _window.DetailPanelBuilderInstance.RequestExtrasRebuild(card);
        return true;
    }

    private async Task ShowDropMessageAsync(string title, string text)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13 },
            CloseButtonText = Loc.GetString("Dialog.Ok"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(dialog);
    }
}
