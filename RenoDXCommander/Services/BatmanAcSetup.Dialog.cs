// BatmanAcSetup.Dialog.cs -- the pop-up shown before boostedwarr's Batman: Arkham City mod installs: what the mod is,
// what RHI does with it and the few things to do in the game. Same look as the other games' install pop-ups.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RenoDXCommander.Services;

internal static partial class BatmanAcSetup
{
    /// <summary>Shows the install pop-up. True = Install, false = Cancel. No window to show it in = true.</summary>
    internal static async Task<bool> ShowInstallDialogAsync()
    {
        try
        {
            XamlRoot? xamlRoot = null;
            if (Application.Current is App app
                && typeof(App).GetField("_window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) is MainWindow mw)
                xamlRoot = mw.Content?.XamlRoot;
            if (xamlRoot == null) return true;

            var content = new StackPanel { Spacing = 10, MaxWidth = 620 };
            void Heading(string text) => content.Children.Add(new TextBlock
            {
                Text = text, FontSize = 13, FontWeight = new Windows.UI.Text.FontWeight(600),
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush), Margin = new Thickness(0, 4, 0, 0),
            });
            void Body(string text, string brush = ResourceKeys.TextSecondaryBrush, double size = 13) => content.Children.Add(new TextBlock
            {
                Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size, Foreground = UIFactory.Brush(brush),
            });

            var loc = App.Services.GetRequiredService<ILocalizationService>();

            Body(loc.GetString("Dialog.BatmanAc.Body.Intro"));

            Heading(loc.GetString("Dialog.BatmanAc.Heading.Also"));
            Body(loc.GetString("Dialog.BatmanAc.Body.Also"));

            Heading(loc.GetString("Dialog.BatmanAc.Heading.InGame"));
            Body(loc.GetString("Dialog.BatmanAc.Body.InGame"),
                 ResourceKeys.InlineDescriptionBrush, 12);

            Body(loc.GetString("Dialog.BatmanAc.Body.Remove"),
                 ResourceKeys.TextTertiaryBrush, 12);

            var dialog = new ContentDialog
            {
                Title = loc.GetString("Dialog.BatmanAc.Title"),
                Content = new ScrollViewer { Content = content, MaxHeight = 560 },
                PrimaryButtonText = loc.GetString("Dialog.Install"),
                CloseButtonText = loc.GetString("Dialog.Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            dialog.Resources["ContentDialogMaxWidth"] = 700.0;
            return await DialogService.ShowSafeAsync(dialog) == ContentDialogResult.Primary;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            CrashReporter.Log($"[BatmanAcSetup.ShowInstallDialogAsync] {ex.Message}");
            return true;
        }
    }
}
