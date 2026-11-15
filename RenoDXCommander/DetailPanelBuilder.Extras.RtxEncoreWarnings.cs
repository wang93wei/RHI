// DetailPanelBuilder.Extras.RtxEncoreWarnings.cs — warning shown after RHI installs RTX Encore's DLSS frame
// generation files in a game where ReShade is dxgi.dll: choosing DLSS-FG then crashes the game at startup
// (RTX Encore issue #133; Streamline loads ReShade's dxgi.dll and its swap chain creation loops back into itself).
// A warning rather than a block, so users decide and new RTX Encore versions can be tested without an RHI update.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using System;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    internal const string EncoreDlssFgReShadeIssueUrl = "https://github.com/SilyNoMeta/rtx-encore/issues/133";

    internal static string EncoreDlssFgReShadeWarningText => Loc.GetString("Extras.RtxEncoreWarn.WarningText");

    /// <summary>True when this game's ReShade is installed as dxgi.dll (the setup RTX Encore's added DLSS-FG crashes with).</summary>
    internal static bool IsReShadeDxgi(GameCardViewModel card) =>
        card.IsRsInstalled && string.Equals(card.RsInstalledFile, "dxgi.dll", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Tells the user about the DLSS-FG + ReShade dxgi.dll crash after RHI installed the frame generation files.
    /// Information only (the install has happened); "Don't show this again" is saved.
    /// </summary>
    private async Task ShowEncoreDlssFgReShadeWarningAsync(XamlRoot? xamlRoot)
    {
        xamlRoot ??= _window.Content.XamlRoot;
        if (xamlRoot == null || _window.ViewModel.Settings.EncoreDlssFgReShadeWarningDismissed) return;

        var dontShowCheck = new CheckBox
        {
            Content = Loc.GetString("Dialog.DonTShowThisAgain"),
            FontSize = 12,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Extras.RtxEncoreWarn.Intro") + "\n\n" + EncoreDlssFgReShadeWarningText,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 22,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        });
        var link = new HyperlinkButton { Content = Loc.GetString("Extras.RtxEncoreWarn.IssueLink"), Padding = new Thickness(0), FontSize = 12 };
        link.Click += (s, e) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(EncoreDlssFgReShadeIssueUrl));
        content.Children.Add(link);
        content.Children.Add(dontShowCheck);

        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Extras.RtxEncoreWarn.Title"),
            Content = content,
            CloseButtonText = Loc.GetString("Dialog.Ok"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        await DialogService.ShowSafeAsync(dialog);
        if (dontShowCheck.IsChecked == true)
        {
            _window.ViewModel.Settings.EncoreDlssFgReShadeWarningDismissed = true;
            _window.ViewModel.SaveSettingsPublic();
            CrashReporter.Log("[DetailPanelBuilder.ShowEncoreDlssFgReShadeWarningAsync] Don't show again ticked");
        }
    }
}
