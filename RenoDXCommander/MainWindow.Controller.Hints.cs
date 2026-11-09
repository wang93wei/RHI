// MainWindow.Controller.Hints.cs -- the controller's button hints and its short messages
// ("Launching …"). The hints take the anti-cheat line's place in the middle of the footer while the
// controller is in use (so they never cover the page, whether Spotlight's covers are at the top or
// the bottom), list only the buttons that do something where focus is now, and go back to the
// warning when the mouse moves. The message floats above the footer. Neither takes clicks.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private StackPanel? _padHintRow;
    private string? _padHintKey;
    private Border? _padToast;
    private TextBlock? _padToastText;
    private DispatcherQueueTimer? _padToastTimer;

    /// <summary>Shows (or hides) the hint bar; shown, it's refreshed for where focus is.</summary>
    private void ShowPadHints(bool show)
    {
        try
        {
            if (!show)
            {
                if (_padHints != null) _padHints.Visibility = Visibility.Collapsed;
                if (_footerWarning != null) _footerWarning.Visibility = Visibility.Visible;
                RestorePadPointer();
                return;
            }
            if (!EnsurePadOverlay()) return;
            UpdatePadHints();
            _padHints!.Visibility = Visibility.Visible;
            if (_footerWarning != null) _footerWarning.Visibility = Visibility.Collapsed;
            HidePadPointer();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.ShowPadHints] {ex.Message}");
        }
    }

    /// <summary>Rebuilds the hint bar for the current place (only when it changed).</summary>
    private void UpdatePadHints()
    {
        if (_padHintRow == null || Content?.XamlRoot is not { } xamlRoot) return;
        var hints = CurrentPadHints(xamlRoot).Select(h => (Key: PadButtonName(h.Key), h.Label)).ToList();
        var key = string.Join("|", hints.Select(h => h.Key + h.Label));
        if (key == _padHintKey) return;
        _padHintKey = key;
        _padHintRow.Children.Clear();
        // Compact, to fit the footer's one line without making it taller
        foreach (var (button, label) in hints)
        {
            var pair = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            pair.Children.Add(new Border
            {
                Background = TabsTheme.B("#1E2738"),
                BorderBrush = TabsTheme.B("#33405A"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 0, 5, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = button, FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = TabsTheme.B(TabsTheme.TextPrimary) },
            });
            pair.Children.Add(new TextBlock { Text = label, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = TabsTheme.B(TabsTheme.TextSecondary) });
            _padHintRow.Children.Add(pair);
        }
    }

    /// <summary>The button's name on the pad in use: Xbox names, or PlayStation ones (✕ ○ □ △, L1 / R1…).</summary>
    private string PadButtonName(string xbox)
    {
        if (!_padIsPlayStation) return xbox;
        bool ds4 = _psPad.Model == PlayStationModel.DualShock4;
        return xbox switch
        {
            "A" => "✕",
            "B" => "○",
            "X" => "□",
            "Y" => "△",
            "LB / RB" => "L1 / R1",
            "LT / RT" => "L2 / R2",
            "LS" => "L3",
            "RS" => "R3",
            "View" => ds4 ? "Share" : "Create",
            "Menu" => "Options",
            _ => xbox,
        };
    }

    /// <summary>What each button does right now, in the order they're listed.</summary>
    private List<(string Key, string Label)> CurrentPadHints(XamlRoot xamlRoot)
    {
        var focused = FocusManager.GetFocusedElement(xamlRoot) as DependencyObject;
        var popup = TopPopup(xamlRoot, focused);
        var layout = _detailPanelBuilder.Layout;

        if (popup != null)
        {
            if (focused is ComboBoxItem) return new() { ("A", Loc.GetString("Controller.Hint.Choose")), ("B", Loc.GetString("Controller.Hint.Cancel")) };
            if (focused is MenuFlyoutSubItem) return new() { ("A", Loc.GetString("Controller.Hint.Open")), ("B", _padSubmenus.Count > 0 ? Loc.GetString("Controller.Hint.Back") : Loc.GetString("Controller.Hint.Close")) };
            if (focused is MenuFlyoutItemBase) return new() { ("A", Loc.GetString("Controller.Hint.Choose")), ("B", _padSubmenus.Count > 0 ? Loc.GetString("Controller.Hint.Back") : Loc.GetString("Controller.Hint.Close")) };
            var dialogHints = new List<(string, string)> { ("A", Loc.GetString("Controller.Hint.Select")), ("B", Loc.GetString("Controller.Hint.Close")) };
            if (PopupScroller(popup, focused) != null) dialogHints.Add((Loc.GetString("Controller.Hardware.RightStick"), Loc.GetString("Controller.Hint.Scroll")));
            return dialogHints;
        }

        if (ViewModel.CurrentPage == AppPage.Settings)
            return new() { ("A", Loc.GetString("Controller.Hint.Select")), ("B", Loc.GetString("Controller.Hint.BackToGames")), ("LB / RB", Loc.GetString("Controller.Hint.Section")), (Loc.GetString("Controller.Hardware.RightStick"), Loc.GetString("Controller.Hint.Scroll")) };

        var hints = new List<(string, string)>();
        string? select = focused switch
        {
            TextBox => null,                   // typing needs a keyboard
            ComboBox => Loc.GetString("Controller.Hint.Open"),
            ToggleSwitch => Loc.GetString("Controller.Hint.Switch"),
            _ when focused != null && _shelf != null && IsWithin(focused, _shelf) => Loc.GetString("Controller.Hint.Select"),
            _ => Loc.GetString("Controller.Hint.Select"),
        };
        if (select != null) hints.Add(("A", select));

        if (layout.IsPanelOpen)
        {
            hints.Add(("B", layout.IsCogInPanel ? Loc.GetString("Controller.Hint.Back") : Loc.GetString("Controller.Hint.Close")));
            hints.Add(("LB / RB", Loc.GetString("Controller.Hint.Section")));
            hints.Add(("Y", Loc.GetString("Controller.Hint.Launch")));
            hints.Add(("RS", Loc.GetString("Controller.Hint.Covers")));
            return hints;
        }

        bool onShelf = focused != null && _shelf != null && IsWithin(focused, _shelf);
        if (!onShelf) hints.Add(("B", Loc.GetString("Controller.Hint.Covers")));
        hints.Add(("Y", Loc.GetString("Controller.Hint.Launch")));
        hints.Add(("X", IsCollectionChip(focused, out _) ? Loc.GetString("Controller.Hint.CollectionMenu") : Loc.GetString("Controller.Hint.GameMenu")));
        hints.Add(("LB / RB", Loc.GetString("Controller.Hint.Game")));
        hints.Add(("LT / RT", Loc.GetString("Controller.Hint.Skip10")));
        hints.Add(("LS", Loc.GetString("Controller.Hint.Filter")));
        hints.Add(("RS", ViewModel.CollectionNames.Count > 0 ? Loc.GetString("Controller.Hint.Collection") : Loc.GetString("Controller.Hint.Covers")));
        hints.Add(("View", Loc.GetString("Controller.Hint.Filters")));
        hints.Add(("Menu", Loc.GetString("Controller.Hint.Settings")));
        return hints;
    }

    /// <summary>A short message just above the footer, e.g. "Launching Witcher 3…".</summary>
    private void ShowPadToast(string text, int ms = 3500)
    {
        try
        {
            if (!EnsurePadOverlay()) return;
            _padToastText!.Text = text;
            _padToast!.Visibility = Visibility.Visible;
            if (_padToastTimer == null)
            {
                _padToastTimer = DispatcherQueue.CreateTimer();
                _padToastTimer.IsRepeating = false;
                _padToastTimer.Tick += (t, _) => { if (_padToast != null) _padToast.Visibility = Visibility.Collapsed; };
            }
            _padToastTimer.Stop();
            _padToastTimer.Interval = TimeSpan.FromMilliseconds(ms);
            _padToastTimer.Start();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.ShowPadToast] {ex.Message}");
        }
    }

    // The footer's centre line (the anti-cheat warning), swapped with the hints
    private TextBlock? _footerWarning;

    /// <summary>
    /// Builds the hints (in the footer's centre cell, beside the warning they replace) and the
    /// message (in the grid that holds both the game page and Settings, so it shows on either)
    /// once, and hides the hints when the mouse moves.
    /// </summary>
    private bool EnsurePadOverlay()
    {
        if (_padHints != null) return true;
        if (VisualTreeHelper.GetParent(GameViewPanel) is not Grid host) return false;
        if (_footer?.Child is not Grid footerGrid) return false;
        _footerWarning = footerGrid.Children.OfType<TextBlock>().FirstOrDefault(t => Grid.GetColumn(t) == 1);

        _padHintRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _padHints = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Child = _padHintRow,
            Visibility = Visibility.Collapsed,
        };
        Grid.SetColumn(_padHints, 1);
        footerGrid.Children.Add(_padHints);
        _padToastText = new TextBlock { FontSize = 14, Foreground = TabsTheme.B(TabsTheme.TextPrimary) };
        _padToast = new Border
        {
            Background = TabsTheme.B("#F01A2336"),
            BorderBrush = TabsTheme.B(TabsTheme.Accent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 10, 18, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 14), // just above the footer
            IsHitTestVisible = false,
            Child = _padToastText,
            Visibility = Visibility.Collapsed,
        };
        Canvas.SetZIndex(_padToast, 60);
        Grid.SetRowSpan(_padToast, Math.Max(1, host.RowDefinitions.Count));
        Grid.SetColumnSpan(_padToast, Math.Max(1, host.ColumnDefinitions.Count));
        host.Children.Add(_padToast);

        if (!_padPointerHooked && Content is UIElement root)
        {
            _padPointerHooked = true;
            root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((s, e) =>
            {
                // While the pointer is hidden, PadTick's cursor-position check decides: the shield
                // appearing under a resting mouse raises a PointerMoved without any movement.
                if (_padHints?.Visibility == Visibility.Visible && !_padPointerHidden
                    && e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
                    ShowPadHints(false);
            }), handledEventsToo: true);
        }
        return true;
    }
}
