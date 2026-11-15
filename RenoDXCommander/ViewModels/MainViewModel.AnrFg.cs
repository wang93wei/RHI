// MainViewModel.AnrFg.cs — installs ANR FG (Apex Neural Rendering frame generation) for the
// OptiScaler preset that asks for it (installAnrFg in optiscaler_presets.json).

using Microsoft.Extensions.DependencyInjection;
using RenoDXCommander.Services;

namespace RenoDXCommander.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Makes sure ANR FG is in the game's OptiScaler custom folder, downloading or updating it first.
    /// Needs an OptiScaler build that loads custom frame generation providers: Nightly from 29 Oct 2026,
    /// or a fork / custom build based on one. Checked in the installed DLL, not by version name.
    /// Returns an error message, or null when it's in place.
    /// </summary>
    public async Task<string?> EnsureAnrFgForPresetAsync(GameCardViewModel card)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return Loc.GetString("OptiScaler.Preset.Mfg.NoInstallFolder");
        if (!card.IsOsInstalled) return Loc.GetString("AnrFg.Status.NeedsOptiScaler");

        var installPath = card.InstallPath;
        var record = _auxInstaller.FindRecord(card.GameName, installPath, OptiScalerService.AddonType);
        var variant = record?.OsVariant ?? OsVariants.Stable;
        var osDll = string.IsNullOrEmpty(record?.InstalledAs) ? null : FileHelper.SafeCombine(installPath, record.InstalledAs);
        bool supported = osDll != null && await Task.Run(() => OptiScalerService.SupportsCustomFgProviders(osDll));
        if (!supported)
        {
            _crashReporter.Log($"[MainViewModel.EnsureAnrFgForPreset] '{card.GameName}': OptiScaler {variant} ({record?.InstalledAs ?? "no record"}) can't load custom frame generation providers");
            return Loc.GetString("AnrFg.Status.OsTooOld", OsVariants.Label(variant));
        }

        var anr = App.Services.GetRequiredService<AnrFgService>();
        try
        {
            var error = await Task.Run(async () =>
            {
                if (!await anr.EnsureStagingAsync().ConfigureAwait(false))
                    return Loc.GetString("AnrFg.Error.DownloadFailed");
                return anr.Install(installPath);
            });
            if (error == null)
                _crashReporter.Log($"[MainViewModel.EnsureAnrFgForPreset] ANR FG {anr.StagedVersion} in place for '{card.GameName}'");
            return error;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException)
        {
            _crashReporter.Log($"[MainViewModel.EnsureAnrFgForPreset] '{card.GameName}' — {ex.Message}");
            return Loc.GetString("AnrFg.Error.InstallFailed", ex.Message);
        }
    }
}
