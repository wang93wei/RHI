// MainWindow.AntivirusNotice.cs — tells the user when an antivirus deleted a file RHI had just
// downloaded (see Services/AntivirusGuard.cs). Shown once per component per session.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private readonly HashSet<string> _antivirusNoticeShown = new(StringComparer.OrdinalIgnoreCase);

    private void HookAntivirusNotice()
    {
        AntivirusGuard.Blocked += (component, path) =>
            DispatcherQueue?.TryEnqueue(() => _ = ShowAntivirusNoticeAsync(component, path));
    }

    private async Task ShowAntivirusNoticeAsync(string component, string path)
    {
        try
        {
            if (IsShuttingDown || !_antivirusNoticeShown.Add(component)) return;
            ShowStatusBarMessage(Loc.GetString("Antivirus.Status.Removed", component), TimeSpan.FromSeconds(15));
            if (Content?.XamlRoot is not { } root) return;

            var folder = Path.GetDirectoryName(path) ?? path;
            var dialog = new ContentDialog
            {
                Title = Loc.GetString("Antivirus.Dialog.Title"),
                Content = Loc.GetString("Antivirus.Dialog.Content", component, path),
                PrimaryButtonText = Loc.GetString("Dialog.OpenFolder"),
                CloseButtonText = Loc.GetString("Dialog.Ok"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(dialog) == ContentDialogResult.Primary)
            {
                var open = Directory.Exists(folder) ? folder
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RHI");
                System.Diagnostics.Process.Start("explorer.exe", $"\"{open}\"");
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.ShowAntivirusNoticeAsync] {ex.Message}");
        }
    }
}
