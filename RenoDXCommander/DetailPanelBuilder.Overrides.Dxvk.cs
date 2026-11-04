// DetailPanelBuilder.Overrides.Dxvk.cs — Management section.
// DXVK controls have moved to DetailPanelBuilder.Extras.cs under the API Upgrades sub-header.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    internal static readonly string[] DcDllOverrideNames =
    [
        "dxgi.dll", "d3d9.dll", "d3d11.dll", "d3d12.dll", "ddraw.dll",
        "hid.dll", "version.dll", "opengl32.dll", "dbghelp.dll",
        "vulkan-1.dll", "winmm.dll",
    ];

    private void BuildManagementSection(GameCardViewModel card, string capturedName, OverridesPanelCtx ctx)
    {
        _window.ViewModel.SetLastUiAction($"BuildManagementSection({card.GameName})");
        // ── Management section (single row: 5 buttons side by side with separators) ──
        _window.ManagementPanel.Children.Clear();

        // Collapsible header
        const string mgmtSectionKey = "Management";
        bool mgmtCollapsed = Layout.IsCollapsed(mgmtSectionKey);

        var mgmtArrow = new TextBlock
        {
            Text      = mgmtCollapsed ? "▶" : "▼",
            FontSize  = 10,
            Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(0, 0, 6, 0),
        };
        var mgmtTitle = new TextBlock
        {
            Text       = Loc.GetString("Xaml.Management"),
            FontSize   = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var mgmtHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        ToolTipService.SetToolTip(mgmtArrow, SectionHeaderTooltip);
        ToolTipService.SetToolTip(mgmtTitle, SectionHeaderTooltip);
        mgmtHeaderRow.Children.Add(MakeDragHandle(_window.ManagementContainer));
        mgmtHeaderRow.Children.Add(mgmtArrow);
        mgmtHeaderRow.Children.Add(mgmtTitle);
        _window.ManagementPanel.Children.Add(mgmtHeaderRow);
        Layout.RegisterHeader(mgmtSectionKey, mgmtHeaderRow);

        // Body wrapper
        var mgmtBody = new StackPanel { Spacing = 6, Visibility = mgmtCollapsed ? Visibility.Collapsed : Visibility.Visible };
        _window.ManagementPanel.Children.Add(mgmtBody);

        mgmtHeaderRow.PointerEntered += (s, e) => mgmtTitle.Foreground = UIFactory.Brush(ResourceKeys.AccentTealBrush);
        mgmtHeaderRow.PointerExited  += (s, e) => mgmtTitle.Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush);
        var mgmtHandCursor  = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        var mgmtArrowCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
        var mgmtCursorProp  = DetailPanelBuilder.CursorProp;
        mgmtHeaderRow.PointerEntered += (s, e) => mgmtCursorProp?.SetValue(mgmtHeaderRow, mgmtHandCursor);
        mgmtHeaderRow.PointerExited  += (s, e) => mgmtCursorProp?.SetValue(mgmtHeaderRow, mgmtArrowCursor);
        mgmtHeaderRow.PointerPressed += (s, e) =>
        {
            bool nowCollapsed = mgmtBody.Visibility == Visibility.Visible;
            mgmtBody.Visibility = nowCollapsed ? Visibility.Collapsed : Visibility.Visible;
            mgmtArrow.Text = nowCollapsed ? "▶" : "▼";
            Layout.SetCollapsed(mgmtSectionKey, nowCollapsed);
        };

        var mgmtRow = new Grid { ColumnSpacing = 0 };
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var changeFolderBtn = new Button
        {
            Content = Loc.GetString("Dialog.ChangeInstallFolder"),
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = card,
        };
        changeFolderBtn.Click += (s, ev) => _window.BrowseFolder_Click(s, ev);
        ToolTipService.SetToolTip(changeFolderBtn, Loc.GetString("Overrides.ChangeFolder.Tooltip"));
        Grid.SetColumn(changeFolderBtn, 0);
        mgmtRow.Children.Add(changeFolderBtn);

        var sep1 = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush), Margin = new Thickness(8, 4, 8, 4) };
        Grid.SetColumn(sep1, 1);
        mgmtRow.Children.Add(sep1);

        var resetFolderBtn = new Button
        {
            Content = Loc.GetString("Overrides.Management.ResetInstallFolder.Button"),
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = card,
            IsEnabled = !card.IsManuallyAdded,
        };
        resetFolderBtn.Click += (s, ev) => _window.ResetInstallFolder_Click(s, ev);
        ToolTipService.SetToolTip(resetFolderBtn, card.IsManuallyAdded
            ? Loc.GetString("Overrides.Management.ResetInstallFolder.Tooltip.Manual")
            : Loc.GetString("Overrides.Management.ResetInstallFolder.Tooltip.Auto"));
        Grid.SetColumn(resetFolderBtn, 2);
        mgmtRow.Children.Add(resetFolderBtn);

        var sepReset = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush), Margin = new Thickness(8, 4, 8, 4) };
        Grid.SetColumn(sepReset, 3);
        mgmtRow.Children.Add(sepReset);

        var removeGameBtn = new Button
        {
            Content = Loc.GetString("Overrides.Management.RemoveFromRhi.Button"),
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = card,
        };
        removeGameBtn.Click += (s, ev) => _window.RemoveManualGame_Click(s, ev);
        ToolTipService.SetToolTip(removeGameBtn, card.IsManuallyAdded
            ? Loc.GetString("Overrides.Management.RemoveGame.Tooltip.Manual")
            : Loc.GetString("Overrides.Management.RemoveGame.Tooltip.Auto"));
        Grid.SetColumn(removeGameBtn, 4);
        mgmtRow.Children.Add(removeGameBtn);

        var sep2 = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush), Margin = new Thickness(8, 4, 8, 4) };
        Grid.SetColumn(sep2, 5);
        mgmtRow.Children.Add(sep2);

        var mgmtResetOverridesBtn = new Button
        {
            Content = Loc.GetString("Shader.ResetOverrides"),
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        mgmtResetOverridesBtn.Click += (s, ev) =>
        {
            // Call reset action directly — automation peer invoke fails on Visibility.Collapsed buttons
            ctx.ResetAction?.Invoke();
        };
        Grid.SetColumn(mgmtResetOverridesBtn, 6);
        ToolTipService.SetToolTip(mgmtResetOverridesBtn, Loc.GetString("Overrides.ResetOverrides.Tooltip"));
        mgmtRow.Children.Add(mgmtResetOverridesBtn);

        var sep3 = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush), Margin = new Thickness(8, 4, 8, 4) };
        Grid.SetColumn(sep3, 7);
        mgmtRow.Children.Add(sep3);

        var reportBtn = new Button
        {
            Content = Loc.GetString("Dialog.CopyReport"),
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        async void CopyReport()
        {
            try
            {
                var targetCard = _window.ViewModel.AllCards.FirstOrDefault(c =>
                    c.GameName.Equals(capturedName, StringComparison.OrdinalIgnoreCase));
                if (targetCard != null)
                    await GameReportEncoder.ShowAndCopyAsync(_window.Content.XamlRoot, targetCard, _window.ViewModel);
            }
            catch (Exception ex)
            {
                Services.CrashReporter.Log($"[BuildManagementSection] Copy Report failed — {ex.Message}");
            }
        }
        reportBtn.Click += (s, ev) => CopyReport();
        Grid.SetColumn(reportBtn, 8);
        ToolTipService.SetToolTip(reportBtn, Loc.GetString("Overrides.CopyReport.Tooltip"));
        mgmtRow.Children.Add(reportBtn);

        if (Layout.IsTabs)
        {
            // Tabs (mockup A): the same four actions as tiles
            var tiles = new Controls.AutoFitGrid { MinColumnWidth = 220 };
            tiles.Children.Add(TabsTheme.MakeTile(
                Loc.GetString("Dialog.ChangeInstallFolder"),
                Loc.GetString("Overrides.Management.Tile.ChangeFolder.Desc"), false,
                (s, ev) => _window.BrowseFolder_Click(changeFolderBtn, ev), TabsTooltip(changeFolderBtn)));
            tiles.Children.Add(TabsTheme.MakeTile(
                Loc.GetString("Dialog.ResetOverrides"),
                Loc.GetString("Overrides.Management.Tile.ResetOverrides.Desc"), false,
                (s, ev) => ctx.ResetAction?.Invoke(), TabsTooltip(mgmtResetOverridesBtn)));
            tiles.Children.Add(TabsTheme.MakeTile(
                Loc.GetString("Dialog.CopyReport"),
                Loc.GetString("Overrides.Management.Tile.CopyReport.Desc"), false,
                (s, ev) => CopyReport(), TabsTooltip(reportBtn)));
            if (!card.IsManuallyAdded)
                tiles.Children.Add(TabsTheme.MakeTile(
                    Loc.GetString("Overrides.Management.ResetInstallFolder.Button"),
                    Loc.GetString("Overrides.Management.Tile.ResetFolder.Desc"), false,
                    (s, ev) => _window.ResetInstallFolder_Click(resetFolderBtn, ev), TabsTooltip(resetFolderBtn)));
            tiles.Children.Add(TabsTheme.MakeTile(
                Loc.GetString("Overrides.Management.RemoveFromRhi.Button"),
                card.IsManuallyAdded
                    ? Loc.GetString("Overrides.Management.Tile.Remove.Desc.Manual")
                    : Loc.GetString("Overrides.Management.Tile.Remove.Desc.Auto"), true,
                (s, ev) => _window.RemoveManualGame_Click(removeGameBtn, ev), TabsTooltip(removeGameBtn)));
            mgmtBody.Children.Add(tiles);
            return;
        }

        mgmtBody.Children.Add(mgmtRow);
    }

    private static string? TabsTooltip(DependencyObject d) => ToolTipService.GetToolTip(d) as string;
}
