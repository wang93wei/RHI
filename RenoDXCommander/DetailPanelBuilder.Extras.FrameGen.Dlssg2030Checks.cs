// DetailPanelBuilder.Extras.FrameGen.Dlssg2030Checks.cs — 20/30 FG Unlock install checks: the OptiScaler warning
// (warn, not block: OptiScaler used for upscaling or GPU spoofing is fine, its frame generation isn't).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    /// <summary>OptiScaler is in the game: Continue / Cancel. True = go on with the install.</summary>
    private static async Task<bool> ConfirmDlssg2030WithOptiScalerAsync(XamlRoot? xamlRoot)
    {
        if (xamlRoot == null) return true;
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Extras.Dlssg2030.Checks.OptiScaler.Title"),
            Content = new TextBlock
            {
                Text = Loc.GetString("Extras.Dlssg2030.Checks.OptiScaler.Content"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                LineHeight = 20,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            },
            PrimaryButtonText = Loc.GetString("Dialog.Continue"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        var go = await DialogService.ShowSafeAsync(dialog) == ContentDialogResult.Primary;
        CrashReporter.Log($"[DetailPanelBuilder.ConfirmDlssg2030WithOptiScaler] {(go ? "continue" : "cancelled")}");
        return go;
    }
}
