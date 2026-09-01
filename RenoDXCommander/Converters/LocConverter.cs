using Microsoft.UI.Xaml.Data;
using RenoDXCommander.Services;

namespace RenoDXCommander.Converters;

/// <summary>
/// Converts a localization key to its translated string.
/// Usage: Text="{Binding Converter={StaticResource LocConverter}, ConverterParameter=Settings.Title}"
/// For parameterized strings, pass the key as parameter and use LocConverter with args via MultiBinding is not supported,
/// prefer code-behind GetString or Binding to indexer.
/// </summary>
public class LocConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            var key = parameter as string ?? value as string;
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var loc = App.Services.GetService(typeof(ILocalizationService)) as ILocalizationService;
            return loc?.GetString(key) ?? key;
        }
        catch { return parameter as string ?? value as string ?? string.Empty; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
