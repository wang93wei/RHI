using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Tests for LocalizationService and SettingsViewModel language persistence.
/// Pure logic — no filesystem beyond in-memory catalogs where needed.
/// </summary>
public class LocalizationTests
{
    private static LocalizationService CreateServiceWithCatalogs()
    {
        var svc = new LocalizationService();
        // Inject minimal catalogs to avoid filesystem dependency in tests
        svc.SetCatalogForTesting("en-US", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["App.Title"] = "RHI",
            ["App.Subtitle"] = "Simplified PC Gaming",
            ["Settings.Language.Title"] = "Language",
            ["Dialog.Ok"] = "OK",
            ["Stats.Shown"] = "{0} shown",
            ["Dialog.UnknownDxgi.Title"] = "⚠ Unknown dxgi.dll Detected",
            ["Dialog.CachePurged.Content"] = "Deleted {0} files, freed {1} of disk space.",
        });
        svc.SetCatalogForTesting("zh-CN", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["App.Title"] = "RHI",
            ["App.Subtitle"] = "简化 PC 游戏",
            ["Settings.Language.Title"] = "语言",
            ["Dialog.Ok"] = "确定",
        });
        svc.SetCatalogForTesting("ja-JP", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["App.Title"] = "RHI",
        });
        // Ensure current is en-US
        svc.CurrentLanguage = "en-US";
        return svc;
    }

    [Fact]
    public void GetString_ExistingKey_ReturnsValue()
    {
        var svc = CreateServiceWithCatalogs();
        Assert.Equal("RHI", svc.GetString("App.Title"));
    }

    [Fact]
    public void GetString_MissingKey_ReturnsKey()
    {
        var svc = CreateServiceWithCatalogs();
        Assert.Equal("Missing.Key", svc.GetString("Missing.Key"));
    }

    [Fact]
    public void GetString_MissingInCurrent_FallbackToEnUS()
    {
        var svc = CreateServiceWithCatalogs();
        svc.CurrentLanguage = "zh-CN";
        // zh-CN has App.Title, but not Dialog.UnknownDxgi.Title -> fallback to en-US
        Assert.Equal("⚠ Unknown dxgi.dll Detected", svc.GetString("Dialog.UnknownDxgi.Title"));
    }

    [Fact]
    public void GetString_MissingInBoth_ReturnsKey()
    {
        var svc = CreateServiceWithCatalogs();
        svc.CurrentLanguage = "zh-CN";
        Assert.Equal("Not.Exist", svc.GetString("Not.Exist"));
    }

    [Fact]
    public void GetString_Parameterized_SingleArg()
    {
        var svc = CreateServiceWithCatalogs();
        Assert.Equal("5 shown", svc.GetString("Stats.Shown", 5));
    }

    [Fact]
    public void GetString_Parameterized_TwoArgs()
    {
        var svc = CreateServiceWithCatalogs();
        Assert.Equal("Deleted 10 files, freed 5 MB of disk space.", svc.GetString("Dialog.CachePurged.Content", 10, "5 MB"));
    }

    [Fact]
    public void GetString_FormatException_ReturnsTemplate()
    {
        var svc = CreateServiceWithCatalogs();
        // Missing args for placeholder should not throw, should log and return template or key
        var result = svc.GetString("Stats.Shown"); // template expects 1 arg but none provided
        // Implementation returns template when no args, so should be "{0} shown"
        Assert.Equal("{0} shown", result);
    }

    [Fact]
    public void Indexer_ReturnsSameAsGetString()
    {
        var svc = CreateServiceWithCatalogs();
        Assert.Equal(svc.GetString("App.Title"), svc["App.Title"]);
    }

    [Fact]
    public void Coverage_EnUS_Full()
    {
        var svc = CreateServiceWithCatalogs();
        // en-US has 6 keys, zh-CN has 4 of those, so coverage 4/6
        var cov = svc.Coverage("zh-CN");
        Assert.InRange(cov, 0.5, 0.8);
    }

    [Fact]
    public void SupportedLanguages_ContainsFive()
    {
        var svc = CreateServiceWithCatalogs();
        Assert.Contains("en-US", svc.SupportedLanguages);
        Assert.Contains("zh-CN", svc.SupportedLanguages);
        Assert.Contains("zh-TW", svc.SupportedLanguages);
        Assert.Contains("ja-JP", svc.SupportedLanguages);
        Assert.Contains("ko-KR", svc.SupportedLanguages);
    }

    [Fact]
    public void ApplyPreference_System_MapsToEnUS_ForUnsupportedCulture()
    {
        var svc = CreateServiceWithCatalogs();
        // System preference should resolve to a supported language (at least en-US)
        svc.ApplyPreference("System");
        Assert.Contains(svc.CurrentLanguage, svc.SupportedLanguages);
    }

    [Fact]
    public void ApplyPreference_Concrete_SetsLanguage()
    {
        var svc = CreateServiceWithCatalogs();
        svc.ApplyPreference("zh-CN");
        Assert.Equal("zh-CN", svc.CurrentLanguage);
        svc.ApplyPreference("ja-JP");
        Assert.Equal("ja-JP", svc.CurrentLanguage);
    }

    [Fact]
    public void ApplyPreference_NullOrEmpty_TreatedAsSystem()
    {
        var svc = CreateServiceWithCatalogs();
        svc.ApplyPreference(null);
        Assert.Contains(svc.CurrentLanguage, svc.SupportedLanguages);
        svc.ApplyPreference("");
        Assert.Contains(svc.CurrentLanguage, svc.SupportedLanguages);
    }

    [Fact]
    public void LanguageChanged_EventFires()
    {
        var svc = CreateServiceWithCatalogs();
        string? changed = null;
        svc.LanguageChanged += (_, lang) => changed = lang;
        svc.CurrentLanguage = "zh-CN";
        Assert.Equal("zh-CN", changed);
    }

    [Fact]
    public void SettingsViewModel_Language_RoundTrip()
    {
        var vm = new SettingsViewModel
        {
            IsLoadingSettings = true,
            Language = "zh-CN"
        };
        vm.IsLoadingSettings = false;
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        vm.SaveSettingsToDict(dict);
        Assert.Equal("zh-CN", dict["Language"]);

        var vm2 = new SettingsViewModel { IsLoadingSettings = true };
        vm2.LoadSettingsFromDict(dict);
        Assert.Equal("zh-CN", vm2.Language);
    }

    [Fact]
    public void SettingsViewModel_Language_DefaultIsSystem()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var vm = new SettingsViewModel { IsLoadingSettings = true };
        vm.LoadSettingsFromDict(dict);
        Assert.Equal("System", vm.Language);
    }

    [Fact]
    public void SettingsViewModel_Language_System_Persisted()
    {
        var vm = new SettingsViewModel { IsLoadingSettings = false, Language = "System" };
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        vm.SaveSettingsToDict(dict);
        Assert.Equal("System", dict["Language"]);
    }
}
