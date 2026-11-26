// SettingsHandler.Monitors.cs -- Settings › Display › "Monitors" card (built in C#, XAML constraint): desktop
// resolution, refresh rate and Windows HDR for each connected monitor. Inserted after the DLDSR card (so above
// RTX Video) the first time Settings opens; re-read each time Settings opens. A resolution or refresh rate change
// shows "Keep these display settings?" and goes back after 15 seconds unless kept, like Windows does.
// Windows calls: Services/HdrToggleService.Monitors.cs and Services/ResolutionToggleService.Monitors.cs.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using DisplayResolution = RenoDXCommander.Services.ResolutionToggleService.DisplayResolution;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private const int MonRevertSeconds = 15;

    private StackPanel? _monBody;
    private bool _monCardFailed;
    private int _monLoadId;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _monRevertTimer;
    private TextBlock? _monCountdownText;
    private MonPendingChange? _monPending;

    private sealed record MonitorView(HdrToggleService.MonitorTarget Target, bool Primary, DisplayResolution? Current, List<DisplayResolution> Modes);

    private sealed class MonPendingChange
    {
        public required string Device { get; init; }
        public required string Name { get; init; }
        public required DisplayResolution RevertTo { get; init; }
        public int SecondsLeft { get; set; } = MonRevertSeconds;
    }

    private void EnsureMonitorsCard()
    {
        if (_monBody != null) { if (_monPending == null) _ = LoadMonitorsCardAsync(); return; }
        if (_monCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Display") { section = sp; break; }
            if (section == null)
            {
                _monCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureMonitorsCard] Display section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var outer = new StackPanel { Spacing = 10 };
            card.Child = outer;
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = Loc.GetString("Settings.Monitors.Title"), Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Settings.Monitors.Intro", MonRevertSeconds),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            outer.Children.Add(head);
            _monBody = new StackPanel { Spacing = 14 };
            outer.Children.Add(_monBody);

            int idx = section.Children.IndexOf(_window.DldsrControlCard);
            if (idx >= 0) section.Children.Insert(idx + 1, card);
            else section.Children.Add(card);
            _ = LoadMonitorsCardAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            _monCardFailed = true;
            _monBody = null;
            CrashReporter.Log($"[SettingsHandler.EnsureMonitorsCard] Failed — {ex.Message}");
        }
    }

    private async Task LoadMonitorsCardAsync()
    {
        if (_monBody == null) return;
        int id = ++_monLoadId;
        if (_monBody.Children.Count == 0) SetMonitorsMessage(Loc.GetString("Settings.Monitors.Reading"));
        List<MonitorView> views;
        try
        {
            views = await Task.Run(() => HdrToggleService.GetMonitors().Select(t =>
            {
                var (current, primary) = ResolutionToggleService.GetCurrentMode(t.GdiName);
                return new MonitorView(t, primary, current, ResolutionToggleService.GetSupportedResolutions(t.GdiName));
            }).ToList());
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            CrashReporter.Log($"[SettingsHandler.LoadMonitorsCardAsync] Failed — {ex.Message}");
            views = new();
        }
        if (id != _monLoadId || _monBody == null) return;
        if (views.Count == 0) { SetMonitorsMessage(Loc.GetString("Settings.Monitors.ReadFailed")); return; }
        BuildMonitorsBody(views);
    }

    private void SetMonitorsMessage(string text)
    {
        if (_monBody == null) return;
        _monBody.Children.Clear();
        _monBody.Children.Add(new TextBlock { Text = text, Style = FindStyle("SettingsNoteStyle"), TextWrapping = TextWrapping.Wrap });
    }

    private void BuildMonitorsBody(List<MonitorView> views)
    {
        if (_monBody == null) return;
        _monBody.Children.Clear();
        _monCountdownText = null;
        if (_monPending is { } pending) _monBody.Children.Add(MakeMonitorKeepBanner(pending));
        foreach (var v in views.OrderByDescending(v => v.Primary))
            _monBody.Children.Add(MakeMonitorBlock(v, enabled: _monPending == null));
    }

    private UIElement MakeMonitorBlock(MonitorView v, bool enabled)
    {
        var block = new StackPanel { Spacing = 6 };
        block.Children.Add(new TextBlock
        {
            Text = v.Primary ? Loc.GetString("Settings.Monitors.NameWithPrimary", v.Target.Name) : v.Target.Name,
            Style = FindStyle("SettingsHeadStyle"),
        });

        var grid = new Grid { ColumnSpacing = 12 };
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        block.Children.Add(grid);

        // ── Resolution + refresh rate ──
        var sizes = v.Modes.Select(m => (m.Width, m.Height)).Distinct().ToList();
        var resCombo = new ComboBox { Style = FindStyle("SettingsComboStyle"), HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = enabled && sizes.Count > 0 };
        foreach (var (w, h) in sizes) resCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Settings.Monitors.ResolutionFormat", w, h) });
        int curSize = v.Current is { } c0 ? sizes.IndexOf((c0.Width, c0.Height)) : -1;
        if (curSize >= 0) resCombo.SelectedIndex = curSize;
        ToolTipService.SetToolTip(resCombo, Loc.GetString("Settings.Monitors.Resolution.Tooltip"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(resCombo, Loc.GetString("Settings.Monitors.Resolution.Automation", v.Target.Name));

        var rateCombo = new ComboBox { Style = FindStyle("SettingsComboStyle"), HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = enabled };
        ToolTipService.SetToolTip(rateCombo, Loc.GetString("Settings.Monitors.RefreshRate.Tooltip"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(rateCombo, Loc.GetString("Settings.Monitors.RefreshRate.Automation", v.Target.Name));
        List<uint> rates = new();
        bool filling = false;
        void FillRates(uint want)
        {
            filling = true;
            rateCombo.Items.Clear();
            rates = resCombo.SelectedIndex < 0 ? new()
                : v.Modes.Where(m => (m.Width, m.Height) == sizes[resCombo.SelectedIndex]).Select(m => m.RefreshRate).Distinct().OrderByDescending(r => r).ToList();
            foreach (var r in rates) rateCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Settings.Monitors.HzFormat", r) });
            int at = rates.IndexOf(want);
            rateCombo.SelectedIndex = at >= 0 ? at : (rates.Count > 0 ? 0 : -1);
            filling = false;
        }
        FillRates(v.Current?.RefreshRate ?? 0);

        resCombo.SelectionChanged += (_, _) =>
        {
            if (resCombo.SelectedIndex < 0) return;
            FillRates(v.Current?.RefreshRate ?? 0); // same rate if this size has it, else the highest
            ApplySelectedMode();
        };
        rateCombo.SelectionChanged += (_, _) => { if (!filling) ApplySelectedMode(); };
        void ApplySelectedMode()
        {
            if (resCombo.SelectedIndex < 0 || rateCombo.SelectedIndex < 0) return;
            var (w, h) = sizes[resCombo.SelectedIndex];
            _ = ApplyMonitorModeAsync(v, new DisplayResolution(w, h, rates[rateCombo.SelectedIndex]));
        }

        // ── HDR ──
        var hdrCombo = new ComboBox { Style = FindStyle("SettingsComboStyle"), HorizontalAlignment = HorizontalAlignment.Stretch };
        hdrCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.Off") });
        hdrCombo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.On") });
        hdrCombo.SelectedIndex = v.Target.HdrOn ? 1 : 0;
        hdrCombo.IsEnabled = enabled && v.Target.HdrSupported;
        ToolTipService.SetToolTip(hdrCombo, v.Target.HdrSupported
            ? Loc.GetString("Settings.Monitors.Hdr.Tooltip")
            : Loc.GetString("Settings.Monitors.Hdr.Unsupported.Tooltip"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(hdrCombo, Loc.GetString("Settings.Monitors.Hdr.Automation", v.Target.Name));
        hdrCombo.SelectionChanged += (_, _) =>
        {
            if (hdrCombo.SelectedIndex >= 0) _ = SetMonitorHdrAsync(v, hdrCombo.SelectedIndex == 1);
        };

        grid.Children.Add(MonitorCell(Loc.GetString("Settings.Monitors.Resolution.Label"), resCombo, 0));
        grid.Children.Add(MonitorCell(Loc.GetString("Settings.Monitors.RefreshRate.Label"), rateCombo, 1));
        grid.Children.Add(MonitorCell(
            v.Target.HdrSupported ? Loc.GetString("Settings.Monitors.HdrLabel") : Loc.GetString("Settings.Monitors.HdrLabelUnsupported"),
            hdrCombo, 2));

        string now = v.Current is { } c
            ? (v.Target.HdrOn
                ? Loc.GetString("Settings.Monitors.NowFormatOn", c.Width, c.Height, FormatMonitorHz(v.Target.RefreshHz, c.RefreshRate))
                : Loc.GetString("Settings.Monitors.NowFormatOff", c.Width, c.Height, FormatMonitorHz(v.Target.RefreshHz, c.RefreshRate)))
            : Loc.GetString("Settings.Monitors.NoCurrentMode");
        block.Children.Add(new TextBlock { Text = now, Style = FindStyle("SettingsHelpStyle"), TextWrapping = TextWrapping.Wrap });
        return block;
    }

    private StackPanel MonitorCell(string label, ComboBox combo, int column)
    {
        var cell = new StackPanel { Spacing = 4 };
        cell.Children.Add(new TextBlock { Text = label, Style = FindStyle("SettingsLabelStyle") });
        cell.Children.Add(combo);
        Grid.SetColumn(cell, column);
        return cell;
    }

    private static string FormatMonitorHz(double exact, uint fallback)
    {
        if (exact < 1) return fallback.ToString();
        return Math.Abs(exact - Math.Round(exact)) < 0.005 ? Math.Round(exact).ToString("0") : exact.ToString("0.00");
    }

    private Border MakeMonitorKeepBanner(MonPendingChange p)
    {
        var banner = new Border
        {
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
        };
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _monCountdownText = new TextBlock
        {
            Text = MonCountdownLabel(p),
            Style = FindStyle("SettingsLabelStyle"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(_monCountdownText);
        var keep = new Button { Content = Loc.GetString("Settings.Monitors.KeepButton"), Style = FindStyle("SettingsButtonStyle") };
        keep.Click += (_, _) => KeepMonitorChange();
        Grid.SetColumn(keep, 1);
        row.Children.Add(keep);
        var revert = new Button { Content = Loc.GetString("Settings.Monitors.RevertButton"), Style = FindStyle("SettingsButtonStyle") };
        revert.Click += (_, _) => _ = RevertMonitorChangeAsync();
        Grid.SetColumn(revert, 2);
        row.Children.Add(revert);
        banner.Child = row;
        return banner;
    }

    private static string MonCountdownLabel(MonPendingChange p) =>
        Loc.GetString("Settings.Monitors.CountdownFormat", p.Name, p.RevertTo.Width, p.RevertTo.Height, p.RevertTo.RefreshRate, p.SecondsLeft);

    private async Task ApplyMonitorModeAsync(MonitorView v, DisplayResolution target)
    {
        if (v.Current is not { } old || old == target || _monPending != null) return;
        int rc;
        try { rc = await Task.Run(() => ResolutionToggleService.SetResolution(v.Target.GdiName, target)); }
        catch (InvalidOperationException ex) { CrashReporter.Log($"[SettingsHandler.ApplyMonitorModeAsync] {ex.Message}"); rc = -1; }
        if (rc != 0)
        {
            _window.ShowStatusBarMessage(Loc.GetString("Settings.Monitors.ApplyFailed", target.Width, target.Height, target.RefreshRate, v.Target.Name, rc), TimeSpan.FromSeconds(8));
            await LoadMonitorsCardAsync();
            return;
        }
        _monPending = new MonPendingChange { Device = v.Target.GdiName, Name = v.Target.Name, RevertTo = old };
        if (_monRevertTimer == null)
        {
            _monRevertTimer = _window.DispatcherQueue.CreateTimer();
            _monRevertTimer.Interval = TimeSpan.FromSeconds(1);
            _monRevertTimer.IsRepeating = true;
            _monRevertTimer.Tick += (_, _) => MonitorRevertTick();
        }
        _monRevertTimer.Stop();
        _monRevertTimer.Start();
        await LoadMonitorsCardAsync();
    }

    private void MonitorRevertTick()
    {
        if (_monPending is not { } p) { _monRevertTimer?.Stop(); return; }
        p.SecondsLeft--;
        if (p.SecondsLeft <= 0) { _ = RevertMonitorChangeAsync(); return; }
        if (_monCountdownText != null) _monCountdownText.Text = MonCountdownLabel(p);
    }

    private void KeepMonitorChange()
    {
        _monRevertTimer?.Stop();
        if (_monPending is { } p) CrashReporter.Log($"[SettingsHandler.KeepMonitorChange] Kept new mode on '{p.Device}'");
        _monPending = null;
        _ = LoadMonitorsCardAsync();
    }

    private async Task RevertMonitorChangeAsync()
    {
        _monRevertTimer?.Stop();
        if (_monPending is not { } p) return;
        _monPending = null;
        int rc;
        try { rc = await Task.Run(() => ResolutionToggleService.SetResolution(p.Device, p.RevertTo)); }
        catch (InvalidOperationException ex) { CrashReporter.Log($"[SettingsHandler.RevertMonitorChangeAsync] {ex.Message}"); rc = -1; }
        CrashReporter.Log($"[SettingsHandler.RevertMonitorChangeAsync] '{p.Device}' back to {p.RevertTo.Label} → rc={rc}");
        if (rc != 0)
            _window.ShowStatusBarMessage(Loc.GetString("Settings.Monitors.RevertFailed", p.Name, p.RevertTo.Width, p.RevertTo.Height, rc), TimeSpan.FromSeconds(8));
        await LoadMonitorsCardAsync();
    }

    private async Task SetMonitorHdrAsync(MonitorView v, bool on)
    {
        if (on == v.Target.HdrOn) return;
        bool ok;
        try { ok = await Task.Run(() => HdrToggleService.SetHdr(v.Target, on)); }
        catch (InvalidOperationException ex) { CrashReporter.Log($"[SettingsHandler.SetMonitorHdrAsync] {ex.Message}"); ok = false; }
        if (!ok)
            _window.ShowStatusBarMessage(on
                ? Loc.GetString("Settings.Monitors.HdrSetOnFailed", v.Target.Name)
                : Loc.GetString("Settings.Monitors.HdrSetOffFailed", v.Target.Name), TimeSpan.FromSeconds(8));
        await LoadMonitorsCardAsync();
    }
}
