using Microsoft.Extensions.DependencyInjection;
using RenoDXCommander.Services;

namespace RenoDXCommander;

/// <summary>
/// Translates combo-box option labels. Keys are "Option." + the English label
/// (e.g. "Option.Off"). The en-US catalog defines every key passed through here,
/// so untranslated technical values (DX12, 4GB, ...) stay in English via fallback;
/// a key missing from every catalog degrades to the raw English label.
/// </summary>
internal static class LocOpt
{
    private static ILocalizationService Loc => App.Services.GetRequiredService<ILocalizationService>();

    /// <summary>Display text for an option whose canonical (logical) value is <paramref name="en"/>.</summary>
    public static string T(string en)
    {
        var key = "Option." + en;
        var s = Loc.GetString(key);
        return s == key ? en : s;
    }

    /// <summary>Display text for the "Global (...)" inherit-from-global entry.</summary>
    public static string Global(string inner) => Loc.GetString("Option.GlobalFormat", inner);
}
