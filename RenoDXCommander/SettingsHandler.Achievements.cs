// SettingsHandler.Achievements.cs -- the "Achievements" card in Settings › Overlay & Achievements (after the RHI
// overlay card): opt-in toggles, one per store, for showing a game's achievements on the Spotlight page and in the
// overlay. Built in C# (XAML constraint) the first time Settings opens. If the overlay section is missing it falls
// back to its own section before Tools & Maintenance.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private ComboBox? _steamAchCombo;
    private ComboBox? _xboxAchCombo;
    private bool _achievementsSectionFailed;

    private void EnsureAchievementsSection()
    {
        if (_steamAchCombo != null)
        {
            _steamAchCombo.SelectedIndex = ViewModel.Settings.SteamAchievements ? 1 : 0;
            if (_xboxAchCombo != null) _xboxAchCombo.SelectedIndex = ViewModel.Settings.XboxAchievements ? 1 : 0;
            return;
        }
        if (_achievementsSectionFailed) return;
        try
        {
            var panel = _window.SettingsCardsPanel;
            int toolsIndex = -1;
            StackPanel? overlaySection = null;
            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is not StackPanel sp || sp.Tag is not string t) continue;
                if (t == "Section:Tools") toolsIndex = i;
                else if (t == "Section:RhiOverlay") overlaySection = sp;
            }
            if (toolsIndex < 0 && overlaySection == null)
            {
                _achievementsSectionFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureAchievementsSection] Tools section not found — section not added");
                return;
            }

            // Normally a card in the Overlay & Achievements section; its own section only if that one failed
            var section = overlaySection ?? new StackPanel { Tag = "Section:Achievements", Spacing = 12 };
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Achievements.Section.Title"),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            });
            head.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Achievements.Section.Description"),
                FontSize = 12,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            });
            if (overlaySection == null) section.Children.Add(head);

            // One card: Steam left, Xbox right (the usual two-column Settings grid)
            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 12 };
            card.Child = body;
            var cardHead = new StackPanel { Spacing = 3 };
            cardHead.Children.Add(new TextBlock { Text = Loc.GetString("Achievements.Card.Title"), Style = FindStyle("SettingsCardTitleStyle") });
            cardHead.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Achievements.Card.Description"),
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            body.Children.Add(cardHead);

            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left = new StackPanel { Spacing = 9, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Top };
            var divider = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush) };
            var right = new StackPanel { Spacing = 9, Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(divider, 1);
            Grid.SetColumn(right, 2);
            columns.Children.Add(left);
            columns.Children.Add(divider);
            columns.Children.Add(right);

            left.Children.Add(MakeAchievementRow(
                "Steam",
                Loc.GetString("Achievements.Steam.Tooltip"),
                () => ViewModel.Settings.SteamAchievements,
                on => ViewModel.Settings.SteamAchievements = on,
                out _steamAchCombo));
            right.Children.Add(MakeAchievementRow(
                "Xbox",
                Loc.GetString("Achievements.Xbox.Tooltip"),
                () => ViewModel.Settings.XboxAchievements,
                on => ViewModel.Settings.XboxAchievements = on,
                out _xboxAchCombo));
            body.Children.Add(columns);
            body.Children.Add(new TextBlock
            {
                Text = Loc.GetString("Achievements.Section.Footnote"),
                Style = FindStyle("SettingsHelpStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            section.Children.Add(card);

            if (overlaySection == null) panel.Children.Insert(toolsIndex, section);
        }
        catch (Exception ex)
        {
            _achievementsSectionFailed = true;
            _steamAchCombo = null;
            CrashReporter.Log($"[SettingsHandler.EnsureAchievementsSection] Failed — {ex.Message}");
        }
    }

    /// <summary>One store's "Steam  [Off / On]" row; details in the combo's tooltip.</summary>
    private Grid MakeAchievementRow(string store, string tooltip, Func<bool> get, Action<bool> set, out ComboBox comboOut)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
        row.Children.Add(new TextBlock
        {
            Text = store,
            Style = FindStyle("SettingsLabelStyle"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
        combo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.Off") });
        combo.Items.Add(new ComboBoxItem { Content = Loc.GetString("Option.On") });
        combo.SelectedIndex = get() ? 1 : 0;
        ToolTipService.SetToolTip(combo, tooltip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, Loc.GetString("Achievements.Row.AutomationName", store));
        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex < 0) return;
            bool on = combo.SelectedIndex == 1;
            if (on == get()) return;
            set(on);
            ViewModel.SaveSettingsPublic();
            CrashReporter.Log($"[SettingsHandler.{store}Achievements] {(on ? "On" : "Off")}");
            _window.AchievementsSettingChanged();
        };
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        comboOut = combo;
        return row;
    }
}
