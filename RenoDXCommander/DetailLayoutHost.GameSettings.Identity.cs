// DetailLayoutHost.GameSettings.Identity.cs — game settings ⚙ rows that came from Game Overrides (v2.9.6; the section
// was removed in 2.9.6.3): Game name, Bitness and Graphics API at the top, Reset at the bottom. The logic is in
// DetailPanelBuilder.Overrides.GameShared.cs / ApiShared.cs.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

internal sealed partial class DetailLayoutHost
{
    private void AddIdentityRows(StackPanel rows, GameCardViewModel card)
    {
        var dp = _window.DetailPanelBuilderInstance;
        string name = card.GameName;
        var status = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
            Visibility = Visibility.Collapsed,
        };
        void Status(string text) { status.Text = text; status.Visibility = Visibility.Visible; }

        // ── Game name: box + Reset ──
        var nameBox = new TextBox { Text = name, FontSize = 12, Width = 280 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nameBox, Loc.GetString("Detail.GameSettings.GameName.Label"));
        void SaveName()
        {
            var newName = nameBox.Text?.Trim() ?? "";
            if (newName.Length == 0) { nameBox.Text = name; return; }
            if (dp.RenameGameTo(name, newName))
            {
                name = newName;
                Status(Loc.GetString("Detail.GameSettings.GameName.RenamedStatus", newName));
            }
        }
        nameBox.KeyDown += (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; SaveName(); } };
        nameBox.LostFocus += (s, e) => SaveName();
        var resetNameBtn = new Button { Content = Loc.GetString("Detail.GameSettings.Reset.Button"), Padding = new Thickness(14, 0, 14, 0) };
        TabsTheme.Apply(resetNameBtn, TabsTheme.IconButton, height: 32, fontSize: 12);
        ToolTipService.SetToolTip(resetNameBtn, Loc.GetString("Detail.GameSettings.ResetName.Tooltip"));
        resetNameBtn.Click += (s, e) =>
        {
            var original = dp.GetOriginalGameName(name);
            nameBox.Text = original;
            if (dp.RenameGameTo(name, original))
            {
                name = original;
                Status(Loc.GetString("Detail.GameSettings.GameName.RevertedStatus", original));
            }
        };
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        nameRow.Children.Add(nameBox);
        nameRow.Children.Add(resetNameBtn);
        AddGameSettingRow(rows, Loc.GetString("Detail.GameSettings.GameName.Label"),
            Loc.GetString("Detail.GameSettings.GameName.Desc"),
            nameRow);

        // ── Bitness ──
        var bitness = new ComboBox { ItemsSource = DetailPanelBuilder.BitnessItems, SelectedItem = dp.GetBitnessDisplay(card), MinWidth = 140 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(bitness, Loc.GetString("Detail.GameSettings.Bitness.Label"));
        bitness.SelectionChanged += (s, e) =>
        {
            try
            {
                if (bitness.SelectedItem is not string sel || sel == dp.GetBitnessDisplay(card)) return;
                bool removed = dp.ApplyBitnessOverride(card, name, sel, rebuildNow: false);
                Status(removed
                    ? Loc.GetString("Detail.GameSettings.Bitness.SetRemoved", sel)
                    : Loc.GetString("Detail.GameSettings.Bitness.Set", sel));
            }
            catch (Exception ex) { CrashReporter.Log($"[DetailLayoutHost.GameSettings] Bitness for '{name}' — {ex.Message}"); }
        };
        AddGameSettingRow(rows, Loc.GetString("Detail.GameSettings.Bitness.Label"),
            Loc.GetString("Detail.GameSettings.Bitness.Desc"),
            bitness);

        // ── Graphics API ──
        var api = new ComboBox { ItemsSource = DetailPanelBuilder.ApiItems, SelectedItem = dp.GetApiDisplay(card), MinWidth = 140 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(api, Loc.GetString("Detail.GameSettings.GraphicsApi.Label"));
        api.SelectionChanged += (s, e) =>
        {
            try
            {
                if (api.SelectedItem is not string sel || sel == dp.GetApiDisplay(card)) return;
                bool removed = dp.ApplyApiOverride(card, name, sel, rebuildNow: false);
                Status(removed
                    ? Loc.GetString("Detail.GameSettings.GraphicsApi.SetRemoved", sel)
                    : Loc.GetString("Detail.GameSettings.GraphicsApi.Set", sel));
            }
            catch (Exception ex) { CrashReporter.Log($"[DetailLayoutHost.GameSettings] Graphics API for '{name}' — {ex.Message}"); }
        };
        AddGameSettingRow(rows, Loc.GetString("Detail.GameSettings.GraphicsApi.Label"),
            Loc.GetString("Detail.GameSettings.GraphicsApi.Desc"),
            api);
        rows.Children.Add(status);
    }

    /// <summary>Reset (DetailPanelBuilder.ResetGameSettings). Two clicks: the first asks.</summary>
    private void AddResetRow(StackPanel rows, GameCardViewModel card)
    {
        var dp = _window.DetailPanelBuilderInstance;
        var btn = new Button { Content = Loc.GetString("Detail.GameSettings.Reset.Button"), Padding = new Thickness(14, 0, 14, 0) };
        TabsTheme.Apply(btn, TabsTheme.DangerButton, height: 32, fontSize: 12);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, Loc.GetString("Detail.GameSettings.Reset.Label"));
        bool armed = false;
        btn.Click += (s, e) =>
        {
            try
            {
                if (!armed)
                {
                    armed = true;
                    btn.Content = Loc.GetString("Detail.GameSettings.Reset.Confirm");
                    return;
                }
                armed = false;
                btn.Content = Loc.GetString("Detail.GameSettings.Reset.Button");
                dp.ResetGameSettings(card);
                btn.Content = Loc.GetString("Detail.GameSettings.Reset.Done");
                btn.IsEnabled = false;
            }
            catch (Exception ex) { CrashReporter.Log($"[DetailLayoutHost.GameSettings] Reset for '{card.GameName}' — {ex.Message}"); }
        };
        AddGameSettingRow(rows, Loc.GetString("Detail.GameSettings.Reset.Label"),
            Loc.GetString("Detail.GameSettings.Reset.Desc"),
            btn);
    }
}
