// DetailPanelBuilder.Extras.CustomDlls.cs — Extras row "Custom DLLs": the user's own single-file DLLs (Special K and
// the like) from %LocalAppData%\RHI\Custom\DLLs\. Install / Manage opens a picker (tick box + "Install as" name per
// DLL of the game's bitness; Apply installs, removes and renames to match), ✕ removes them all (confirm, Cancel
// default), ⚙ shows each DLL's state with Remove and Open folder. File work is CustomDllService's, off the UI thread.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private bool _customDllsHooked;

    private void BuildCustomDllsRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildCustomDllsRow({card.GameName})");
        var vm = _window.ViewModel;
        if (!_customDllsHooked)
        {
            _customDllsHooked = true;
            vm.CustomDllsChanged += () => { if (_currentDetailCard is { } shown) RequestExtrasRebuild(shown); };
        }

        var records = vm.CustomDlls.GetRecords(card.GameName, card.Source ?? "");
        bool installed = records.Count > 0;
        var installPath = card.InstallPath ?? "";
        string? blocked = string.IsNullOrEmpty(installPath) ? Loc.GetString("Extras.CustomDlls.Blocked.NoInstallFolder")
            : XboxPaths.IsWindowsApps(installPath) ? Loc.GetString("Extras.CustomDlls.Blocked.WindowsApps")
            : null;

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExtrasContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        // Col 0 — label (the tooltip's Game: line shows what's installed under the name in Tabs / Spotlight)
        var label = new TextBlock
        {
            Text = Loc.GetString("Extras.CustomDlls.Label"),
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = blocked != null ? 0.35 : 1.0,
        };
        var summary = string.Join(", ", records.Select(r => Loc.GetString("Extras.CustomDlls.Summary.Item", r.Source, r.InstalledAs)));
        ToolTipService.SetToolTip(label,
            Loc.GetString("Extras.CustomDlls.Tooltip")
            + (installed ? "\n" + Loc.GetString("Extras.CustomDlls.Tooltip.Installed", records.Count, summary) : "")
            + (blocked != null ? "\n\n" + blocked : ""));
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Col 1 — status
        var statusBlock = new TextBlock
        {
            Text = installed ? Loc.GetString("Extras.CustomDlls.Status.InstalledCount", records.Count) : Loc.GetString("Status.Ready"),
            FontSize = 12,
            Foreground = UIFactory.GetBrush(installed ? "#5ECB7D" : "#A0AABB"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            Opacity = blocked != null ? 0.35 : 1.0,
        };
        if (installed) ToolTipService.SetToolTip(statusBlock, summary);
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        // Col 2 — Info: opens the folder the DLLs go in
        var infoBtn = new Button
        {
            Content = Loc.GetString("Dialog.Info"),
            FontSize = 11,
            Padding = new Thickness(6, 2, 6, 2),
            Width = 36,
            Height = 32,
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        ToolTipService.SetToolTip(infoBtn, Loc.GetString("Extras.CustomDlls.Info.Tooltip"));
        infoBtn.Click += (s, e) => OpenCustomDllFolder();
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install / Manage (both open the picker)
        var installBtn = new Button
        {
            Content = installed ? Loc.GetString("Dialog.Manage") : Loc.GetString("Dialog.Install"),
            FontSize = 12,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            Background = installed ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = installed ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = installed ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            Tag = card,
            IsEnabled = blocked == null,
            Opacity = blocked != null ? 0.35 : 1.0,
        };
        ToolTipService.SetToolTip(installBtn, installed
            ? Loc.GetString("Extras.CustomDlls.Manage.Tooltip")
            : Loc.GetString("Extras.CustomDlls.Install.Tooltip"));
        installBtn.Click += async (s, e) =>
        {
            try { await ShowCustomDllPickerAsync(card, installBtn.XamlRoot); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            { CrashReporter.Log($"[DetailPanelBuilder.CustomDlls] Picker failed for '{card.GameName}' — {ex.Message}"); }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — ⚙
        var cogBtn = CogDialog.MakeCogButton(Loc.GetString("Extras.CustomDlls.Cog.Tooltip"));
        cogBtn.Tag = card;
        cogBtn.IsEnabled = blocked == null;
        cogBtn.Opacity = blocked != null ? 0.35 : 1.0;
        cogBtn.Click += async (s, e) =>
        {
            try { await ShowCustomDllCogAsync(card, cogBtn.XamlRoot); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            { CrashReporter.Log($"[DetailPanelBuilder.CustomDlls] Settings failed for '{card.GameName}' — {ex.Message}"); }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        // Col 5 — ✕ removes them all (confirm first)
        var removeBtn = new Button
        {
            Width = 36,
            Height = 32,
            Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Tag = card,
            Opacity = installed ? 1.0 : 0.0,
            IsHitTestVisible = installed,
        };
        ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.CustomDlls.RemoveAll.Tooltip"));
        removeBtn.Click += async (s, e) =>
        {
            try { await ConfirmRemoveAllCustomDllsAsync(card, removeBtn.XamlRoot); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            { CrashReporter.Log($"[DetailPanelBuilder.CustomDlls] Remove all failed for '{card.GameName}' — {ex.Message}"); }
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    private void OpenCustomDllFolder()
    {
        try
        {
            var dir = _window.ViewModel.CustomDlls.Folder;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            CrashReporter.Log("[DetailPanelBuilder.CustomDlls] Opened the custom DLL folder");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            CrashReporter.Log($"[DetailPanelBuilder.CustomDlls] Open folder failed — {ex.Message}");
        }
    }

    // ── Picker ─────────────────────────────────────────────────────────────────

    private sealed class CustomDllLine
    {
        public required string Source;
        public required CheckBox Box;
        public required ComboBox Combo;
        public readonly List<(string Name, ComboBoxItem Item)> Items = new();
        public string? Selected => (Combo.SelectedItem as ComboBoxItem)?.Tag as string;
    }

    private async Task ShowCustomDllPickerAsync(GameCardViewModel card, XamlRoot? xamlRoot)
    {
        var vm = _window.ViewModel;
        var svc = vm.CustomDlls;
        xamlRoot ??= _window.Content.XamlRoot;
        if (xamlRoot == null) return;
        var store = card.Source ?? "";

        var sources = await Task.Run(() => svc.ListSources());
        var records = svc.GetRecords(card.GameName, store);
        var owners = vm.NonCustomDllOwners(card);
        var names = CustomDllService.TargetNames(card.Is32Bit);

        var cog = new CogDialog(Loc.GetString("Extras.CustomDlls.Label"), xamlRoot);
        cog.UseApplyButton(Loc.GetString("Dialog.Apply"), Loc.GetString("Dialog.Cancel"));
        cog.AddIntro(Loc.GetString("Dialog.CustomDlls.Picker.Intro", card.GameName));

        var usable = sources.Where(s => s.Usable && s.Is64Bit != card.Is32Bit).ToList();
        var lines = new List<CustomDllLine>();

        void AddLine(string source, string label, CustomDllRecord? rec, bool sourcePresent)
        {
            var box = new CheckBox
            {
                Content = label == source ? source : $"{label} ({source})",
                IsChecked = rec != null,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var combo = new ComboBox { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 200 };
            var line = new CustomDllLine { Source = source, Box = box, Combo = combo };
            var lineNames = new List<string>(names);
            if (rec != null && !lineNames.Contains(rec.InstalledAs, StringComparer.OrdinalIgnoreCase)) lineNames.Insert(0, rec.InstalledAs);
            if (!lineNames.Contains(source, StringComparer.OrdinalIgnoreCase)) lineNames.Add(source);
            foreach (var n in lineNames)
            {
                var item = new ComboBoxItem { Tag = n, Content = n };
                line.Items.Add((n, item));
                combo.Items.Add(item);
            }
            int sel = rec != null ? line.Items.FindIndex(i => i.Name.Equals(rec.InstalledAs, StringComparison.OrdinalIgnoreCase)) : -1;
            combo.SelectedIndex = sel;
            if (!sourcePresent)
            {
                // Its file is gone from the folder: it can still be removed, not installed under a new name
                combo.IsEnabled = false;
                ToolTipService.SetToolTip(box, Loc.GetString("Extras.CustomDlls.Picker.SourceMissing.Tooltip", source));
            }
            lines.Add(line);

            var grid = new Grid { ColumnSpacing = CogDialog.ColumnGap };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            grid.Children.Add(box);
            Grid.SetColumn(combo, 1);
            grid.Children.Add(combo);
            cog.AddContent(grid);
        }

        // Which line (other than this one) has picked a name, and which RHI component holds it
        string? BlockedBy(CustomDllLine self, string name)
        {
            if (owners.TryGetValue(name, out var o)) return Loc.GetString("Extras.CustomDlls.Picker.BlockedByOwner", o);
            var other = lines.FirstOrDefault(l => !ReferenceEquals(l, self) && l.Box.IsChecked == true
                && string.Equals(l.Selected, name, StringComparison.OrdinalIgnoreCase));
            return other != null ? Loc.GetString("Extras.CustomDlls.Picker.BlockedByPick", other.Source) : null;
        }

        bool refreshing = false;
        void Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                // Twice: a name picked for one line in the first pass greys it out for the lines before it
                for (int pass = 0; pass < 2; pass++)
                foreach (var l in lines)
                {
                    foreach (var (n, item) in l.Items)
                    {
                        var why = BlockedBy(l, n);
                        item.IsEnabled = why == null;
                        item.Content = why == null ? n : $"{n} ({why})";
                    }
                    if (l.Box.IsChecked == true && l.Combo.IsEnabled
                        && (l.Selected == null || BlockedBy(l, l.Selected) != null))
                    {
                        // First free name in the list
                        var free = l.Items.FindIndex(i => BlockedBy(l, i.Name) == null);
                        l.Combo.SelectedIndex = free;
                    }
                }
            }
            finally { refreshing = false; }
        }

        if (usable.Count > 0 || records.Count > 0)
        {
            cog.AddSection(Loc.GetString("Extras.CustomDlls.Picker.Section.Folder"));
            foreach (var rec in records)
            {
                var src = sources.FirstOrDefault(s => s.FileName.Equals(rec.Source, StringComparison.OrdinalIgnoreCase));
                AddLine(rec.Source, src?.Label ?? rec.Source, rec, src != null);
            }
            foreach (var s in usable.Where(s => !records.Any(r => r.Source.Equals(s.FileName, StringComparison.OrdinalIgnoreCase))))
                AddLine(s.FileName, s.Label, null, sourcePresent: true);
            foreach (var l in lines)
            {
                l.Box.Checked += (_, _) => Refresh();
                l.Box.Unchecked += (_, _) => Refresh();
                l.Combo.SelectionChanged += (_, _) => Refresh();
            }
            Refresh();
        }
        else
        {
            cog.AddEmpty(Loc.GetString("Extras.CustomDlls.Picker.Empty", card.Is32Bit ? "32" : "64"));
        }

        var unusable = sources.Where(s => !s.Usable || s.Is64Bit == card.Is32Bit).ToList();
        if (unusable.Count > 0)
            cog.AddNote(Loc.GetString("Extras.CustomDlls.Picker.NotOfferedPrefix") + string.Join("; ", unusable.Select(s => s.Usable
                ? Loc.GetString("Extras.CustomDlls.Picker.NotOffered.Bitness", s.FileName, s.Is64Bit ? "64" : "32")
                : $"{s.FileName} ({s.Problem})")) + ".");
        cog.AddNote(Loc.GetString("Extras.CustomDlls.Picker.Hint.Original"));
        cog.AddButtons(cog.MakeButton(Loc.GetString("Detail.OpenFolder"), CogButtonKind.Secondary, OpenCustomDllFolder,
            Loc.GetString("Extras.CustomDlls.OpenFolder.Tooltip")));

        var result = await cog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        // What changes: removals (unticked or renamed) first, then installs
        var desired = lines.Where(l => l.Box.IsChecked == true && l.Selected != null)
            .ToDictionary(l => l.Source, l => l.Selected!, StringComparer.OrdinalIgnoreCase);
        var toRemove = records.Where(r => !desired.TryGetValue(r.Source, out var t)
            || !t.Equals(r.InstalledAs, StringComparison.OrdinalIgnoreCase)).Select(r => r.Source).ToList();
        var toInstall = desired.Where(d => !records.Any(r => r.Source.Equals(d.Key, StringComparison.OrdinalIgnoreCase)
            && r.InstalledAs.Equals(d.Value, StringComparison.OrdinalIgnoreCase))).ToList();
        if (toRemove.Count == 0 && toInstall.Count == 0) return;

        if (toInstall.Count > 0 && records.Count == 0
            && !await ConfirmAntiCheatAsync(card, Loc.GetString("Extras.CustomDlls.AntiCheatLabel"), AntiCheatNoticeKey.CustomDll, xamlRoot)) return;

        var game = MainViewModel.CustomDllGameFor(card);
        if (CustomDllService.FolderBlockedReason(game) is { } refused)
        {
            await ShowCustomDllMessagesAsync(xamlRoot, new[] { refused });
            return;
        }

        var messages = await Task.Run(() =>
        {
            var msgs = new List<string>();
            foreach (var source in toRemove)
                if (svc.Remove(game, source) is { } m) msgs.Add($"{source}: {m}");
            foreach (var (source, target) in toInstall)
                if (svc.Install(game, source, target, owners) is { } m) msgs.Add($"{source}: {m}");
            return msgs;
        });
        CrashReporter.Log($"[DetailPanelBuilder.CustomDlls] '{card.GameName}': {toRemove.Count} removed, {toInstall.Count} to install, {messages.Count} message(s)");
        RequestExtrasRebuild(card);
        if (messages.Count > 0) await ShowCustomDllMessagesAsync(xamlRoot, messages);
        else _window.ShowStatusBarMessage(Loc.GetString("Extras.CustomDlls.StatusBar.Updated", card.GameName), TimeSpan.FromSeconds(6));
    }

    private static async Task ShowCustomDllMessagesAsync(XamlRoot xamlRoot, IEnumerable<string> messages)
    {
        var dlg = new ContentDialog
        {
            Title = Loc.GetString("Extras.CustomDlls.Label"),
            Content = new TextBlock
            {
                Text = string.Join("\n\n", messages),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            },
            CloseButtonText = Loc.GetString("Dialog.Ok"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(dlg);
    }

    // ── ⚙ ─────────────────────────────────────────────────────────────────────

    private async Task ShowCustomDllCogAsync(GameCardViewModel card, XamlRoot? xamlRoot)
    {
        var vm = _window.ViewModel;
        var svc = vm.CustomDlls;
        xamlRoot ??= _window.Content.XamlRoot;
        if (xamlRoot == null) return;
        var store = card.Source ?? "";

        var (states, sources) = await Task.Run(() => (svc.GetStates(card.GameName, store), svc.ListSources()));

        var cog = new CogDialog(Loc.GetString("Extras.CustomDlls.Label"), xamlRoot);
        cog.AddIntro(Loc.GetString("Dialog.CustomDlls.Cog.Intro"));
        cog.AddButtons(cog.MakeButton(Loc.GetString("Detail.OpenFolder"), CogButtonKind.Secondary, OpenCustomDllFolder, Loc.GetString("Extras.CustomDlls.OpenFolder.Tooltip")));

        if (states.Count == 0)
        {
            cog.AddNote(Loc.GetString("Extras.CustomDlls.Cog.None"));
        }
        else
        {
            cog.AddSection(Loc.GetString("Status.Installed"));
            var game = MainViewModel.CustomDllGameFor(card);
            foreach (var (rec, state) in states)
            {
                var label = sources.FirstOrDefault(s => s.FileName.Equals(rec.Source, StringComparison.OrdinalIgnoreCase))?.Label ?? rec.Source;
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
                text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = label == rec.Source ? rec.Source : $"{label} ({rec.Source})" });
                text.Inlines.Add(new Microsoft.UI.Xaml.Documents.LineBreak());
                text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                {
                    Text = Loc.GetString("Extras.CustomDlls.Cog.InstalledAs", rec.InstalledAs, StateText(rec, state)),
                    Foreground = UIFactory.Brush(state is CustomDllState.UpToDate or CustomDllState.UpdatePending
                        ? ResourceKeys.TextTertiaryBrush : ResourceKeys.AccentAmberBrush),
                });

                var source = rec.Source;
                Button? removeBtn = null;
                removeBtn = cog.MakeButton(Loc.GetString("Dialog.Remove"), CogButtonKind.Danger, async () =>
                {
                    removeBtn!.IsEnabled = false;
                    var msg = await Task.Run(() => svc.Remove(game, source));
                    if (msg == null || msg.StartsWith("Note:", StringComparison.Ordinal))
                        cog.SetSuccess(msg ?? Loc.GetString("Extras.CustomDlls.Cog.Remove.Success", source, card.GameName));
                    else
                    {
                        cog.SetError(msg);
                        removeBtn.IsEnabled = true;
                    }
                    RequestExtrasRebuild(card);
                }, Loc.GetString("Extras.CustomDlls.Cog.Remove.Tooltip", source, rec.InstalledAs));

                var grid = new Grid { ColumnSpacing = CogDialog.ColumnGap };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.Children.Add(text);
                removeBtn.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(removeBtn, 1);
                grid.Children.Add(removeBtn);
                cog.AddContent(grid);
            }
        }

        cog.AddNote(Loc.GetString("Extras.CustomDlls.Cog.Note"));
        await cog.ShowAsync();
    }

    private static string StateText(CustomDllRecord rec, CustomDllState state) => state switch
    {
        CustomDllState.UpToDate => rec.UpdatedUtc is { } u ? Loc.GetString("Extras.CustomDlls.State.Updated", u.ToLocalTime().ToString("g")) : Loc.GetString("Extras.CustomDlls.State.UpToDate"),
        CustomDllState.UpdatePending => Loc.GetString("Extras.CustomDlls.State.UpdatePending"),
        CustomDllState.SourceMissing => Loc.GetString("Extras.CustomDlls.State.SourceMissing"),
        CustomDllState.ChangedOutsideRhi => Loc.GetString("Extras.CustomDlls.State.ChangedOutsideRhi"),
        CustomDllState.Missing => Loc.GetString("Extras.CustomDlls.State.Missing"),
        _ => "",
    };

    // ── ✕ ─────────────────────────────────────────────────────────────────────

    private async Task ConfirmRemoveAllCustomDllsAsync(GameCardViewModel card, XamlRoot? xamlRoot)
    {
        var svc = _window.ViewModel.CustomDlls;
        xamlRoot ??= _window.Content.XamlRoot;
        if (xamlRoot == null) return;
        var records = svc.GetRecords(card.GameName, card.Source ?? "");
        if (records.Count == 0) return;

        var dlg = new ContentDialog
        {
            Title = Loc.GetString("Dialog.CustomDlls.RemoveAll.Title"),
            Content = new TextBlock
            {
                Text = (records.Count == 1
                    ? Loc.GetString("Dialog.CustomDlls.RemoveAll.Content.One", card.GameName,
                        string.Join("\n", records.Select(r => Loc.GetString("Extras.CustomDlls.RemoveAll.Item", r.Source, r.InstalledAs))))
                    : Loc.GetString("Dialog.CustomDlls.RemoveAll.Content.Many", records.Count, card.GameName,
                        string.Join("\n", records.Select(r => Loc.GetString("Extras.CustomDlls.RemoveAll.Item", r.Source, r.InstalledAs))))),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            },
            PrimaryButtonText = Loc.GetString("Dialog.Remove"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        if (await DialogService.ShowSafeAsync(dlg) != ContentDialogResult.Primary) return;

        var game = MainViewModel.CustomDllGameFor(card);
        var messages = await Task.Run(() =>
        {
            var msgs = new List<string>();
            foreach (var r in records)
                if (svc.Remove(game, r.Source) is { } m) msgs.Add($"{r.Source}: {m}");
            return msgs;
        });
        CrashReporter.Log($"[DetailPanelBuilder.CustomDlls] '{card.GameName}': remove all ({records.Count}), {messages.Count} message(s)");
        RequestExtrasRebuild(card);
        if (messages.Count > 0) await ShowCustomDllMessagesAsync(xamlRoot, messages);
    }
}
