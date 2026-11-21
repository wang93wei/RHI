// DetailPanelBuilder.Overrides.DriverSettings.Texture.cs — Driver Settings › Texture Filtering group
// (texture LOD bias, anisotropic filtering, texture filtering quality).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private StackPanel BuildTextureFilteringColumn(DriverProfileData d, DlssPresetService svc,
        string capturedName, string installPath)
    {
        var col = new StackPanel { Spacing = 4 };
        var title = new TextBlock { Text = Loc.GetString("Texture.Filtering"), FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
        ToolTipService.SetToolTip(title, Loc.GetString("Texture.Filtering.Tooltip"));
        col.Children.Add(title);

        ComboBox MakeCombo(IList<string> items, int index) => new()
        {
            ItemsSource = items,
            SelectedIndex = index,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
            MaxDropDownHeight = 300,
        };
        void AddLabel(string text) => col.Children.Add(new TextBlock
        {
            Text = text, FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0),
        });

        // LOD Bias
        {
            AddLabel(Loc.GetString("Texture.LodBias"));
            var options = DlssPresetService.LodBiasOptions;
            var items = options.Select(o => o.Name).ToList();
            int idx = Array.FindIndex(options, o => o.Eighths == d.LodBias);
            if (idx < 0 && d.LodBias is int own)
            {
                // A value set elsewhere (Profile Inspector) that isn't in the list: show it as it is
                items.Add(Loc.GetString("Dialog.CustomFpsFormat", DlssPresetService.FormatLodBias(own)));
                idx = items.Count - 1;
            }
            var combo = MakeCombo(items, Math.Max(0, idx));
            ToolTipService.SetToolTip(combo, Loc.GetString("Texture.LodBias.Tooltip"));
            var init = true;
            combo.SelectionChanged += (s, e) =>
            {
                if (init) return;
                int i = combo.SelectedIndex;
                if (i < 0 || i >= options.Length) return; // the Custom entry: nothing to change
                int? value = options[i].Eighths;
                _ = Task.Run(() => svc.SetLodBias(capturedName, installPath, value));
                if (_driverSummaryData != null)
                {
                    _driverSummaryData = _driverSummaryData with { LodBias = value };
                    UpdateDriverSummary();
                }
            };
            col.Children.Add(combo);
            init = false;
        }

        // Anisotropic Filtering
        {
            AddLabel(Loc.GetString("Texture.AnisotropicFiltering"));
            var options = DlssPresetService.AnisoOptions;
            var items = options.Select(o => o.Name).ToList();
            int idx = Array.FindIndex(options, o => o.Level == d.AnisoLevel);
            if (idx < 0)
            {
                items.Add(Loc.GetString("Dialog.CustomFpsFormat", $"{d.AnisoLevel}x"));
                idx = items.Count - 1;
            }
            var combo = MakeCombo(items, idx);
            ToolTipService.SetToolTip(combo, Loc.GetString("Texture.AnisotropicFiltering.Tooltip"));
            var init = true;
            combo.SelectionChanged += (s, e) =>
            {
                if (init) return;
                int i = combo.SelectedIndex;
                if (i < 0 || i >= options.Length) return;
                uint level = options[i].Level;
                _ = Task.Run(() => svc.SetAnisoLevel(capturedName, installPath, level));
                if (_driverSummaryData != null)
                {
                    _driverSummaryData = _driverSummaryData with { AnisoLevel = level };
                    UpdateDriverSummary();
                }
            };
            col.Children.Add(combo);
            init = false;
        }

        // Texture Filtering Quality
        {
            AddLabel(Loc.GetString("Extras.RtxEncore.Opt.Quality"));
            var options = DlssPresetService.TextureQualityOptions;
            var items = options.Select(o => o.Name).ToList();
            int idx = Array.FindIndex(options, o => o.Value == d.TextureQuality);
            if (idx < 0)
            {
                items.Add(Loc.GetString("Dialog.CustomFpsFormat", $"0x{d.TextureQuality:X8}"));
                idx = items.Count - 1;
            }
            var combo = MakeCombo(items, idx);
            ToolTipService.SetToolTip(combo, Loc.GetString("Texture.Quality.Tooltip"));
            var init = true;
            combo.SelectionChanged += (s, e) =>
            {
                if (init) return;
                int i = combo.SelectedIndex;
                if (i < 0 || i >= options.Length) return;
                uint? value = options[i].Value;
                _ = Task.Run(() => svc.SetTextureQuality(capturedName, installPath, value));
            };
            col.Children.Add(combo);
            init = false;
        }

        return col;
    }
}
