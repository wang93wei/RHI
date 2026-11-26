// SettingsHandler.RtxVideo.cs -- Settings › Display › "RTX Video" card (built in C#, XAML constraint): RTX Video
// Super Resolution and RTX Video HDR, the NVIDIA App's System › Video settings for video in browsers and players.
// Inserted after the DLDSR card the first time Settings opens; re-read from the driver each time Settings opens.
// Driver calls: Services/RtxVideoService.cs (background thread). Changes apply at once (sliders 400 ms after the
// last move).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private StackPanel? _rvBody;
    private bool _rvCardFailed;
    private int _rvLoadId;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _rvSliderTimer;
    private Func<Task>? _rvPendingSlider;

    private static string[] RvSrModes() => new[]
    {
        Loc.GetString("Option.Off"),
        Loc.GetString("Settings.RtxVideo.Mode.Auto"),
        Loc.GetString("Settings.RtxVideo.Mode.VeryLow"),
        Loc.GetString("Settings.RtxVideo.Mode.Low"),
        Loc.GetString("Settings.RtxVideo.Mode.Medium"),
        Loc.GetString("Settings.RtxVideo.Mode.High"),
    };

    private static string[] RvPriorities() => new[]
    {
        Loc.GetString("Settings.RtxVideo.Priority.Low"),
        Loc.GetString("Settings.RtxVideo.Priority.Medium"),
        Loc.GetString("Settings.RtxVideo.Priority.High"),
    };

    private void EnsureRtxVideoCard()
    {
        if (_rvBody != null) { _ = LoadRtxVideoCardAsync(); return; }
        if (_rvCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Display") { section = sp; break; }
            if (section == null)
            {
                _rvCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureRtxVideoCard] Display section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var outer = new StackPanel { Spacing = 10 };
            card.Child = outer;
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = Loc.GetString("Settings.RtxVideo.Title"), Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.RtxVideo.Intro"),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            outer.Children.Add(head);
            _rvBody = new StackPanel { Spacing = 10 };
            outer.Children.Add(_rvBody);

            int idx = section.Children.IndexOf(_window.DldsrControlCard);
            if (idx >= 0) section.Children.Insert(idx + 1, card);
            else section.Children.Add(card);
            _ = LoadRtxVideoCardAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            _rvCardFailed = true;
            _rvBody = null;
            CrashReporter.Log($"[SettingsHandler.EnsureRtxVideoCard] Failed — {ex.Message}");
        }
    }

    private async Task LoadRtxVideoCardAsync()
    {
        if (_rvBody == null) return;
        int id = ++_rvLoadId;
        if (_rvBody.Children.Count == 0) SetRtxVideoMessage(Loc.GetString("Settings.RtxVideo.Reading"));
        RtxVideoState? state = null;
        bool hasApp = false;
        try
        {
            (state, hasApp) = await Task.Run(() => (RtxVideoService.Read(), RtxVideoService.FindNvCplDll() != null));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.SEHException)
        {
            CrashReporter.Log($"[SettingsHandler.LoadRtxVideoCardAsync] Read failed — {ex.Message}");
        }
        if (id != _rvLoadId || _rvBody == null) return;
        if (state == null || (!state.SrSupported && !state.HdrSupported))
        {
            SetRtxVideoMessage(hasApp
                ? Loc.GetString("Settings.RtxVideo.NotAvailable")
                : Loc.GetString("Settings.RtxVideo.NeedsNvidiaApp"));
            return;
        }
        BuildRtxVideoBody(state);
    }

    private void SetRtxVideoMessage(string text)
    {
        if (_rvBody == null) return;
        _rvBody.Children.Clear();
        _rvBody.Children.Add(new TextBlock { Text = text, Style = FindStyle("SettingsNoteStyle"), TextWrapping = TextWrapping.Wrap });
    }

    private void BuildRtxVideoBody(RtxVideoState s)
    {
        if (_rvBody == null) return;
        _rvBody.Children.Clear();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel { Spacing = 9, Margin = new Thickness(0, 0, 20, 0) };
        var divider = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush) };
        var right = new StackPanel { Spacing = 9, Margin = new Thickness(20, 0, 0, 0) };
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(divider);
        grid.Children.Add(right);
        _rvBody.Children.Add(grid);

        // ── Super Resolution ──
        left.Children.Add(new TextBlock { Text = Loc.GetString("Settings.RtxVideo.SuperResolution.Heading"), Style = FindStyle("SettingsHeadStyle") });
        if (!s.SrSupported)
            left.Children.Add(new TextBlock { Text = Loc.GetString("Settings.RtxVideo.SrNotSupported"), Style = FindStyle("SettingsNoteStyle"), TextWrapping = TextWrapping.Wrap });
        else
        {
            // Combo order: Off, Auto, Very low .. High  ↔  driver 0, 5, 1 .. 4
            int srIndex = s.SrMode switch { 5 => 1, >= 1 and <= 4 => s.SrMode + 1, _ => 0 };
            var srCombo = RvCombo(RvSrModes(), srIndex, Loc.GetString("Settings.RtxVideo.SuperResolution.Label"),
                Loc.GetString("Settings.RtxVideo.SuperResolution.Tooltip"));
            var prioCombo = RvCombo(RvPriorities(), s.SrGpuPriority, Loc.GetString("Settings.RtxVideo.GpuPriority.Label"),
                Loc.GetString("Settings.RtxVideo.GpuPriority.Tooltip"));
            prioCombo.IsEnabled = srIndex == 1;
            srCombo.SelectionChanged += (_, _) =>
            {
                int i = srCombo.SelectedIndex;
                if (i < 0) return;
                int mode = i switch { 0 => 0, 1 => 5, _ => i - 1 };
                prioCombo.IsEnabled = i == 1;
                _ = RtxVideoWriteAsync(() => RtxVideoService.SetSuperResolution(mode), reload: true);
            };
            prioCombo.SelectionChanged += (_, _) =>
            {
                int i = prioCombo.SelectedIndex;
                if (i >= 0) _ = RtxVideoWriteAsync(() => RtxVideoService.SetGpuPriority(i), reload: false);
            };
            left.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.SuperResolution.Label"), srCombo));
            left.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.GpuPriority.Label"), prioCombo));
            left.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.StatusIndicator.Label"), RvIndicatorCombo(s.SrIndicator, RtxVideoService.FlagSrIndicator,
                Loc.GetString("Settings.RtxVideo.SrIndicator.Tooltip"))));
            left.Children.Add(new TextBlock
            {
                Text = s.SrStatus is >= 1 and <= 4 ? Loc.GetString("Settings.RtxVideo.SrStatusActive", s.SrStatus) : Loc.GetString("Settings.RtxVideo.SrStatusInactive"),
                Style = FindStyle("SettingsHelpStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        // ── HDR ──
        right.Children.Add(new TextBlock { Text = Loc.GetString("Settings.RtxVideo.Hdr.Heading"), Style = FindStyle("SettingsHeadStyle") });
        if (!s.HdrSupported)
        {
            right.Children.Add(new TextBlock { Text = Loc.GetString("Settings.RtxVideo.HdrNotSupported"), Style = FindStyle("SettingsNoteStyle"), TextWrapping = TextWrapping.Wrap });
            return;
        }
        var hdrCombo = RvCombo(new[] { Loc.GetString("Option.Off"), Loc.GetString("Option.On") }, s.HdrOn ? 1 : 0, Loc.GetString("Settings.RtxVideo.VideoHdr.Label"),
            Loc.GetString("Settings.RtxVideo.VideoHdr.Tooltip"));
        right.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.Hdr.Heading"), hdrCombo));

        int peakMin = s.HdrPeakMin > 0 ? s.HdrPeakMin : 400;
        int peakMax = s.HdrPeakMax >= peakMin ? s.HdrPeakMax : Math.Max(peakMin, 2000);
        var peak = RvSlider(peakMin, peakMax, Math.Clamp(s.HdrPeak, peakMin, peakMax), "",
            Loc.GetString("Settings.RtxVideo.Peak.Tooltip"));
        var mid = RvSlider(10, 100, Math.Clamp(s.HdrMiddleGrey, 10, 100), "",
            Loc.GetString("Settings.RtxVideo.MiddleGrey.Tooltip"));
        var contrast = RvSlider(-100, 100, Math.Clamp(s.HdrContrast, -100, 100), "%", Loc.GetString("Settings.RtxVideo.Contrast.Tooltip"));
        var saturation = RvSlider(-100, 100, Math.Clamp(s.HdrSaturation, -100, 100), "%", Loc.GetString("Settings.RtxVideo.Saturation.Tooltip"));
        right.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.Peak.Label"), peak.Row));
        right.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.MiddleGrey.Label"), mid.Row));
        right.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.Contrast.Label"), contrast.Row));
        right.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.Saturation.Label"), saturation.Row));
        right.Children.Add(SettingRow(Loc.GetString("Settings.RtxVideo.StatusIndicator.Label"), RvIndicatorCombo(s.HdrIndicator, RtxVideoService.FlagHdrIndicator,
            Loc.GetString("Settings.RtxVideo.HdrIndicator.Tooltip"))));
        right.Children.Add(new TextBlock
        {
            Text = Loc.GetString("Settings.RtxVideo.HdrHelpNote"),
            Style = FindStyle("SettingsHelpStyle"),
            TextWrapping = TextWrapping.Wrap,
        });

        void SlidersChanged()
        {
            int p = (int)peak.Slider.Value, m = (int)mid.Slider.Value, c = (int)contrast.Slider.Value, sa = (int)saturation.Slider.Value;
            QueueRtxVideoSliderWrite(() => RtxVideoWriteAsync(() => RtxVideoService.SetHdrValues(p, m, c, sa), reload: false));
        }
        foreach (var sl in new[] { peak.Slider, mid.Slider, contrast.Slider, saturation.Slider })
            sl.ValueChanged += (_, _) => SlidersChanged();
        hdrCombo.SelectionChanged += (_, _) =>
        {
            int i = hdrCombo.SelectedIndex;
            if (i >= 0) _ = RtxVideoWriteAsync(() => RtxVideoService.SetHdrEnabled(i == 1), reload: false);
        };
    }

    private ComboBox RvCombo(string[] items, int selected, string name, string tooltip)
    {
        var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
        foreach (var it in items) combo.Items.Add(new ComboBoxItem { Content = it });
        combo.SelectedIndex = Math.Clamp(selected, 0, items.Length - 1); // before any handler is attached
        ToolTipService.SetToolTip(combo, tooltip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, name);
        return combo;
    }

    private ComboBox RvIndicatorCombo(bool on, int flag, string tooltip)
    {
        var combo = RvCombo(new[] { Loc.GetString("Option.Off"), Loc.GetString("Option.On") }, on ? 1 : 0, Loc.GetString("Settings.RtxVideo.StatusIndicator.Label"), tooltip);
        combo.SelectionChanged += (_, _) =>
        {
            int i = combo.SelectedIndex;
            if (i >= 0) _ = RtxVideoWriteAsync(() => RtxVideoService.SetIndicator(flag, i == 1), reload: false);
        };
        return combo;
    }

    private (Grid Row, Slider Slider) RvSlider(int min, int max, int value, string suffix, string tooltip)
    {
        var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = 1, Value = value, VerticalAlignment = VerticalAlignment.Center };
        var text = new TextBlock
        {
            Text = $"{value}{suffix}",
            Style = FindStyle("SettingsLabelStyle"),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 44,
            TextAlignment = TextAlignment.Right,
        };
        slider.ValueChanged += (_, e) => text.Text = $"{(int)e.NewValue}{suffix}";
        ToolTipService.SetToolTip(slider, tooltip);
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(slider);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return (row, slider);
    }

    /// <summary>Slider writes wait until the slider has been still for 400 ms.</summary>
    private void QueueRtxVideoSliderWrite(Func<Task> write)
    {
        _rvPendingSlider = write;
        if (_rvSliderTimer == null)
        {
            _rvSliderTimer = _window.DispatcherQueue.CreateTimer();
            _rvSliderTimer.Interval = TimeSpan.FromMilliseconds(400);
            _rvSliderTimer.IsRepeating = false;
            _rvSliderTimer.Tick += (_, _) =>
            {
                var w = _rvPendingSlider;
                _rvPendingSlider = null;
                if (w != null) _ = w();
            };
        }
        _rvSliderTimer.Stop();
        _rvSliderTimer.Start();
    }

    private async Task RtxVideoWriteAsync(Func<bool> write, bool reload)
    {
        bool ok;
        try { ok = await Task.Run(write); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.SEHException)
        {
            CrashReporter.Log($"[SettingsHandler.RtxVideoWriteAsync] Failed — {ex.Message}");
            ok = false;
        }
        if (!ok)
        {
            _window.ShowStatusBarMessage(Loc.GetString("Settings.RtxVideo.WriteFailed"), TimeSpan.FromSeconds(8));
            reload = true; // show what the driver really has
        }
        if (reload) await LoadRtxVideoCardAsync();
    }
}
