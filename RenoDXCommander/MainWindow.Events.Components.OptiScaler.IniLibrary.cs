// MainWindow.Events.Components.OptiScaler.IniLibrary.cs — "Save ini..." and "Load saved ini..." in the
// OptiScaler cog's footer. Saved inis live in OsIniLibrary (any number, named by the user); loading one
// makes it the game's OptiScaler.ini and remembers it, so a reinstall or update puts it back.
// Both use flyouts: a second dialog can't open over the cog.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    /// <summary>Shown in the cog's status line after it reopens (e.g. after loading a saved ini).</summary>
    private string? _osCogNotice;

    private Button BuildOsSaveIniButton(OsCogContext ctx, string? iniPath, Action<string, bool> showStatus)
    {
        var card = ctx.Card;
        var btn = CogDialog.StyledButton(Loc.GetString("Dialog.OptiScaler.SaveIniButton"), CogButtonKind.Secondary);
        ToolTipService.SetToolTip(btn,
            Loc.GetString("Dialog.OptiScaler.SaveIniTooltip"));

        var nameBox = CogDialog.MakeTextBox("", Loc.GetString("Dialog.OptiScaler.SaveIniNamePlaceholder"));
        nameBox.MaxLength = OsIniLibrary.MaxNameLength;
        var note = MakeOsFooterNote("");
        var saveBtn = CogDialog.StyledButton(Loc.GetString("Common.Save"), CogButtonKind.Primary, compact: true);
        var panel = new StackPanel { Spacing = 8, Width = 300 };
        panel.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Dialog.OptiScaler.SaveIniPrompt"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        });
        panel.Children.Add(nameBox);
        panel.Children.Add(note);
        panel.Children.Add(saveBtn);
        var flyout = new Flyout { Content = panel };
        btn.Flyout = flyout;

        void ShowNote(string text, bool error)
        {
            note.Text = text;
            note.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextTertiaryBrush);
        }

        flyout.Opening += (_, _) =>
        {
            nameBox.Text = ViewModel.GetOsIniProfile(card.GameName, ctx.Store) ?? "";
            ShowNote("", false);
        };
        nameBox.TextChanged += (_, _) =>
        {
            var name = OsIniLibrary.CleanName(nameBox.Text);
            ShowNote(name != null && OsIniLibrary.Exists(name) ? Loc.GetString("Dialog.OptiScaler.SaveIniReplaces", name) : "", false);
        };

        async Task SaveAsync()
        {
            try
            {
                var name = OsIniLibrary.CleanName(nameBox.Text);
                if (name == null) { ShowNote(Loc.GetString("Dialog.OptiScaler.SaveIniNameRequired"), true); return; }
                if (iniPath == null || !File.Exists(iniPath)) { ShowNote(Loc.GetString("Dialog.OptiScaler.SaveIniNoFile"), true); return; }
                bool replaced = OsIniLibrary.Exists(name);
                await Task.Run(() => OsIniLibrary.Save(name, iniPath));
                // The game now matches the saved ini, so a reinstall puts it back
                ViewModel.SetOsIniProfile(card.GameName, name, ctx.Store);
                flyout.Hide();
                showStatus(replaced
                    ? Loc.GetString("Dialog.OptiScaler.SaveIniReplaced", name)
                    : Loc.GetString("Dialog.OptiScaler.SaveIniSavedAs", name), true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashReporter.Log($"[MainWindow.BuildOsSaveIniButton] Save failed — {ex.Message}");
                ShowNote(Loc.GetString("Dialog.OptiScaler.SaveIniFailed", ex.Message), true);
            }
        }

        saveBtn.Click += async (_, _) => await SaveAsync();
        nameBox.KeyDown += async (_, e) =>
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            e.Handled = true;
            await SaveAsync();
        };
        return btn;
    }

    private Button BuildOsLoadIniButton(OsCogContext ctx, Action<string, bool> showStatus)
    {
        var card = ctx.Card;
        var store = ctx.Store;
        var btn = CogDialog.StyledButton(Loc.GetString("Dialog.OptiScaler.LoadIniButton"), CogButtonKind.Secondary);
        ToolTipService.SetToolTip(btn,
            Loc.GetString("Dialog.OptiScaler.LoadIniTooltip"));

        var panel = new StackPanel { Spacing = 8, Width = 320 };
        var flyout = new Flyout { Content = panel };
        btn.Flyout = flyout;

        void Fill()
        {
            panel.Children.Clear();
            var names = OsIniLibrary.List();
            var note = MakeOsFooterNote("");

            void ShowNote(string text, bool error)
            {
                note.Text = text;
                note.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextTertiaryBrush);
            }

            var addBtn = CogDialog.StyledButton(Loc.GetString("Dialog.OptiScaler.AddIniButton"), CogButtonKind.Secondary, compact: true);
            ToolTipService.SetToolTip(addBtn, Loc.GetString("Dialog.OptiScaler.AddIniTooltip"));
            addBtn.Click += async (_, _) =>
            {
                try
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    // Resolve the localized title on the UI thread (Loc is a DI lookup)
                    var addTitle = Loc.GetString("Dialog.OptiScaler.AddIniTitle");
                    var file = await Task.Run(() =>
                    {
                        var ofn = new NativeInterop.OpenFileName();
                        ofn.structSize = System.Runtime.InteropServices.Marshal.SizeOf(ofn);
                        ofn.hwndOwner = hwnd;
                        ofn.filter = "INI files (*.ini)\0*.ini\0All Files (*.*)\0*.*\0";
                        ofn.file = new string(new char[260]);
                        ofn.maxFile = ofn.file.Length;
                        ofn.title = addTitle;
                        ofn.flags = 0x00080000 | 0x00001000; // OFN_EXPLORER | OFN_FILEMUSTEXIST
                        return NativeInterop.GetOpenFileName(ref ofn) ? ofn.file.TrimEnd('\0') : null;
                    });
                    if (string.IsNullOrEmpty(file)) return;
                    var name = OsIniLibrary.SuggestName(file);
                    await Task.Run(() => OsIniLibrary.Save(name, file));
                    Fill();
                    flyout.ShowAt(btn);
                    showStatus(Loc.GetString("Dialog.OptiScaler.AddIniDone", name), true);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MainWindow.BuildOsLoadIniButton] Add from file failed — {ex.Message}");
                    ShowNote(Loc.GetString("Dialog.OptiScaler.AddIniFailed", ex.Message), true);
                }
            };

            var folderBtn = CogDialog.StyledButton(Loc.GetString("Detail.OpenFolder"), CogButtonKind.Secondary, compact: true);
            ToolTipService.SetToolTip(folderBtn, Loc.GetString("Dialog.OptiScaler.OpenIniFolderTooltip"));
            folderBtn.Click += async (_, _) =>
            {
                try
                {
                    Directory.CreateDirectory(OsIniLibrary.Dir);
                    await Windows.System.Launcher.LaunchFolderPathAsync(OsIniLibrary.Dir);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MainWindow.BuildOsLoadIniButton] Open folder failed — {ex.Message}");
                    ShowNote(Loc.GetString("Dialog.OptiScaler.OpenIniFolderFailed", ex.Message), true);
                }
            };

            if (names.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = Loc.GetString("Dialog.OptiScaler.NoSavedInis"),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                });
                var emptyRow = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
                emptyRow.Children.Add(addBtn);
                emptyRow.Children.Add(folderBtn);
                panel.Children.Add(emptyRow);
                panel.Children.Add(note);
                return;
            }

            var current = ViewModel.GetOsIniProfile(card.GameName, store);
            var combo = CogDialog.MakeCombo(names, Math.Max(0, names.FindIndex(n => n.Equals(current, StringComparison.OrdinalIgnoreCase))));
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;

            var useBtn = CogDialog.StyledButton(Loc.GetString("Dialog.OptiScaler.UseIniButton"), CogButtonKind.Primary, compact: true);
            useBtn.Click += async (_, _) =>
            {
                if (combo.SelectedItem is not string name || string.IsNullOrEmpty(card.InstallPath)) return;
                var gameDir = card.InstallPath;
                var err = await Task.Run(() => OsIniLibrary.Deploy(name, gameDir, ViewModel.Settings.OsHotkey));
                if (err != null) { ShowNote(Loc.GetString("Dialog.OptiScaler.LoadIniFailed", err), true); return; }

                ViewModel.SetOsIniProfile(card.GameName, name, store);
                // Edits applied to the old ini don't belong to this one
                ViewModel.SetOsIniEdits(card.GameName, null, store);
                SyncOsFgSettingsFromIni(card.GameName, store, gameDir);
                CrashReporter.Log($"[MainWindow.BuildOsLoadIniButton] '{card.GameName}' now uses saved ini '{name}'");
                flyout.Hide();
                _osCogNotice = Loc.GetString("Dialog.OptiScaler.LoadIniNotice", name);
                ReopenOsCog(ctx); // every setting above now reads the new ini
            };

            // Deleting asks for a second click
            var deleteBtn = CogDialog.StyledButton(Loc.GetString("Dialog.Delete"), CogButtonKind.Danger, compact: true);
            bool armed = false;
            deleteBtn.Click += async (_, _) =>
            {
                if (combo.SelectedItem is not string name) return;
                if (!armed)
                {
                    armed = true;
                    deleteBtn.Content = Loc.GetString("Dialog.OptiScaler.DeleteIniArmed");
                    ShowNote(Loc.GetString("Dialog.OptiScaler.DeleteIniConfirm", name), false);
                    return;
                }
                try
                {
                    await Task.Run(() => OsIniLibrary.Delete(name));
                    if (name.Equals(ViewModel.GetOsIniProfile(card.GameName, store), StringComparison.OrdinalIgnoreCase))
                        ViewModel.SetOsIniProfile(card.GameName, null, store);
                    showStatus(Loc.GetString("Dialog.OptiScaler.DeleteIniDone", name), true);
                    Fill();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    CrashReporter.Log($"[MainWindow.BuildOsLoadIniButton] Delete failed — {ex.Message}");
                    ShowNote(Loc.GetString("Dialog.OptiScaler.DeleteIniFailed", ex.Message), true);
                }
            };
            combo.SelectionChanged += (_, _) =>
            {
                armed = false;
                deleteBtn.Content = Loc.GetString("Dialog.Delete");
                ShowNote("", false);
            };

            panel.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Dialog.OptiScaler.LoadIniPrompt"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
            });
            panel.Children.Add(combo);
            var row = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            row.Children.Add(useBtn);
            row.Children.Add(deleteBtn);
            row.Children.Add(addBtn);
            row.Children.Add(folderBtn);
            panel.Children.Add(row);
            panel.Children.Add(note);
        }

        flyout.Opening += (_, _) => Fill();
        return btn;
    }

    /// <summary>The Frame Generation dropdowns keep their own saved values; match them to the game's ini.</summary>
    private void SyncOsFgSettingsFromIni(string gameName, string store, string gameDir)
    {
        try
        {
            var settings = OsIniEditor.ParseFile(Path.Combine(gameDir, OptiScalerService.IniFileName));
            string? Val(string key) => settings.FirstOrDefault(s =>
                s.Section.Equals("FrameGen", StringComparison.OrdinalIgnoreCase) && s.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
            ViewModel.SetOsFgInput(gameName, string.IsNullOrEmpty(Val("FGInput")) ? "auto" : Val("FGInput")!, store);
            ViewModel.SetOsFgOutput(gameName, string.IsNullOrEmpty(Val("FGOutput")) ? "auto" : Val("FGOutput")!, store);
            var nvngx = Val("FGNvngxReplacement");
            ViewModel.SetOsFgNvngxReplacement(gameName,
                string.IsNullOrEmpty(nvngx) || nvngx.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "None" : nvngx, store);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashReporter.Log($"[MainWindow.SyncOsFgSettingsFromIni] Failed — {ex.Message}");
        }
    }
}
