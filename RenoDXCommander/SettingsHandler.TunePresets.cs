// SettingsHandler.TunePresets.cs -- Settings › "Tune presets" section (built in C#, XAML constraint), inserted before
// Components so it gets a jump button: the on/off switch for the Tune button on game pages, the preset list (edit,
// rename, move, hide, delete; the built-in RHI Tuned can be reset instead of deleted) and Add preset.
// Data: MainViewModel.Tune.cs. Editor: ShowTunePresetEditorAsync (a CogDialog).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private const string TuneSectionKey = "Tune";
    private StackPanel? _tuneBody;
    private TextBlock? _tuneStatus;
    private bool _tuneSectionFailed;

    private void EnsureTunePresetsSection()
    {
        if (_tuneBody != null) { RefreshTunePresetsCard(); return; }
        if (_tuneSectionFailed) return;
        try
        {
            var panel = _window.SettingsCardsPanel;
            int index = -1;
            for (int i = 0; i < panel.Children.Count; i++)
                if (panel.Children[i] is StackPanel sp && sp.Tag is string t && t == "Section:Components") { index = i; break; }
            if (index < 0)
            {
                _tuneSectionFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureTunePresetsSection] Components section not found — section not added");
                return;
            }

            var section = new StackPanel { Tag = "Section:" + TuneSectionKey, Spacing = 12 };
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.TunePresets.Title"),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.TunePresets.Intro"),
                FontSize = 12,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            });
            section.Children.Add(head);

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var outer = new StackPanel { Spacing = 10 };
            card.Child = outer;
            var cardHead = new StackPanel { Spacing = 3 };
            cardHead.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Presets"), Style = FindStyle("SettingsCardTitleStyle") });
            cardHead.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.TunePresets.CardDesc"),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            outer.Children.Add(cardHead);

            _tuneBody = new StackPanel { Spacing = 10 };
            outer.Children.Add(_tuneBody);
            _tuneStatus = new TextBlock { Style = FindStyle("SettingsNoteStyle"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            outer.Children.Add(_tuneStatus);

            section.Children.Add(card);
            panel.Children.Insert(index, section);
            ViewModel.TunePresetsChanged += () => _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window.SettingsPanel.Visibility == Visibility.Visible) RefreshTunePresetsCard();
            });
            RefreshTunePresetsCard();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            _tuneSectionFailed = true;
            _tuneBody = null;
            CrashReporter.Log($"[SettingsHandler.EnsureTunePresetsSection] Failed — {ex.Message}");
        }
    }

    /// <summary>Opens the Tune presets section (from a game page's Tune menu).</summary>
    internal void JumpToTunePresets() => JumpToSection(TuneSectionKey, animate: true, saveAsLast: false);

    private void RefreshTunePresetsCard()
    {
        if (_tuneBody == null) return;
        _tuneBody.Children.Clear();
        var vm = ViewModel;
        var store = vm.TunePresetStore;
        var presets = store.Presets;

        var onCombo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
        onCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Xaml.Off") });
        onCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Xaml.On") });
        onCombo.SelectedIndex = vm.TunePresetsOn ? 1 : 0;
        ToolTipService.SetToolTip(onCombo, Loc.GetString("Settings.TunePresets.ToggleTooltip"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(onCombo, Loc.GetString("Settings.TunePresets.ToggleName"));
        onCombo.SelectionChanged += (s, e) =>
        {
            if (onCombo.SelectedIndex < 0) return;
            vm.SetTunePresetsEnabled(onCombo.SelectedIndex == 1);
        };
        _tuneBody.Children.Add(SettingRow(Loc.GetString("Settings.TunePresets.ShowOnGamePages"), onCombo));

        _tuneBody.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Presets"), Style = FindStyle("SettingsHeadStyle") });
        for (int i = 0; i < presets.Count; i++)
        {
            var p = presets[i];
            var id = p.Id;
            var name = p.Name;
            bool builtIn = TunePresetStore.IsBuiltIn(id);
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int steps = p.StepCount;
            int tuned = store.CountTunedWith(id);
            var stepsText = steps == 0 ? Loc.GetString("Settings.TunePresets.NothingSet")
                : steps == 1 ? Loc.GetString("Settings.TunePresets.StepsOne", steps)
                : Loc.GetString("Settings.TunePresets.StepsMany", steps);
            var text = $"{name}  ({stepsText})";
            if (p.Hidden) text += Loc.GetString("Settings.TunePresets.HiddenSuffix");
            if (tuned > 0) text += tuned == 1 ? Loc.GetString("Settings.TunePresets.TunedOneGame", tuned) : Loc.GetString("Settings.TunePresets.TunedGames", tuned);
            var label = new TextBlock
            {
                Text = text,
                Style = FindStyle("SettingsLabelStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Opacity = p.Hidden ? 0.6 : 1.0,
            };
            ToolTipService.SetToolTip(label, TuneSummary(p));
            row.Children.Add(label);

            var buttons = new Controls.WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(DmButton(Loc.GetString("Settings.DisplayModes.DefaultsButton"), DmKind.Normal, Loc.GetString("Settings.TunePresets.EditTooltip", name),
                async () => await ShowTunePresetEditorAsync(id)));
            buttons.Children.Add(DmButton(Loc.GetString("Collections.Dialog.Rename"), DmKind.Normal, Loc.GetString("Settings.TunePresets.RenameTooltip", name), async () =>
            {
                var newName = await PromptTuneNameAsync(Loc.GetString("Settings.TunePresets.RenameTitle"), Loc.GetString("Settings.TunePresets.RenamePrompt", name), Loc.GetString("Collections.Dialog.Rename"), name, id);
                if (newName != null && vm.RenameTunePreset(id, newName)) SetTuneStatus(Loc.GetString("Settings.TunePresets.StatusRenamed", name, newName), false);
            }));
            var up = DmButton(Loc.GetString("Settings.DisplayModes.MoveUpButton"), DmKind.Normal, Loc.GetString("Settings.TunePresets.MoveUpTooltip"),
                async () => { vm.MoveTunePreset(id, -1); await Task.CompletedTask; });
            up.IsEnabled = i > 0;
            buttons.Children.Add(up);
            var down = DmButton(Loc.GetString("Settings.DisplayModes.MoveDownButton"), DmKind.Normal, Loc.GetString("Settings.TunePresets.MoveDownTooltip"),
                async () => { vm.MoveTunePreset(id, 1); await Task.CompletedTask; });
            down.IsEnabled = i < presets.Count - 1;
            buttons.Children.Add(down);
            buttons.Children.Add(DmButton(p.Hidden ? Loc.GetString("CardMenu.Show") : Loc.GetString("CardMenu.Hide"), DmKind.Normal,
                p.Hidden ? Loc.GetString("Settings.TunePresets.ShowTooltip", name) : Loc.GetString("Settings.TunePresets.HideTooltip", name),
                async () => { vm.SetTunePresetHidden(id, !p.Hidden); await Task.CompletedTask; }));
            if (builtIn)
                buttons.Children.Add(DmButton(Loc.GetString("Xaml.Reset"), DmKind.Danger, Loc.GetString("Settings.TunePresets.ResetTooltip", name), async () =>
                {
                    if (!await ConfirmAsync(Loc.GetString("Settings.TunePresets.ResetTitle"), Loc.GetString("Settings.TunePresets.ResetPrompt", name), Loc.GetString("Xaml.Reset"))) return;
                    if (vm.ResetBuiltInTunePreset()) SetTuneStatus(Loc.GetString("Settings.TunePresets.StatusReset", name), false);
                }));
            else
                buttons.Children.Add(DmButton(Loc.GetString("Dialog.Delete"), DmKind.Danger, Loc.GetString("Settings.TunePresets.DeleteTooltip", name), async () =>
                {
                    var note = tuned > 0 ? (tuned == 1 ? Loc.GetString("Settings.TunePresets.DeleteNoteOneGame", tuned) : Loc.GetString("Settings.TunePresets.DeleteNoteGames", tuned)) : "";
                    if (!await ConfirmAsync(Loc.GetString("Settings.TunePresets.DeleteTitle"), Loc.GetString("Settings.TunePresets.DeletePrompt", name, note), Loc.GetString("Dialog.Delete"))) return;
                    if (vm.DeleteTunePreset(id)) SetTuneStatus(Loc.GetString("Settings.TunePresets.StatusDeleted", name), false);
                }));
            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);
            _tuneBody.Children.Add(row);
        }

        var addRow = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
        var addBtn = DmButton(Loc.GetString("Settings.TunePresets.AddPreset"), DmKind.Primary, Loc.GetString("Settings.TunePresets.AddPresetTooltip", TunePresetStore.MaxPresets), async () =>
        {
            var newName = await PromptTuneNameAsync(Loc.GetString("Settings.TunePresets.AddPreset"), Loc.GetString("Settings.TunePresets.AddNamePrompt"), Loc.GetString("Settings.DisplayModes.AddDialog.Primary"), "", null);
            if (newName == null) return;
            var newId = vm.AddTunePreset(newName);
            if (newId == null) return;
            SetTuneStatus(Loc.GetString("Settings.TunePresets.StatusAdded", newName), false);
            await ShowTunePresetEditorAsync(newId);
        });
        addBtn.IsEnabled = presets.Count < TunePresetStore.MaxPresets;
        addRow.Children.Add(addBtn);
        _tuneBody.Children.Add(addRow);
        _tuneBody.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Settings.TunePresets.MultiPresetHelp"),
            Style = FindStyle("SettingsHelpStyle"),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private void SetTuneStatus(string text, bool error)
    {
        if (_tuneStatus == null) return;
        _tuneStatus.Text = text;
        _tuneStatus.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextSecondaryBrush);
        _tuneStatus.Visibility = Visibility.Visible;
    }

    /// <summary>What a preset does, one line per step (the row's tooltip).</summary>
    private static string TuneSummary(TunePreset p)
    {
        var lines = new List<string>();
        if (p.ReShade) lines.Add(Loc.GetString("Tune.Step.Install", "ReShade"));
        if (!string.IsNullOrEmpty(p.HdrMod)) lines.Add(Loc.GetString("Tune.Step.Install", HdrLabel(p.HdrMod)));
        if (!string.IsNullOrEmpty(p.FrameLimiter)) lines.Add(Loc.GetString("Tune.Step.Install", LimiterLabel(p.FrameLimiter)));
        if (p.OptiScaler) lines.Add(string.IsNullOrEmpty(p.OsDllName) ? Loc.GetString("Tune.Summary.InstallOptiScaler") : Loc.GetString("Tune.Summary.InstallOptiScalerAs", p.OsDllName));
        foreach (var id in TuneComponentIds.ExtraOrder)
            if (p.Extras.TryGetValue(id, out var opt))
                lines.Add(Loc.GetString("Tune.Step.Install", DetailPanelBuilder.TuneExtraLabel(id)) + (string.IsNullOrEmpty(opt) ? "" : $" ({opt})"));
        if (!string.IsNullOrEmpty(p.NrMethod))
            lines.Add(Loc.GetString("Tune.Summary.NeuralRenderingLine",
                (p.NrMethod == TuneNr.Auto ? Loc.GetString("Option.Auto") : DetailPanelBuilder.NrMethodLabel(p.NrMethod))
                    + (string.IsNullOrEmpty(p.NrAddonVersion) ? "" : $" {p.NrAddonVersion}"),
                string.IsNullOrEmpty(p.NrDllVersion) ? Loc.GetString("Tune.Summary.Latest") : p.NrDllVersion));
        void Dll(string label, string v) { if (!string.IsNullOrEmpty(v)) lines.Add($"{label}: {(v == TuneDll.Newest ? Loc.GetString("Tune.Summary.Newest") : v)}"); }
        Dll("DLSS Super Resolution", p.DlssSr);
        Dll("DLSS Ray Reconstruction", p.DlssRr);
        Dll("DLSS Frame Generation", p.DlssFg);
        Dll("Streamline", p.Streamline);
        foreach (var item in TuneItems.All)
            if (p.Nvidia.TryGetValue(item.Key, out var v)) lines.Add($"{TunePlanner.NvidiaLabel(item)}: {item.Format(v)}");
        return lines.Count == 0 ? Loc.GetString("Tune.Summary.DoesNothing") : string.Join("\n", lines);
    }

    private static string HdrLabel(string v) => v switch
    {
        TuneHdrMod.RenoDx => "RenoDX",
        TuneHdrMod.Luma => "Luma",
        TuneHdrMod.RenoDxElseLuma => "RenoDX, else Luma",
        _ => "None",
    };

    private static string LimiterLabel(string v) => v switch
    {
        TuneLimiter.ReLimiter => "ReLimiter",
        TuneLimiter.DisplayCommander => "Display Commander",
        _ => "None",
    };

    /// <summary>Asks for a preset name, checked as it's typed. Null when cancelled.</summary>
    private async Task<string?> PromptTuneNameAsync(string title, string prompt, string primary, string initial, string? renamingId)
    {
        var store = ViewModel.TunePresetStore;
        var box = new TextBox { Text = initial, MaxLength = TunePresetStore.MaxNameLength, Width = 340, PlaceholderText = Loc.GetString("Settings.TunePresets.NamePlaceholder") };
        var error = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = TabsTheme.B("#FF9AA8"), Visibility = Visibility.Collapsed };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        panel.Children.Add(error);
        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primary,
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        string? Check() => store.ValidateName(box.Text, renamingId);
        void ShowCheck(bool showEmpty)
        {
            var problem = Check();
            dialog.IsPrimaryButtonEnabled = problem == null;
            error.Text = problem ?? "";
            error.Visibility = problem != null && (showEmpty || !string.IsNullOrWhiteSpace(box.Text)) ? Visibility.Visible : Visibility.Collapsed;
        }
        box.TextChanged += (s, e) => ShowCheck(false);
        dialog.PrimaryButtonClick += (s, e) => { if (Check() != null) { e.Cancel = true; ShowCheck(true); } };
        box.Loaded += (s, e) => { box.Focus(FocusState.Programmatic); box.SelectAll(); };
        ShowCheck(false);
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return null;
        return box.Text.Trim();
    }

    // ── Editor ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A preset's settings: every choice saves at once. "Leave" = Tune doesn't touch it. Laid out two per row
    /// (dense settings, user preference), help in tooltips.
    /// </summary>
    private async Task ShowTunePresetEditorAsync(string id)
    {
        var vm = ViewModel;
        var preset = vm.TunePresetStore.Get(id);
        if (preset == null) return;
        var cog = new CogDialog(Loc.GetString("Tune.Editor.Title", preset.Name), _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Tune.Editor.Intro"));

        void Save()
        {
            if (!vm.UpdateTunePreset(preset)) cog.SetError(Loc.GetString("Tune.Editor.SaveError"));
            else cog.ClearStatus();
        }

        var dlss = App.Services.GetRequiredService<IDlssStreamlineService>();

        // ── Components ──
        cog.AddSection(Loc.GetString("Tune.Editor.SectionComponents"));
        var hdrValues = new[] { TuneHdrMod.None, TuneHdrMod.RenoDx, TuneHdrMod.Luma, TuneHdrMod.RenoDxElseLuma };
        var limValues = new[] { TuneLimiter.None, TuneLimiter.ReLimiter, TuneLimiter.DisplayCommander };
        cog.AddCompactGrid(
            cog.CompactCombo("ReShade", Loc.GetString("Tune.Help.ReShade"),
                new[] { Loc.GetString("Tune.Option.Leave"), Loc.GetString("Dialog.Install") }, preset.ReShade ? 1 : 0, i => { preset.ReShade = i == 1; Save(); return true; }),
            cog.CompactCombo(Loc.GetString("Tune.Field.HdrMod"), Loc.GetString("Tune.Help.HdrMod"),
                new[] { Loc.GetString("Option.None"), "RenoDX", "Luma", Loc.GetString("Tune.Hdr.RenoDxElseLuma") }, Math.Max(0, Array.IndexOf(hdrValues, preset.HdrMod ?? "")),
                i => { preset.HdrMod = hdrValues[i]; Save(); return true; }),
            cog.CompactCombo(Loc.GetString("Tune.Field.FrameLimiter"), Loc.GetString("Tune.Help.FrameLimiter"),
                new[] { Loc.GetString("Option.None"), "ReLimiter", "Display Commander" }, Math.Max(0, Array.IndexOf(limValues, preset.FrameLimiter ?? "")),
                i => { preset.FrameLimiter = limValues[i]; Save(); return true; }),
            null);

        // ── OptiScaler ──
        cog.AddSection("OptiScaler");
        var osNames = new List<string> { "" };
        osNames.AddRange(OptiScalerService.SupportedDllNames);
        if (!string.IsNullOrEmpty(preset.OsDllName) && !osNames.Contains(preset.OsDllName, StringComparer.OrdinalIgnoreCase)) osNames.Add(preset.OsDllName);
        cog.AddCompactGrid(
            cog.CompactCombo("OptiScaler", Loc.GetString("Tune.Help.OptiScaler"),
                new[] { Loc.GetString("Tune.Option.Leave"), Loc.GetString("Dialog.Install") }, preset.OptiScaler ? 1 : 0, i => { preset.OptiScaler = i == 1; Save(); return true; }),
            cog.CompactCombo(Loc.GetString("Dialog.Dc.DllName.Label"), Loc.GetString("Tune.Help.OsDllName"),
                osNames.Select(n => n.Length == 0 ? Loc.GetString("Option.Default") : n).ToList(),
                Math.Max(0, osNames.FindIndex(n => n.Equals(preset.OsDllName ?? "", StringComparison.OrdinalIgnoreCase))),
                i => { preset.OsDllName = osNames[i]; Save(); return true; }));

        // ── Other Extras ──
        cog.AddSection(Loc.GetString("Tune.Editor.SectionMoreExtras"));
        var extraCells = new List<CogDialog.CompactCell?>();
        foreach (var extraId in TuneComponentIds.ExtraOrder)
        {
            var xid = extraId;
            string label = DetailPanelBuilder.TuneExtraLabel(xid);
            string optLabel = DetailPanelBuilder.TuneExtraOptionLabel(xid);
            preset.Extras.TryGetValue(xid, out var curOpt);
            bool on = preset.Extras.ContainsKey(xid);
            var options = new List<string> { "" };
            if (optLabel.Length > 0)
            {
                options.AddRange(_window.DetailPanelBuilderInstance.TuneExtraOptions(xid));
                if (!string.IsNullOrEmpty(curOpt) && !options.Contains(curOpt, StringComparer.OrdinalIgnoreCase)) options.Add(curOpt);
            }
            var optCombo = CogDialog.MakeCombo(options.Select(o => o.Length == 0 ? Loc.GetString("Option.Default") : o).ToList(),
                Math.Max(0, options.FindIndex(o => o.Equals(curOpt ?? "", StringComparison.OrdinalIgnoreCase))), isEnabled: on && optLabel.Length > 0);
            optCombo.SelectionChanged += (s, e) =>
            {
                if (optCombo.SelectedIndex < 0 || !preset.Extras.ContainsKey(xid)) return;
                preset.Extras[xid] = options[optCombo.SelectedIndex];
                Save();
            };
            extraCells.Add(cog.CompactCombo(label, Loc.GetString("Tune.Help.ExtraInstall", label),
                new[] { Loc.GetString("Tune.Option.Leave"), Loc.GetString("Dialog.Install") }, on ? 1 : 0, i =>
                {
                    if (i == 1) preset.Extras[xid] = options[Math.Max(0, optCombo.SelectedIndex)];
                    else preset.Extras.Remove(xid);
                    optCombo.IsEnabled = i == 1 && optLabel.Length > 0;
                    Save();
                    return true;
                }));
            var optLabelDisplay = optLabel switch
            {
                "DLL name" => Loc.GetString("Dialog.Dc.DllName.Label"),
                "Variant" => Loc.GetString("Xaml.Variant"),
                "Version" => Loc.GetString("Dialog.Version"),
                _ => optLabel,
            };
            extraCells.Add(optLabel.Length > 0
                ? new CogDialog.CompactCell(optLabelDisplay, optLabel == "DLL name"
                    ? Loc.GetString("Tune.Help.ExtraDllName")
                    : optLabel == "Variant" ? Loc.GetString("Tune.Help.ExtraVariant")
                    : Loc.GetString("Tune.Help.ExtraVersion"), optCombo)
                : null);
        }
        cog.AddCompactGrid(extraCells.ToArray());

        // ── Neural Rendering ──
        cog.AddSection(Loc.GetString("Dialog.NeuralRendering"));
        var nrMethods = new List<string> { TuneNr.None, TuneNr.Auto };
        nrMethods.AddRange(DetailPanelBuilder.TuneNrMethods);
        var nrLabels = nrMethods.Select(m => m == TuneNr.None ? Loc.GetString("Tune.Option.Leave") : m == TuneNr.Auto ? Loc.GetString("Tune.Nr.AutoRHIPick") : DetailPanelBuilder.NrMethodLabel(m)).ToList();
        var nrDlls = new List<string> { "" };
        nrDlls.AddRange(dlss.DlssnrVersions);
        if (!string.IsNullOrEmpty(preset.NrDllVersion) && !nrDlls.Contains(preset.NrDllVersion, StringComparer.OrdinalIgnoreCase)) nrDlls.Add(preset.NrDllVersion);

        // Add-on version list follows the method (Auto / Leave = Latest only)
        var addonValues = new List<string>();
        var addonCombo = CogDialog.MakeCombo(new List<string>(), 0);
        bool addonFilling = false;
        void FillAddonVersions()
        {
            addonFilling = true;
            addonValues.Clear();
            addonValues.Add("");
            string m = preset.NrMethod ?? "";
            bool fixedMethod = m.Length > 0 && m != TuneNr.Auto;
            if (fixedMethod) addonValues.AddRange(DetailPanelBuilder.NrAddonVersionsFor(m));
            if (!string.IsNullOrEmpty(preset.NrAddonVersion) && !addonValues.Contains(preset.NrAddonVersion, StringComparer.OrdinalIgnoreCase))
                addonValues.Add(preset.NrAddonVersion);
            addonCombo.ItemsSource = addonValues.Select(v => v.Length == 0 ? Loc.GetString("NeuralRendering.Latest") : v).ToList();
            addonCombo.SelectedIndex = Math.Max(0, addonValues.FindIndex(v => v.Equals(preset.NrAddonVersion ?? "", StringComparison.OrdinalIgnoreCase)));
            addonCombo.IsEnabled = fixedMethod;
            addonFilling = false;
        }
        addonCombo.SelectionChanged += (s, e) =>
        {
            if (addonFilling || addonCombo.SelectedIndex < 0 || addonCombo.SelectedIndex >= addonValues.Count) return;
            preset.NrAddonVersion = addonValues[addonCombo.SelectedIndex];
            Save();
        };
        FillAddonVersions();
        string addonHelp = Loc.GetString("Tune.Nr.AddonVersionHelp");
        ToolTipService.SetToolTip(addonCombo, addonHelp);

        cog.AddCompactGrid(
            cog.CompactCombo(Loc.GetString("Tune.Field.Method"), Loc.GetString("Tune.Help.Method"),
                nrLabels, Math.Max(0, nrMethods.FindIndex(m => m.Equals(preset.NrMethod ?? "", StringComparison.OrdinalIgnoreCase))),
                i =>
                {
                    if (preset.NrMethod != nrMethods[i]) preset.NrAddonVersion = ""; // versions belong to one method
                    preset.NrMethod = nrMethods[i];
                    Save();
                    FillAddonVersions();
                    return true;
                }),
            new CogDialog.CompactCell(Loc.GetString("Tune.Field.AddonVersion"), addonHelp, addonCombo),
            cog.CompactCombo(Loc.GetString("Tune.Field.NrDllVersion"), Loc.GetString("Tune.Help.NrDllVersion"),
                nrDlls.Select(v => v.Length == 0 ? Loc.GetString("NeuralRendering.Latest") : v).ToList(),
                Math.Max(0, nrDlls.FindIndex(v => v.Equals(preset.NrDllVersion ?? "", StringComparison.OrdinalIgnoreCase))),
                i => { preset.NrDllVersion = nrDlls[i]; Save(); return true; }),
            null);

        // ── DLSS / Streamline files ──
        cog.AddSection(Loc.GetString("Tune.Editor.SectionDlssFiles"));
        CogDialog.CompactCell DllCombo(string label, string help, IReadOnlyList<string> versions, Func<string> get, Action<string> set)
        {
            var values = new List<string> { TuneDll.Leave, TuneDll.Newest };
            values.AddRange(versions);
            var cur = get() ?? "";
            if (cur.Length > 0 && !values.Contains(cur, StringComparer.OrdinalIgnoreCase)) values.Add(cur); // a version no longer listed
            var labels = values.Select(v => v == TuneDll.Leave ? Loc.GetString("Tune.Option.Leave") : v == TuneDll.Newest ? Loc.GetString("Tune.Option.Newest") : v).ToList();
            int idx = Math.Max(0, values.FindIndex(v => v.Equals(cur, StringComparison.OrdinalIgnoreCase)));
            return cog.CompactCombo(label, help, labels, idx, i => { set(values[i]); Save(); return true; });
        }
        cog.AddCompactGrid(
            DllCombo("Super Resolution", Loc.GetString("Tune.Help.DlssSr"), dlss.DlssVersions, () => preset.DlssSr, v => preset.DlssSr = v),
            DllCombo("Ray Reconstruction", Loc.GetString("Tune.Help.DlssRr"), dlss.DlssdVersions, () => preset.DlssRr, v => preset.DlssRr = v),
            DllCombo("Frame Generation", Loc.GetString("Tune.Help.DlssFg"), dlss.DlssgVersions, () => preset.DlssFg, v => preset.DlssFg = v),
            DllCombo("Streamline", Loc.GetString("Tune.Help.Streamline"), dlss.StreamlineVersions, () => preset.Streamline, v => preset.Streamline = v));
        cog.AddNote(Loc.GetString("Tune.Editor.NoteFiles"));

        // ── NVIDIA settings ──
        foreach (var group in new[] { TuneItems.GroupDlssPresets, TuneItems.GroupDlssScale, TuneItems.GroupDriver })
        {
            cog.AddSection(group);
            var cells = new List<CogDialog.CompactCell?>();
            foreach (var item in TuneItems.All.Where(i => i.Group == group))
            {
                var key = item.Key;
                var choices = item.Choices?.Invoke() ?? Array.Empty<(string, string)>();
                var values = new List<string?> { null };
                var labels = new List<string> { Loc.GetString("Tune.Option.Leave") };
                foreach (var (l, v) in choices) { labels.Add(l); values.Add(v); }
                preset.Nvidia.TryGetValue(key, out var cur);
                if (cur != null && !values.Contains(cur)) { labels.Add(item.Format(cur)); values.Add(cur); }
                int idx = Math.Max(0, values.IndexOf(cur));
                cells.Add(cog.CompactCombo(item.Label, item.Help, labels, idx, i =>
                {
                    if (values[i] == null) preset.Nvidia.Remove(key);
                    else preset.Nvidia[key] = values[i]!;
                    Save();
                    return true;
                }));
            }
            cog.AddCompactGrid(cells.ToArray());
            if (group == TuneItems.GroupDriver)
                cog.AddNote(Loc.GetString("Tune.Editor.NoteDriver"));
        }

        await cog.ShowAsync();
        RefreshTunePresetsCard();
        _window.RefreshTuneButtons();
    }
}
