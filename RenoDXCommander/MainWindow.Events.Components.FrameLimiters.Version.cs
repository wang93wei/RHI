// MainWindow.Events.Components.FrameLimiters.Version.cs — the Version section of the ReLimiter ⚙.
// Per game ("GameName|Store"): Automatic (v4 where it qualifies, else the last v3), v4 (latest), v3 (final), or a pinned
// release. Pinned games are skipped by the update check, Update All and automatic updates. Releases come from
// UlReleaseService; picking a version on a game with ReLimiter installed swaps the addon straight away. relimiter.ini is
// kept, except across v3 ↔ v4, where the game gets a fresh one (RHI's lines only) and the old one is backed up.
// The v3 / v4 options only appear once a v4 release exists; the rules are in UlV4Rules.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    // Combo entries. Read through Loc each time so the cog follows the current language; they are compared by value
    // inside one dialog instance, never persisted.
    private string UlLatestItem => Loc.GetString("Dialog.Ul.Version.Latest");
    private string UlAutoItem => Loc.GetString("Dialog.Ul.Version.Auto");
    private string UlV4Item => Loc.GetString("Dialog.Ul.Version.V4");
    private string UlV3Item => Loc.GetString("Dialog.Ul.Version.V3");

    private void AddUlVersionSection(CogDialog cog, GameCardViewModel card)
    {
        var store = card.Source ?? "";
        var pinned = ViewModel.GetUlPinnedVersion(card.GameName, store);
        var choice = ViewModel.GetUlMajorChoice(card.GameName, store);
        bool is32 = card.Is32Bit;

        cog.AddSection(Loc.GetString("Dialog.Ul.Version.Section"));

        var combo = CogDialog.MakeCombo(new List<string> { pinned ?? UlLatestItem }, 0);
        cog.AddRow(Loc.GetString("Dialog.Ul.Version.RowLabel"), combo,
            Loc.GetString("Dialog.Ul.Version.Tooltip") + (is32 ? Loc.GetString("Dialog.Ul.Version.Tooltip32Bit") : ""));

        var installedLine = cog.AddNote(InstalledText());
        var verdictLine = cog.AddNote("");
        verdictLine.Visibility = Visibility.Collapsed;
        var warning = cog.AddWarning("");
        warning.Visibility = Visibility.Collapsed;
        var switchBtn = CogDialog.StyledButton(Loc.GetString("Dialog.Ul.Version.SwitchToV3"), CogButtonKind.Primary);
        var switchRow = cog.AddButtons(switchBtn);
        switchRow.Visibility = Visibility.Collapsed;

        string InstalledText() => card.UlStatus == Models.GameStatus.NotInstalled
            ? Loc.GetString("Dialog.Ul.Version.NotInstalled")
            : Loc.GetString("Dialog.Ul.Version.Installed",
                string.IsNullOrEmpty(card.UlInstalledVersion) ? Loc.GetString("Dialog.Ul.Version.Unknown") : "v" + card.UlInstalledVersion.TrimStart('v', 'V'));

        bool filling = false;
        bool hasV4 = false;
        UlV4Verdict verdict = ViewModel.UlV4VerdictFor(card);
        switchBtn.Click += async (s, ev) => await SwitchToV3Async();

        // Combo entry for the game's current setting
        string CurrentItem() => pinned ?? (!hasV4 ? UlLatestItem : choice switch
        {
            "4" when verdict.Allowed => UlV4Item,
            "3" => UlV3Item,
            _ => UlAutoItem,
        });

        void RefreshNotes()
        {
            string? text = !hasV4 ? null
                : !verdict.Allowed ? verdict.Reason
                : verdict.Note ?? (verdict.Auto ? Loc.GetString("Dialog.Ul.Version.AutoNote") : null);
            if (MainViewModel.UlDevTestVersion() is { } dev)
                text = Loc.GetString("Dialog.Ul.Version.DevTest", UlV4Rules.DisplayVersion(dev)) + (text ?? "");
            verdictLine.Text = text ?? "";
            verdictLine.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;

            // Safety net: v4 in a game that can't use it (GPU changed, DXVK added, 32-bit override...)
            bool onV4 = card.UlStatus != Models.GameStatus.NotInstalled
                && UlV4Rules.MajorOf(card.UlInstalledVersion) >= UlV4Rules.V4Major;
            bool bad = onV4 && !verdict.Allowed;
            warning.Text = bad ? Loc.GetString("Dialog.Ul.Version.V4Blocked", verdict.Reason) : "";
            warning.Visibility = bad ? Visibility.Visible : Visibility.Collapsed;
            switchRow.Visibility = bad ? Visibility.Visible : Visibility.Collapsed;
        }

        void Fill(List<UlRelease> releases)
        {
            filling = true;
            try
            {
                verdict = ViewModel.UlV4VerdictFor(card);
                var devBuild = MainViewModel.UlDevTestVersion();
                hasV4 = releases.Any(r => UlV4Rules.MajorOf(r.Tag) >= UlV4Rules.V4Major)
                    || devBuild?.Major >= UlV4Rules.V4Major;
                var tags = releases
                    .Where(r => r.UrlFor(is32) != null)
                    .Where(r => verdict.Allowed || !(UlV4Rules.MajorOf(r.Tag) >= UlV4Rules.V4Major))
                    .Select(r => r.Tag).ToList();
                if (pinned != null && !tags.Contains(pinned, StringComparer.OrdinalIgnoreCase)) tags.Insert(0, pinned);

                var list = new List<string>();
                if (hasV4)
                {
                    list.Add(UlAutoItem);
                    if (verdict.Allowed) list.Add(UlV4Item);
                    list.Add(UlV3Item);
                }
                else list.Add(UlLatestItem);
                list.AddRange(tags);
                combo.ItemsSource = list;
                var idx = list.FindIndex(t => t.Equals(CurrentItem(), StringComparison.OrdinalIgnoreCase));
                combo.SelectedIndex = idx < 0 ? 0 : idx;
                RefreshNotes();
            }
            finally { filling = false; }
        }

        if (UlReleaseService.Cached is { } known && GpuInfoService.Cached != null) Fill(known);
        _ = Task.Run(async () =>
        {
            try
            {
                await GpuInfoService.GetNvidiaGpusAsync().ConfigureAwait(false);
                var releases = await UlReleaseService.ListAsync().ConfigureAwait(false);
                if (releases == null)
                {
                    DispatcherQueue.TryEnqueue(() => cog.SetError(Loc.GetString("Dialog.Ul.Version.ReleasesFailed")));
                    return;
                }
                DispatcherQueue.TryEnqueue(() => Fill(releases));
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MainWindow.UlVersion] Loading releases failed — {ex.Message}");
            }
        });

        // Puts the combo back on the game's current setting without running the handler
        void SelectCurrent()
        {
            if (combo.ItemsSource is not List<string> list) return;
            filling = true;
            try
            {
                var idx = list.FindIndex(t => t.Equals(CurrentItem(), StringComparison.OrdinalIgnoreCase));
                combo.SelectedIndex = idx < 0 ? 0 : idx;
            }
            finally { filling = false; }
        }

        // Saves the setting and installs it when ReLimiter is installed. Returns false when the install failed.
        async Task<bool> ApplyAsync(string? newChoice, string? newPin, string label)
        {
            var (prevChoice, prevPin) = (choice, pinned);
            ViewModel.SetUlMajorChoice(card.GameName, newChoice, store);
            ViewModel.SetUlPinnedVersion(card.GameName, newPin, store);
            (choice, pinned) = (newChoice, newPin);

            if (card.UlStatus == Models.GameStatus.NotInstalled)
            {
                if (newPin == null) { cog.SetSuccess(Loc.GetString("Dialog.Ul.Version.WillGetOnInstall", label)); return true; }
                // Download it now so the install doesn't wait
                cog.SetStatus(Loc.GetString("Dialog.Ul.Version.DownloadingPin", newPin));
                var (path, err) = await Task.Run(() => UlReleaseService.EnsurePinnedAsync(newPin, is32, null, LifetimeToken));
                if (path != null) cog.SetSuccess(Loc.GetString("Dialog.Ul.Version.PinReady", newPin));
                else cog.SetError(Loc.GetString("Dialog.Ul.Version.PinDownloadFailed", newPin, err));
                return true;
            }

            cog.SetStatus(Loc.GetString("Dialog.Ul.Version.Installing", label));
            var (error, iniReset) = await ViewModel.SwitchUlVersionAsync(card);
            if (error == null)
            {
                var ini = iniReset
                    ? Loc.GetString("Dialog.Ul.Version.IniReset")
                    : Loc.GetString("Dialog.Ul.Version.IniKept");
                cog.SetSuccess(newPin != null
                    ? Loc.GetString("Dialog.Ul.Version.SuccessPin", newPin, ini)
                    : Loc.GetString("Dialog.Ul.Version.SuccessLabel", label, ini));
                // The card's version is set on the dispatcher; read it after that has run
                DispatcherQueue.TryEnqueue(() => { installedLine.Text = InstalledText(); RefreshNotes(); });
                return true;
            }

            // Keep the setting in line with what's in the game folder
            ViewModel.SetUlMajorChoice(card.GameName, prevChoice, store);
            ViewModel.SetUlPinnedVersion(card.GameName, prevPin, store);
            (choice, pinned) = (prevChoice, prevPin);
            SelectCurrent();
            cog.SetError(Loc.GetString("Dialog.Ul.Version.InstallFailed", label, error));
            return false;
        }

        async Task SwitchToV3Async()
        {
            switchBtn.IsEnabled = false;
            try { await ApplyAsync("3", null, Loc.GetString("Dialog.Ul.Version.LabelV3")); SelectCurrent(); }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MainWindow.UlVersion] Switch to v3 failed for '{card.GameName}' — {ex.Message}");
                cog.SetError(Loc.GetString("Dialog.Ul.Version.SwitchFailed", ex.Message));
            }
            finally { switchBtn.IsEnabled = true; }
        }

        bool busy = false;
        combo.SelectionChanged += async (s, ev) =>
        {
            if (filling || busy || combo.SelectedItem is not string sel) return;
            if (sel.Equals(CurrentItem(), StringComparison.OrdinalIgnoreCase)) return;

            // The combo shows localized labels, so map them back to choices by value instead of pattern matching.
            string? newChoice;
            string? newPin;
            string label;
            if (sel.Equals(UlLatestItem, StringComparison.OrdinalIgnoreCase) || sel.Equals(UlAutoItem, StringComparison.OrdinalIgnoreCase))
            {
                newChoice = null;
                newPin = null;
                label = hasV4 ? Loc.GetString("Dialog.Ul.Version.LabelAuto") : Loc.GetString("Dialog.Ul.Version.LabelLatest");
            }
            else if (sel.Equals(UlV4Item, StringComparison.OrdinalIgnoreCase))
            {
                newChoice = "4";
                newPin = null;
                label = Loc.GetString("Dialog.Ul.Version.LabelV4");
            }
            else if (sel.Equals(UlV3Item, StringComparison.OrdinalIgnoreCase))
            {
                newChoice = "3";
                newPin = null;
                label = Loc.GetString("Dialog.Ul.Version.LabelV3");
            }
            else
            {
                newChoice = null;
                newPin = sel;
                label = sel;
            }

            busy = true;
            combo.IsEnabled = false;
            try
            {
                await ApplyAsync(newChoice, newPin, label);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MainWindow.UlVersion] Version change failed for '{card.GameName}' — {ex.Message}");
                cog.SetError(Loc.GetString("Dialog.Ul.Version.ChangeFailed", ex.Message));
            }
            finally
            {
                busy = false;
                combo.IsEnabled = true;
            }
        };
    }
}
