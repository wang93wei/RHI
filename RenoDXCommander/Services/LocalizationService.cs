using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RenoDXCommander.Services;

/// <summary>
/// JSON-backed localization service. Loads flat key-value JSON files from
/// Assets/Languages/{lang}.json. Supports live switching via INotifyPropertyChanged.
/// </summary>
public partial class LocalizationService : ObservableObject, ILocalizationService
{
    public const string DefaultLanguage = "en-US";
    public const string PreferenceSystem = "System";

    private static readonly HashSet<string> _supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "en-US", "zh-CN", "zh-TW", "ja-JP", "ko-KR"
    };

    // Alias map: en-GB -> en-US, zh-Hans -> zh-CN, etc.
    private static readonly Dictionary<string, string> _aliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-GB"] = "en-US",
        ["en"] = "en-US",
        ["zh-Hans"] = "zh-CN",
        ["zh-Hant"] = "zh-TW",
        ["zh-SG"] = "zh-CN",
        ["zh-HK"] = "zh-TW",
        ["zh-MO"] = "zh-TW",
        ["ja"] = "ja-JP",
        ["ko"] = "ko-KR",
    };

    private readonly ConcurrentDictionary<string, bool> _loggedMissingKeys = new(StringComparer.OrdinalIgnoreCase);

    // lang -> (key -> value)
    private readonly Dictionary<string, Dictionary<string, string>> _catalogs = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _currentLanguage = DefaultLanguage;

    public string FallbackLanguage => DefaultLanguage;

    public IReadOnlySet<string> SupportedLanguages => _supported;

    public event EventHandler<string>? LanguageChanged;

    public string this[string key] => GetString(key);

    public LocalizationService()
    {
        LoadCatalogs();
        // Initialize to system language until settings override it
        try
        {
            var sys = ResolveSystemLanguage();
            if (_supported.Contains(sys))
                _currentLanguage = sys;
        }
        catch { /* keep default */ }
    }

    partial void OnCurrentLanguageChanged(string value)
    {
        // Normalize alias
        if (_aliasMap.TryGetValue(value, out var aliased))
            value = aliased;

        if (!_supported.Contains(value))
        {
            CrashReporter.Log($"[Localization] Unsupported language '{value}' — falling back to {DefaultLanguage}");
            value = DefaultLanguage;
            // prevent recursion loop if setter was already default
            if (string.Equals(_currentLanguage, value, StringComparison.OrdinalIgnoreCase))
                return;
            _currentLanguage = value;
            return;
        }

        CrashReporter.Log($"[Localization] Language changed to {value}");

        // Notify indexer and all bindings. In WinUI, empty string notifies all, "Item[]" notifies indexer.
        OnPropertyChanged(string.Empty);
        OnPropertyChanged("Item[]");
        // Also raise explicit indexer property for WPF compatibility
        OnPropertyChanged("Item");

        LanguageChanged?.Invoke(this, value);
    }

    public void ApplyPreference(string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference) || string.Equals(preference, PreferenceSystem, StringComparison.OrdinalIgnoreCase))
        {
            CurrentLanguage = ResolveSystemLanguage();
            return;
        }

        // Normalize input (allow "en-us", "en", "zh")
        var normalized = NormalizeLanguageCode(preference);
        CurrentLanguage = normalized;
    }

    public string ResolveSystemLanguage()
    {
        try
        {
            var cultureName = CultureInfo.CurrentUICulture.Name; // e.g. "zh-CN", "en-US", "ja-JP"
            if (string.IsNullOrWhiteSpace(cultureName))
                cultureName = CultureInfo.CurrentCulture.Name;

            return MapToSupported(cultureName);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[Localization.ResolveSystemLanguage] Failed — {ex.Message}");
            return DefaultLanguage;
        }
    }

    public string GetString(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        string? template = null;

        // 1. Current language
        if (_catalogs.TryGetValue(CurrentLanguage, out var currentDict) && currentDict.TryGetValue(key, out template))
        {
            // found
        }
        // 2. Alias fallback (if current is alias, already normalized, so skip)
        // 3. Fallback language
        else if (_catalogs.TryGetValue(FallbackLanguage, out var fallbackDict) && fallbackDict.TryGetValue(key, out template))
        {
            // Log missing in current but present in fallback (only once)
            if (!_supported.Contains(CurrentLanguage) || !string.Equals(CurrentLanguage, FallbackLanguage, StringComparison.OrdinalIgnoreCase))
            {
                var logKey = $"{CurrentLanguage}:{key}";
                if (_loggedMissingKeys.TryAdd(logKey, true))
                    CrashReporter.Log($"[Localization] Missing key '{key}' in '{CurrentLanguage}' — fallback to '{FallbackLanguage}'");
            }
        }
        else
        {
            // Missing in both
            var logKey = $"missing:{key}";
            if (_loggedMissingKeys.TryAdd(logKey, true))
                CrashReporter.Log($"[Localization] Missing key '{key}' in both '{CurrentLanguage}' and fallback '{FallbackLanguage}' — returning key");
            return key;
        }

        if (args == null || args.Length == 0)
            return template ?? key;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template!, args);
        }
        catch (FormatException ex)
        {
            CrashReporter.Log($"[Localization.GetString] Format failed for key '{key}' — {ex.Message}");
            return template ?? key;
        }
    }

    public double Coverage(string lang)
    {
        if (!_catalogs.TryGetValue(FallbackLanguage, out var fallbackDict) || fallbackDict.Count == 0)
            return 0;
        if (!_catalogs.TryGetValue(lang, out var dict))
            return 0;
        var total = fallbackDict.Count;
        var present = dict.Count(kv => !string.IsNullOrWhiteSpace(kv.Value));
        // Only count keys that exist in fallback
        var covered = fallbackDict.Keys.Count(k => dict.ContainsKey(k) && !string.IsNullOrWhiteSpace(dict[k]));
        return total == 0 ? 0 : (double)covered / total;
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    private static string NormalizeLanguageCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return DefaultLanguage;
        code = code.Trim();
        // Handle alias directly
        if (_aliasMap.TryGetValue(code, out var aliased))
            return aliased;
        // Normalize case: "en-us" -> "en-US", "zh-cn" -> "zh-CN"
        var parts = code.Split('-');
        if (parts.Length == 1)
        {
            // e.g. "en" -> check alias, else map prefix
            var lower = parts[0].ToLowerInvariant();
            return lower switch
            {
                "en" => "en-US",
                "zh" => "zh-CN",
                "ja" => "ja-JP",
                "ko" => "ko-KR",
                _ => DefaultLanguage
            };
        }
        if (parts.Length >= 2)
        {
            var lang = parts[0].ToLowerInvariant();
            var region = parts[1].ToUpperInvariant();
            var combined = $"{lang}-{region}";
            if (_supported.Contains(combined))
                return combined;
            if (_aliasMap.TryGetValue(combined, out var a))
                return a;
            // Prefix fallback
            return lang switch
            {
                "en" => "en-US",
                "zh" => region == "TW" || region == "HK" || region == "MO" || region == "HANT" ? "zh-TW" : "zh-CN",
                "ja" => "ja-JP",
                "ko" => "ko-KR",
                _ => DefaultLanguage
            };
        }
        return DefaultLanguage;
    }

    private static string MapToSupported(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return DefaultLanguage;

        // Exact or alias
        if (_supported.Contains(cultureName))
            return cultureName;
        if (_aliasMap.TryGetValue(cultureName, out var aliased))
            return aliased;

        // Try prefix match: "zh-CN" -> "zh-CN", "zh" -> "zh-CN"
        var normalized = NormalizeLanguageCode(cultureName);
        if (_supported.Contains(normalized))
            return normalized;

        // Language prefix fallback
        var langPrefix = cultureName.Split('-')[0].ToLowerInvariant();
        return langPrefix switch
        {
            "zh" => "zh-CN",
            "en" => "en-US",
            "ja" => "ja-JP",
            "ko" => "ko-KR",
            _ => DefaultLanguage
        };
    }

    private void LoadCatalogs()
    {
        var dirs = GetCandidateLanguageDirs();
        string? foundDir = null;
        foreach (var dir in dirs)
        {
            if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.json").Length > 0)
            {
                foundDir = dir;
                break;
            }
        }

        if (foundDir == null)
        {
            CrashReporter.Log($"[Localization.LoadCatalogs] No language directory found. Tried: {string.Join("; ", dirs)}");
            // Ensure fallback catalog exists as empty to avoid NRE
            _catalogs[DefaultLanguage] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        CrashReporter.Log($"[Localization.LoadCatalogs] Loading from {foundDir}");

        // Load all supported + fallback
        var langsToLoad = new HashSet<string>(_supported, StringComparer.OrdinalIgnoreCase) { DefaultLanguage };
        foreach (var lang in langsToLoad)
        {
            var file = Path.Combine(foundDir, $"{lang}.json");
            if (!File.Exists(file))
            {
                CrashReporter.Log($"[Localization.LoadCatalogs] Missing file for '{lang}': {file}");
                // Create empty to allow fallback logic
                if (!_catalogs.ContainsKey(lang))
                    _catalogs[lang] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            try
            {
                var json = File.ReadAllText(file);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                _catalogs[lang] = dict != null
                    ? new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                CrashReporter.Log($"[Localization.LoadCatalogs] Loaded {lang}: {_catalogs[lang].Count} keys");
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[Localization.LoadCatalogs] Failed to load '{lang}' — {ex.Message}");
                _catalogs[lang] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static List<string> GetCandidateLanguageDirs()
    {
        var list = new List<string>();
        try
        {
            var baseDir = AppContext.BaseDirectory;
            list.Add(Path.Combine(baseDir, "Assets", "Languages"));
            // When running from solution root via dotnet run
            list.Add(Path.Combine(baseDir, "RenoDXCommander", "Assets", "Languages"));
            // Current directory fallback
            list.Add(Path.Combine(Directory.GetCurrentDirectory(), "RenoDXCommander", "Assets", "Languages"));
            list.Add(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Languages"));
            // For tests: relative to repo root
            var repoRoot = FindRepoRoot(baseDir);
            if (repoRoot != null)
            {
                list.Add(Path.Combine(repoRoot, "RenoDXCommander", "Assets", "Languages"));
            }
        }
        catch { }
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? FindRepoRoot(string start)
    {
        try
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 6 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "RenoDXCommander.sln")) ||
                    Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch { }
        return null;
    }

    // For testing: inject catalog directly
    internal void SetCatalogForTesting(string lang, Dictionary<string, string> dict)
    {
        _catalogs[lang] = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
    }
}
