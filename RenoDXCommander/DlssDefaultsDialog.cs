using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

/// <summary>
/// Dialog for configuring DLSS/Streamline default versions, presets, and render scales.
/// These defaults can be applied to any game in one click via the "Apply Defaults" button.
/// </summary>
public static class DlssDefaultsDialog
{
    private static ILocalizationService Loc => App.Services.GetRequiredService<ILocalizationService>();

    public static async Task ShowAsync(MainViewModel viewModel, IDlssStreamlineService dlssService, DlssPresetService presetService, XamlRoot xamlRoot)
    {
        var settings = viewModel.Settings;

        var grid = new Grid { ColumnSpacing = 12, MinWidth = 700 };
        // 4 columns with dividers: SR | div | RR | div | FG | div | SL
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ── SR Column ──
        var srCol = new StackPanel { Spacing = 4 };
        srCol.Children.Add(new TextBlock { Text = Loc.GetString("Xaml.Dlss"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) });

        srCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Version"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var srVersionCombo = BuildCombo(dlssService.DlssVersions, settings.DefaultDlssVersion);
        srCol.Children.Add(srVersionCombo);

        srCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Preset"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var srPresetCombo = BuildPresetComboBox(DlssPresetService.SrPresets, settings.DefaultSrPreset);
        srCol.Children.Add(srPresetCombo);

        srCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.RenderScale"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var srScaleCombo = BuildRenderScaleComboBox(settings.DefaultSrRenderScale);
        srCol.Children.Add(srScaleCombo);

        Grid.SetColumn(srCol, 0);
        grid.Children.Add(srCol);

        // Divider
        grid.Children.Add(MakeDivider(1));

        // ── RR Column ──
        var rrCol = new StackPanel { Spacing = 4 };
        rrCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.RayReconstruction"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) });

        rrCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Version"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var rrVersionCombo = BuildCombo(dlssService.DlssdVersions, settings.DefaultDlssdVersion);
        rrCol.Children.Add(rrVersionCombo);

        rrCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Preset"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var rrPresetCombo = BuildPresetComboBox(DlssPresetService.RrPresets, settings.DefaultRrPreset);
        rrCol.Children.Add(rrPresetCombo);

        rrCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.RenderScale"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var rrScaleCombo = BuildRenderScaleComboBox(settings.DefaultRrRenderScale);
        rrCol.Children.Add(rrScaleCombo);

        Grid.SetColumn(rrCol, 2);
        grid.Children.Add(rrCol);

        // Divider
        grid.Children.Add(MakeDivider(3));

        // ── FG Column ──
        var fgCol = new StackPanel { Spacing = 4 };
        fgCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.FrameGeneration"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) });

        fgCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Version"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var fgVersionCombo = BuildCombo(dlssService.DlssgVersions, settings.DefaultDlssgVersion);
        fgCol.Children.Add(fgVersionCombo);

        fgCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Preset"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var fgPresetCombo = BuildPresetComboBox(DlssPresetService.FgPresets, settings.DefaultFgPreset);
        fgCol.Children.Add(fgPresetCombo);

        Grid.SetColumn(fgCol, 4);
        grid.Children.Add(fgCol);

        // Divider
        grid.Children.Add(MakeDivider(5));

        // ── NR Column (dev-only) ──
        ComboBox? nrVersionCombo = null;
        ComboBox? nrPresetCombo = null;
        if (FeatureFlags.DlssNr)
        {
            // Expand grid to 9 columns: SR, div, RR, div, FG, div, NR, div, SL
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var nrCol = new StackPanel { Spacing = 4 };
            nrCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.NeuralRendering"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) });

            nrCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Version"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
            nrVersionCombo = BuildCombo(dlssService.DlssnrVersions, settings.DefaultDlssnrVersion);
            nrCol.Children.Add(nrVersionCombo);

            nrCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Preset"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
            nrPresetCombo = BuildPresetComboBox(DlssPresetService.NrPresets, settings.DefaultNrPreset);
            nrCol.Children.Add(nrPresetCombo);

            Grid.SetColumn(nrCol, 6);
            grid.Children.Add(nrCol);

            grid.Children.Add(MakeDivider(7));
        }

        int slGridCol = FeatureFlags.DlssNr ? 8 : 6;

        // ── SL Column ──
        var slCol = new StackPanel { Spacing = 4 };
        slCol.Children.Add(new TextBlock { Text = Loc.GetString("Xaml.Streamline"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) });

        slCol.Children.Add(new TextBlock { Text = Loc.GetString("Dialog.Version"), FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0) });
        var slVersionCombo = BuildCombo(dlssService.StreamlineVersions, settings.DefaultStreamlineVersion);
        slCol.Children.Add(slVersionCombo);

        Grid.SetColumn(slCol, slGridCol);
        grid.Children.Add(slCol);

        var dialog = new ContentDialog
        {
            Title = Loc.GetString("Dialog.DlssStreamlineDefaults"),
            Content = grid,
            PrimaryButtonText = Loc.GetString("Dialog.Save"),
            CloseButtonText = Loc.GetString("Dialog.Cancel"),
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        // Override default ContentDialog max width to fit 4 columns
        dialog.Resources["ContentDialogMaxWidth"] = 900.0;

        var result = await DialogService.ShowSafeAsync(dialog);
        if (result != ContentDialogResult.Primary) return;

        // Save selections
        settings.DefaultDlssVersion = GetSelectedVersion(srVersionCombo);
        settings.DefaultDlssdVersion = GetSelectedVersion(rrVersionCombo);
        settings.DefaultDlssgVersion = GetSelectedVersion(fgVersionCombo);
        settings.DefaultStreamlineVersion = GetSelectedVersion(slVersionCombo);
        settings.DefaultSrPreset = GetSelectedPreset(srPresetCombo, DlssPresetService.SrPresets);
        settings.DefaultRrPreset = GetSelectedPreset(rrPresetCombo, DlssPresetService.RrPresets);
        settings.DefaultFgPreset = GetSelectedPreset(fgPresetCombo, DlssPresetService.FgPresets);
        if (FeatureFlags.DlssNr)
        {
            settings.DefaultDlssnrVersion = nrVersionCombo != null ? GetSelectedVersion(nrVersionCombo) : "";
            settings.DefaultNrPreset = nrPresetCombo != null ? GetSelectedPreset(nrPresetCombo, DlssPresetService.NrPresets) : 0u;
        }
        settings.DefaultSrRenderScale = GetSelectedRenderScale(srScaleCombo);
        settings.DefaultRrRenderScale = GetSelectedRenderScale(rrScaleCombo);

        viewModel.SaveSettingsPublic();
        CrashReporter.Log("[DlssDefaultsDialog] Defaults saved");
    }

    private static Border MakeDivider(int column)
    {
        var divider = new Border
        {
            Width = 1,
            Background = UIFactory.Brush(ResourceKeys.BorderSubtleBrush),
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetColumn(divider, column);
        return divider;
    }

    private static ComboBox BuildCombo(IReadOnlyList<string> versions, string currentDefault)
    {
        var items = new List<string> { LocOpt.T("Default") };
        items.AddRange(versions);
        items.Add(LocOpt.T("Custom"));

        int selectedIdx = 0;
        if (!string.IsNullOrEmpty(currentDefault))
        {
            if (string.Equals(currentDefault, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                selectedIdx = items.Count - 1;
            }
            else
            {
                // versions occupy indexes 1..Count-2 (Default at 0, Custom at the end)
                for (int i = 1; i < items.Count - 1; i++)
                {
                    if (versions[i - 1].Equals(currentDefault, StringComparison.Ordinal)
                        || versions[i - 1].StartsWith(currentDefault, StringComparison.OrdinalIgnoreCase))
                    { selectedIdx = i; break; }
                }
            }
        }

        return new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = selectedIdx,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
        };
    }

    private static ComboBox BuildPresetComboBox((string Name, uint Value)[] presets, uint currentDefault)
    {
        var items = presets.Select(p => LocOpt.T(p.Name)).ToList();

        int selectedIdx = 0;
        if (currentDefault != 0)
        {
            var idx = Array.FindIndex(presets, p => p.Value == currentDefault);
            if (idx >= 0) selectedIdx = idx;
        }

        return new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = selectedIdx,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
        };
    }

    private static ComboBox BuildRenderScaleComboBox(uint currentDefault)
    {
        var options = DlssPresetService.RenderScaleOptions;
        var items = options.Select(o => LocOpt.T(o.Name)).ToList();

        int selectedIdx = 0;
        if (currentDefault != 0)
        {
            var idx = Array.FindIndex(options, o => o.Value == currentDefault);
            if (idx >= 0) selectedIdx = idx;
        }

        return new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = selectedIdx,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
        };
    }

    private static string GetSelectedVersion(ComboBox combo)
    {
        if (combo.SelectedIndex <= 0) return ""; // "Default" = empty = don't change
        if (combo.SelectedIndex == combo.Items.Count - 1) return "Custom"; // last item is Custom
        return combo.SelectedItem as string ?? ""; // version numbers are untranslated
    }

    private static uint GetSelectedPreset(ComboBox combo, (string Name, uint Value)[] presets)
    {
        var idx = combo.SelectedIndex;
        if (idx >= 0 && idx < presets.Length) return presets[idx].Value;
        return 0;
    }

    private static uint GetSelectedRenderScale(ComboBox combo)
    {
        var options = DlssPresetService.RenderScaleOptions;
        var idx = combo.SelectedIndex;
        if (idx >= 0 && idx < options.Length) return options[idx].Value;
        return 0;
    }
}
