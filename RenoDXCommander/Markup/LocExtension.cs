using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using RenoDXCommander.Services;

namespace RenoDXCommander.Markup;

/// <summary>
/// Markup extension for localization: {markup:Loc Key=Settings.Title}
/// Provides a Binding to LocalizationService indexer for live updates.
/// </summary>
[MarkupExtensionReturnType(ReturnType = typeof(Binding))]
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue()
    {
        // Create a binding to ILocalizationService[Key] with StaticResource Loc
        // WinUI does not support full markup extension binding creation in ProvideValue easily,
        // so we fallback to direct string lookup for initial value and rely on page-level
        // Binding for live updates. For live-updating XAML, prefer:
        // Text="{Binding [Settings.Title], Source={StaticResource Loc}}"
        try
        {
            var loc = App.Services?.GetService(typeof(ILocalizationService)) as ILocalizationService;
            return loc?.GetString(Key) ?? Key;
        }
        catch { return Key; }
    }
}
