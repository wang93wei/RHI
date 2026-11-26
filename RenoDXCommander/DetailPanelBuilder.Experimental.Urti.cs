// DetailPanelBuilder.Experimental.Urti.cs — Experimental › Ray Tracing row for URTI (Skynizz/URTI): ray-traced
// reflections for DirectX 12 games, as a ReShade add-on. Per game. Install / remove go through MainViewModel.Urti.cs
// (the effects through the shader sync, the "URTI" effect ticked in the game's preset), files and staging through
// UrtiService. URTI doesn't draw while HSF is in the same game (tested Nov 2026, Red Dead Redemption 2): warned both
// ways, and installing asks first (Install anyway / Cancel), like ReLimiter v4's warnings.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    /// <summary>Localizes a key, or returns the key itself when the app's services are not up (e.g. pure-logic tests).</summary>
    private static string LocOrKey(string key, params object[] args) => App.Services is null ? key : Loc.GetString(key, args);

    /// <summary>The URTI / HSF clash, as a tooltip line ("Game:" lines show under the name in Tabs / Spotlight).</summary>
    internal static string UrtiHsfClashLine => Loc.GetString("Extras.Urti.HsfClash.Line");

    /// <summary>The URTI / HSF clash, for dialogs and ⚙ warnings.</summary>
    internal static string UrtiHsfClashText => Loc.GetString("Extras.Urti.HsfClash.Text");

    private void BuildUrtiRow(GameCardViewModel card, StackPanel body)
    {
        var vm          = _window.ViewModel;
        var installPath = card.InstallPath ?? "";
        bool isInstalled = UrtiService.IsInstalledIn(installPath);
        bool isOurs      = isInstalled && UrtiService.IsRhiInstalled(installPath);
        bool hsfHere     = !string.IsNullOrEmpty(installPath) && HsfService.IsInstalledIn(installPath, card.Is32Bit);
        bool pulled      = FeatureFlags.IsExperimentalHidden(FeatureFlags.ExperimentalUrti);
        // Taken out by the manifest: only shown where it's installed, and only to be removed
        var block = pulled
            ? (Loc.GetString("Extras.Urti.NotOffered"), Loc.GetString("Extras.Urti.NotOffered.Tooltip"))
            : isInstalled ? null : vm.UrtiBlock(card);

        string statusText  = !isInstalled ? Loc.GetString("Status.Ready")
                           : !isOurs ? Loc.GetString("Extras.Urti.Status.Manual")
                           : UrtiService.InstalledVersion(installPath) is { } v ? "v" + v : Loc.GetString("Status.Installed");
        string statusColor = !isInstalled ? "#A0AABB" : isOurs ? "#5ECB7D" : "#E8C46A";

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExperimentalContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        var label = new TextBlock
        {
            Text = "URTI",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var tip = Loc.GetString("Extras.Urti.Tooltip", hsfHere ? UrtiHsfClashLine + "\n" : "");
        if (isInstalled && !isOurs) tip += Loc.GetString("Extras.Urti.Manual.Tooltip.Note");
        ToolTipService.SetToolTip(label, tip);
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var statusBlock = new TextBlock
        {
            Text = statusText,
            FontSize = 12,
            Foreground = UIFactory.GetBrush(statusColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            TextDecorations = isOurs ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None,
        };
        if (isOurs)
        {
            ToolTipService.SetToolTip(statusBlock, Loc.GetString("Extras.OpenGithubReleases.Tooltip"));
            statusBlock.PointerPressed += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(UrtiService.ReleasesUrl));
            AttachLinkCursor(statusBlock);
        }
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        var infoBtn = new Button
        {
            Content = Loc.GetString("Dialog.Info"), FontSize = 11, Padding = new Thickness(6, 2, 6, 2), Width = 36, Height = 32,
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
        };
        ToolTipService.SetToolTip(infoBtn, Loc.GetString("Extras.Urti.Info.Tooltip"));
        infoBtn.Click += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(UrtiService.RepoUrl));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        bool btnEnabled = block == null;
        var installBtn = new Button
        {
            Content = !btnEnabled ? "⚠  " + block!.Value.Item1
                    : isOurs ? Loc.GetString("Extras.Urti.Reinstall.Button") : Loc.GetString("Extras.Urti.Install.Button"),
            FontSize = 12, Height = 32, HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(8),
            Background  = isOurs ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground  = isOurs ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isOurs ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            IsEnabled = btnEnabled,
            Opacity = btnEnabled ? 1.0 : 0.35,
        };
        if (!btnEnabled)
        {
            ToolTipService.SetToolTip(installBtn, block!.Value.Item2);
            // Disabled buttons show no tooltip: put the reason on the label too
            ToolTipService.SetToolTip(label, $"{tip}\n\n{block.Value.Item2}");
        }
        else
            ToolTipService.SetToolTip(installBtn,
                Loc.GetString("Extras.Urti.Install.Tooltip", hsfHere ? "\n\n" + UrtiHsfClashText : ""));

        installBtn.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            try
            {
                if (card.IsRunning)
                {
                    ToolTipService.SetToolTip(installBtn, Loc.GetString("Extras.Urti.CloseGameFirst"));
                    return;
                }
                var xamlRoot = installBtn.XamlRoot ?? _window.Content.XamlRoot;
                if (HsfService.IsInstalledIn(installPath, card.Is32Bit) && !await ConfirmUrtiWithHsfAsync(card, xamlRoot)) return;
                installBtn.IsEnabled = false;
                installBtn.Content = Loc.GetString("Status.Downloading");
                var error = await vm.InstallUrtiAsync(card);
                if (error != null)
                {
                    installBtn.Content = Loc.GetString("Extras.InstallFailed.Button");
                    installBtn.IsEnabled = true;
                    ToolTipService.SetToolTip(installBtn, error);
                    await DialogService.ShowSafeAsync(new ContentDialog
                    {
                        Title = Loc.GetString("Extras.Urti.InstallFailed.Title"),
                        Content = new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap },
                        CloseButtonText = Loc.GetString("Dialog.Ok"),
                        XamlRoot = xamlRoot,
                        RequestedTheme = ElementTheme.Dark,
                    });
                    return;
                }
                RequestExperimentalRebuild(card);
                RequestExtrasRebuild(card); // HSF's row warns about URTI
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or HttpRequestException or TaskCanceledException or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[BuildUrtiRow] Install failed — {ex.Message}");
                installBtn.Content = Loc.GetString("Extras.InstallFailed.Button");
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        var cogBtn = CogDialog.MakeCogButton(Loc.GetString("Extras.Urti.Settings.Tooltip"));
        cogBtn.Click += async (_, _) =>
        {
            try { await ShowUrtiSettingsAsync(card); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[BuildUrtiRow] Settings dialog failed — {ex.Message}");
            }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        var removeBtn = new Button
        {
            Width = 36, Height = 32, Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Opacity = isOurs ? 1.0 : 0,
            IsHitTestVisible = isOurs,
        };
        ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.Urti.Remove.Tooltip"));
        removeBtn.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            if (card.IsRunning)
            {
                ToolTipService.SetToolTip(removeBtn, Loc.GetString("Extras.Urti.CloseGameFirst"));
                return;
            }
            try
            {
                removeBtn.IsEnabled = false;
                await vm.UninstallUrtiAsync(card);
                RequestExperimentalRebuild(card);
                RequestExtrasRebuild(card); // HSF's row stops warning about URTI
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                CrashReporter.Log($"[BuildUrtiRow] Remove failed — {ex.Message}");
                removeBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    /// <summary>HSF is in the game: URTI won't draw while it is. Install anyway / Cancel (Cancel is the default). True = go ahead.</summary>
    private async Task<bool> ConfirmUrtiWithHsfAsync(GameCardViewModel card, XamlRoot? xamlRoot)
    {
        if (xamlRoot == null) return true;
        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Extras.Urti.HsfDialog.Title"),
            Content = new TextBlock
            {
                Text = Loc.GetString("Extras.Urti.HsfDialog.Body"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460,
            },
            PrimaryButtonText = Loc.GetString("Extras.Urti.HsfDialog.InstallAnyway"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        bool go = await DialogService.ShowSafeAsync(dialog) == ContentDialogResult.Primary;
        CrashReporter.Log($"[BuildUrtiRow] '{card.GameName}': HSF installed — {(go ? "installing URTI anyway" : "cancelled")}");
        return go;
    }

    /// <summary>
    /// One urti.ini setting in the ⚙. Names / Values = a dropdown, otherwise a text box checked by Check (returns the value
    /// to write, or null with the reason). Defaults are URTI's own (urti_addon.cpp ReadSettings, urti_live.inl
    /// LiveHotReload), shown when the key isn't in the file.
    /// </summary>
    private sealed record UrtiSetting(string Key, string Label, string Help, string UrtiDefault, bool AtStart,
        string[]? Names = null, string[]? Values = null, Func<string, (string? Value, string? Error)>? Check = null);

    private static readonly System.Globalization.CultureInfo UrtiInv = System.Globalization.CultureInfo.InvariantCulture;

    private static (string?, string?) UrtiInt(string text, int min, string what) =>
        int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer, UrtiInv, out var n) && n >= min
            ? (n.ToString(UrtiInv), null) : (null, LocOrKey("Extras.Urti.Error.WholeNumber", what, min));

    private static (string?, string?) UrtiF0(string text) =>
        double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, UrtiInv, out var f) && f >= 0 && f <= 1
            ? (f.ToString("0.###", UrtiInv), null) : (null, LocOrKey("Extras.Urti.Error.ReflectionF0"));

    /// <summary>"x0,y0,x1,y1" fractions of the screen, left / top smaller than right / bottom; empty = no zone (key removed).</summary>
    internal static (string?, string?) UrtiZone(string text)
    {
        var t = text.Trim();
        if (t.Length == 0) return ("", null);
        var parts = t.Split(',');
        var v = new double[4];
        bool ok = parts.Length == 4;
        for (int i = 0; ok && i < 4; i++)
            ok = double.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, UrtiInv, out v[i]) && v[i] >= 0 && v[i] <= 1;
        if (!ok || v[0] >= v[2] || v[1] >= v[3])
            return (null, LocOrKey("Extras.Urti.Error.UiZone"));
        return (string.Join(",", v.Select(x => x.ToString("0.###", UrtiInv))), null);
    }

    private static UrtiSetting[] UrtiSettings => new[]
    {
        new("live", Loc.GetString("Extras.Urti.Setting.StartOn.Label"),
            Loc.GetString("Extras.Urti.Setting.StartOn.Help"), "0", AtStart: true,
            Names: new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Xaml.On") }, Values: new[] { "0", "1" }),
        new("shadow_rays", Loc.GetString("Extras.Urti.Setting.ShadowRays.Label"),
            Loc.GetString("Extras.Urti.Setting.ShadowRays.Help"), "2", AtStart: false,
            Names: new[] { "1", "2", "3", "4" }, Values: new[] { "1", "2", "3", "4" }),
        new("calibration_interval", Loc.GetString("Extras.Urti.Setting.Calibration.Label"),
            Loc.GetString("Extras.Urti.Setting.Calibration.Help"),
            "180", AtStart: true, Check: s => UrtiInt(s, 30, Loc.GetString("Extras.Urti.Field.Calibration"))),
        new("capture_presents", Loc.GetString("Extras.Urti.Setting.CaptureLength.Label"),
            Loc.GetString("Extras.Urti.Setting.CaptureLength.Help"),
            "4", AtStart: true, Check: s => UrtiInt(s, 1, Loc.GetString("Extras.Urti.Field.CaptureLength"))),
        new("reflect_f0", Loc.GetString("Extras.Urti.Setting.ReflectionF0.Label"),
            Loc.GetString("Extras.Urti.Setting.ReflectionF0.Help"),
            "0.04", AtStart: false, Check: UrtiF0),
        new("jitter", Loc.GetString("Extras.Urti.Setting.Jitter.Label"),
            Loc.GetString("Extras.Urti.Setting.Jitter.Help"),
            "2", AtStart: false, Names: new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Extras.Urti.Jitter.OptFrameToFrame"), Loc.GetString("Extras.Urti.Jitter.OptFull") }, Values: new[] { "0", "1", "2" }),
        new("ui_photo", Loc.GetString("Extras.Urti.Setting.UiPhoto.Label"),
            Loc.GetString("Extras.Urti.Setting.UiPhoto.Help"),
            "1", AtStart: false, Names: new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Xaml.On") }, Values: new[] { "0", "1" }),
        new("sun_measure", Loc.GetString("Extras.Urti.Setting.SunMeasure.Label"),
            Loc.GetString("Extras.Urti.Setting.SunMeasure.Help"),
            "1", AtStart: false, Names: new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Xaml.On") }, Values: new[] { "0", "1" }),
        new("ui_zone", Loc.GetString("Extras.Urti.Setting.UiZone.Label"),
            Loc.GetString("Extras.Urti.Setting.UiZone.Help"),
            "", AtStart: false, Check: UrtiZone),
    };

    /// <summary>Index of a dropdown setting's current value, read the way URTI reads it (a number; Off/On: not 0 = On; clamped to the list).</summary>
    internal static int UrtiChoiceIndex(string[] values, string value)
    {
        var t = value.Trim();
        int n = int.TryParse(t, System.Globalization.NumberStyles.Integer, UrtiInv, out var x) ? x
              : double.TryParse(t, System.Globalization.NumberStyles.Float, UrtiInv, out var d) ? (int)d : 0;
        var nums = values.Select(v => int.Parse(v, UrtiInv)).ToArray();
        if (nums.Length == 2 && nums[0] == 0 && nums[1] == 1) return n != 0 ? 1 : 0;
        n = Math.Clamp(n, nums[0], nums[^1]);
        int i = Array.IndexOf(nums, n);
        return i >= 0 ? i : 0;
    }

    private void AddUrtiSettings(CogDialog cog, string installPath, Action reopen)
    {
        var svc = App.Services.GetRequiredService<UrtiService>();
        var settings = UrtiSettings;
        cog.AddSection(Loc.GetString("Common.Settings"));
        cog.AddNote(Loc.GetString("Extras.Urti.Settings.Note"));

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        bool Save(UrtiSetting s, string? value)
        {
            // An empty UI zone removes the key: URTI then protects no zone
            var write = s.Key == "ui_zone" && value == "" ? null : value;
            if (svc.SetSetting(installPath, s.Key, write))
            {
                cog.SetSuccess(s.AtStart
                    ? Loc.GetString("Extras.Urti.Settings.SavedAtStart", s.Label)
                    : Loc.GetString("Extras.Urti.Settings.Saved", s.Label));
                return true;
            }
            cog.SetError(Loc.GetString("Extras.Urti.Error.WriteIni"));
            return false;
        }

        for (int i = 0; i < settings.Length; i++)
        {
            var s = settings[i];
            int row = i / 2, col = (i % 2) * 2;
            // The last setting (UI zone, a longer value) takes the rest of its row
            bool wide = i == settings.Length - 1 && i % 2 == 0;
            if (col == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var help = s.Help + (s.AtStart
                ? " " + Loc.GetString("Extras.Urti.Settings.AppliesAtStart")
                : " " + Loc.GetString("Extras.Urti.Settings.AppliesNow"));
            var current = UrtiService.GetSetting(installPath, s.Key) ?? s.UrtiDefault;

            var label = new TextBlock
            {
                Text = s.Label,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            };
            ToolTipService.SetToolTip(label, help);
            FrameworkElement control;
            if (s.Names != null)
            {
                var combo = CogDialog.MakeCombo(s.Names, UrtiChoiceIndex(s.Values!, current));
                int last = combo.SelectedIndex;
                bool reverting = false;
                combo.SelectionChanged += (_, _) =>
                {
                    if (reverting || combo.SelectedIndex < 0 || combo.SelectedIndex == last) return;
                    if (Save(s, s.Values![combo.SelectedIndex])) { last = combo.SelectedIndex; return; }
                    reverting = true;
                    combo.SelectedIndex = last;
                    reverting = false;
                };
                control = combo;
            }
            else
            {
                var box = CogDialog.MakeTextBox(current, s.Key == "ui_zone" ? Loc.GetString("Extras.Urti.UiZone.Placeholder") : s.UrtiDefault);
                string saved = current;
                // Saved on Enter and when the box loses focus; an invalid value is put back
                void Commit()
                {
                    if (box.Text.Trim() == saved.Trim()) return;
                    var (value, error) = s.Check!(box.Text);
                    if (value == null) { cog.SetError(error ?? Loc.GetString("Extras.Urti.Error.NotValid")); box.Text = saved; return; }
                    if (Save(s, value)) { saved = value; box.Text = value; }
                    else box.Text = saved;
                }
                box.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { Commit(); e.Handled = true; } };
                box.LostFocus += (_, _) => Commit();
                control = box;
            }
            ToolTipService.SetToolTip(control, help);
            Grid.SetRow(label, row); Grid.SetColumn(label, col);
            Grid.SetRow(control, row); Grid.SetColumn(control, col + 1);
            if (wide) Grid.SetColumnSpan(control, 3);
            grid.Children.Add(label);
            grid.Children.Add(control);
        }
        cog.AddContent(grid);

        cog.AddButtons(cog.MakeButton(Loc.GetString("Extras.Urti.UseProfile.Button"), CogButtonKind.Secondary, () =>
        {
            // The values in the urti.ini RHI installs (the author's Red Dead Redemption 2 profile); keys it doesn't set are removed
            bool ok = settings.All(s => svc.SetSetting(installPath, s.Key, UrtiService.GetProfileValue(s.Key)));
            if (!ok) { cog.SetError(Loc.GetString("Extras.Urti.Error.WriteIni")); return; }
            reopen();
        }, Loc.GetString("Extras.Urti.UseProfile.Tooltip")));
    }

    internal async Task ShowUrtiSettingsAsync(GameCardViewModel card)
    {
        var installPath = card.InstallPath ?? "";
        bool isInstalled = UrtiService.IsInstalledIn(installPath);
        bool reopen = false;
        var cog = new CogDialog("URTI", _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Extras.Urti.Intro"));
        cog.AddWarning(Loc.GetString("Extras.Urti.Warning.SinglePlayer"));
        if (!string.IsNullOrEmpty(installPath) && HsfService.IsInstalledIn(installPath, card.Is32Bit)) cog.AddWarning(UrtiHsfClashText);
        cog.AddNote(Loc.GetString("Extras.Urti.Note.Experimental"));
        if (MainViewModel.UrtiReShadeVersionNote(card) is { } rsNote) cog.AddWarning(rsNote);

        cog.AddSection(Loc.GetString("Extras.Urti.Section.UsingIt"));
        cog.AddNote(Loc.GetString("Extras.Urti.Note.UsingIt"));

        if (isInstalled && File.Exists(UrtiService.IniPath(installPath)))
            AddUrtiSettings(cog, installPath, () => { reopen = true; cog.Hide(); });
        else
        {
            cog.AddSection(Loc.GetString("Common.Settings"));
            cog.AddNote(isInstalled ? Loc.GetString("Extras.Urti.Note.NoIni") : Loc.GetString("Extras.Urti.Note.NotInstalled"));
        }

        cog.AddSection(Loc.GetString("Dialog.SectionFiles"));
        cog.AddNote(Loc.GetString("Extras.Urti.Note.Files"));
        var addonDir = UrtiService.AddonDir(installPath);
        cog.AddButtons(
            cog.MakeButton(Loc.GetString("Extras.Urti.OpenGithub.Button"), CogButtonKind.Secondary,
                async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(UrtiService.RepoUrl)); }),
            cog.MakeOpenFileButton(Loc.GetString("Extras.Urti.OpenIni.Button"), isInstalled ? UrtiService.IniPath(installPath) : null),
            cog.MakeOpenFileButton(Loc.GetString("Extras.Urti.OpenLog.Button"), isInstalled ? Path.Combine(addonDir, UrtiService.LogName) : null));
        await cog.ShowAsync();
        // "Use RHI's profile" closes the ⚙ so it opens again with the new values
        if (reopen) await ShowUrtiSettingsAsync(card);
    }
}
