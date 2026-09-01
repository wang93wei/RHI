using System.ComponentModel;

namespace RenoDXCommander.Services;

/// <summary>
/// Provides localized strings with fallback and live language switching.
/// Implemented by <see cref="LocalizationService"/> as a singleton.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    /// <summary>Currently active concrete language, e.g. "en-US".</summary>
    string CurrentLanguage { get; set; }

    /// <summary>Fallback language used when a key is missing. Always "en-US".</summary>
    string FallbackLanguage { get; }

    /// <summary>Supported concrete languages.</summary>
    IReadOnlySet<string> SupportedLanguages { get; }

    /// <summary>Indexer for XAML binding: {Binding [Key], Source={StaticResource Loc}}</summary>
    string this[string key] { get; }

    /// <summary>
    /// Resolves a localized string with optional format arguments.
    /// Falls back to <see cref="FallbackLanguage"/> then to the key itself.
    /// Never throws; missing keys are logged.
    /// </summary>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Maps the OS UI culture to the nearest supported language.
    /// </summary>
    string ResolveSystemLanguage();

    /// <summary>
    /// Applies a preference value ("System" or concrete language) to <see cref="CurrentLanguage"/>.
    /// </summary>
    void ApplyPreference(string? preference);

    /// <summary>
    /// Returns translation coverage (0..1) for a language vs fallback.
    /// </summary>
    double Coverage(string lang);

    /// <summary>Fired when <see cref="CurrentLanguage"/> changes.</summary>
    event EventHandler<string>? LanguageChanged;
}
