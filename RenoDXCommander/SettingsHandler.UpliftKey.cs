// SettingsHandler.UpliftKey.cs -- "GITC Uplift" card in Settings › Components, after the ReLimiter and OptiScaler
// card: Uplift's NR on/off key ([Uplift] EnableKey in reshade.ini, a Win32 virtual-key code). RHI writes it into a
// game's reshade.ini when it installs Uplift there; "Write key to all Uplift games" writes it to every game with
// Uplift. None (Uplift's default) writes nothing. Built in C# (XAML constraint), added the first time Settings opens.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private TextBox? _upliftKeyBox;
    private bool _upliftKeyRowFailed;

    private static string UpliftKeyTip => Loc.GetString("Settings.UpliftKey.Tooltip");

    private static string UpliftKeyText(int vk) =>
        vk <= 0 ? Loc.GetString("Option.None") : HotkeyManager.FormatHotkeyDisplay($"{vk},0,0,0");

    private void EnsureUpliftKeySetting()
    {
        if (_upliftKeyBox != null)
        {
            _upliftKeyBox.Text = UpliftKeyText(ViewModel.Settings.UpliftEnableKey);
            return;
        }
        if (_upliftKeyRowFailed) return;
        try
        {
            // The Components section, and its ReLimiter and OptiScaler card (holds the OptiScaler key button)
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Components") { section = sp; break; }
            var osCard = section?.Children.FirstOrDefault(c => Holds(c, _window.ApplyOsHotkeyBtn));
            if (section == null || osCard == null)
            {
                _upliftKeyRowFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureUpliftKeySetting] Components card not found — Uplift card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 9 };
            card.Child = body;
            body.Children.Add(new TextBlock { Text = Loc.GetString("NeuralRendering.Method.GitcUplift"), Style = FindStyle("SettingsCardTitleStyle") });

            // Same shape as the other settings rows: [label | box ↺] in one half-width column
            var row = new Grid { ColumnSpacing = 10, MaxWidth = 470, HorizontalAlignment = HorizontalAlignment.Left };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
            var label = new TextBlock { Text = Loc.GetString("Settings.UpliftKey.Label"), Style = FindStyle("SettingsLabelStyle"), VerticalAlignment = VerticalAlignment.Center };
            ToolTipService.SetToolTip(label, UpliftKeyTip);
            row.Children.Add(label);

            var inner = new Grid { ColumnSpacing = 6 };
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var box = new TextBox
            {
                Style = FindStyle("SettingsTextBoxStyle"),
                IsReadOnly = true,
                PlaceholderText = Loc.GetString("Xaml.PressAKey"),
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                Text = UpliftKeyText(ViewModel.Settings.UpliftEnableKey),
            };
            ToolTipService.SetToolTip(box, UpliftKeyTip);
            box.PreviewKeyDown += (s, e) =>
            {
                var key = e.Key;
                // One key, no modifiers (Uplift's hotkey is a single key); Tab / Enter / Esc keep their normal job
                if (key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.Shift or Windows.System.VirtualKey.Menu
                        or Windows.System.VirtualKey.LeftControl or Windows.System.VirtualKey.RightControl
                        or Windows.System.VirtualKey.LeftShift or Windows.System.VirtualKey.RightShift
                        or Windows.System.VirtualKey.LeftMenu or Windows.System.VirtualKey.RightMenu
                    || (int)key is 91 or 92
                    || IsHotkeyBoxPassThroughKey(key))
                    return;
                int vk = key == Windows.System.VirtualKey.Back ? 0 : (int)key;
                if (vk > 255) return;
                SetUpliftKey(box, vk);
                e.Handled = true;
            };
            Grid.SetColumn(box, 0);
            inner.Children.Add(box);

            var clear = new Button { Content = "↺", Style = FindStyle("SettingsIconButtonStyle"), Foreground = UIFactory.Brush(ResourceKeys.SettingsLabelBrush) };
            ToolTipService.SetToolTip(clear, Loc.GetString("Settings.UpliftKey.Clear.Tooltip"));
            clear.Click += (s, e) => SetUpliftKey(box, 0);
            Grid.SetColumn(clear, 1);
            inner.Children.Add(clear);
            Grid.SetColumn(inner, 1);
            row.Children.Add(inner);
            body.Children.Add(row);

            var write = MakeCustomFoldersButton(Loc.GetString("Settings.UpliftKey.WriteAll"), primary: true,
                Loc.GetString("Settings.UpliftKey.WriteAll.Tooltip"));
            write.HorizontalAlignment = HorizontalAlignment.Left;
            write.Click += async (s, e) =>
            {
                try { await WriteUpliftKeyToAllAsync(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.WriteUpliftKeyToAll] Unhandled error — {ex.Message}"); }
            };
            body.Children.Add(write);

            section.Children.Insert(section.Children.IndexOf(osCard) + 1, card);
            _upliftKeyBox = box;
        }
        catch (Exception ex)
        {
            _upliftKeyRowFailed = true;
            CrashReporter.Log($"[SettingsHandler.EnsureUpliftKeySetting] Failed — {ex.Message}");
        }
    }

    private void SetUpliftKey(TextBox box, int vk)
    {
        box.Text = UpliftKeyText(vk);
        if (ViewModel.Settings.UpliftEnableKey == vk) return;
        ViewModel.Settings.UpliftEnableKey = vk;
        ViewModel.SaveSettingsPublic();
        CrashReporter.Log($"[SettingsHandler.UpliftKey] Saved: {(vk == 0 ? "none" : vk.ToString())}");
    }

    /// <summary>Writes the Uplift key into every game with Uplift (not hidden, reshade.ini not kept by the user).</summary>
    private async Task WriteUpliftKeyToAllAsync()
    {
        int vk = ViewModel.Settings.UpliftEnableKey;
        string message;
        if (vk <= 0)
            message = Loc.GetString("Settings.UpliftKey.PickFirst");
        else
        {
            var games = ViewModel.AllCards
                .Where(c => !c.IsHidden && !string.IsNullOrEmpty(c.InstallPath)
                            && ViewModel.GetKeepRsIniUpdated(c.GameName, c.Source ?? ""))
                .Select(c => c.InstallPath!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            int count = await Task.Run(() => games.Count(path =>
                UpliftService.IsRhiInstalled(path)
                && UpliftService.ApplyEnableKey(Path.Combine(path, "reshade.ini"), vk)));
            CrashReporter.Log($"[SettingsHandler.WriteUpliftKeyToAll] Key {vk} written to {count} game(s)");
            message = Loc.GetString(count == 1 ? "Settings.UpliftKey.WrittenOne" : "Settings.UpliftKey.WrittenMany", UpliftKeyText(vk), count);
        }
        await DialogService.ShowSafeAsync(new ContentDialog
        {
            Title = Loc.GetString("NeuralRendering.Method.GitcUplift"),
            Content = message,
            CloseButtonText = Loc.GetString("Dialog.Ok"),
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        });
    }

    /// <summary>True when <paramref name="target"/> is <paramref name="node"/> or inside it (panels, borders, scroll viewers).</summary>
    private static bool Holds(UIElement node, UIElement target)
    {
        if (ReferenceEquals(node, target)) return true;
        return node switch
        {
            Panel p => p.Children.Any(c => Holds(c, target)),
            Border { Child: { } child } => Holds(child, target),
            ScrollViewer { Content: UIElement content } => Holds(content, target),
            _ => false,
        };
    }
}
