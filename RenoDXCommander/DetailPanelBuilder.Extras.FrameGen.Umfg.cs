// DetailPanelBuilder.Extras.FrameGen.Umfg.cs — Universal MFG Unlocker ⚙ sections: RTX Hair (dxcompiler.dll / dxil.dll
// beside the exe, RTXMFG.RtxHair.ini) and DLSS 5 (nvngx_dlssnr.dll beside the exe). Wording follows the mod's
// docs/compatibility.md ("Path traced hair") and docs/menu.md.

using Microsoft.Extensions.DependencyInjection;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private void AddUmfgRtxHairSection(CogDialog cog, GameCardViewModel card, Rtx40MfgService mfgSvc, bool isInstalled, bool isHsfMods)
    {
        var installPath = card.InstallPath ?? "";
        cog.AddSection(Loc.GetString("Extras.Umfg.RtxHair.Section"));
        cog.AddIntro(Loc.GetString("Extras.Umfg.RtxHair.Intro"));

        if (!isInstalled || string.IsNullOrEmpty(installPath) || isHsfMods)
        {
            if (isHsfMods) cog.AddNote(Loc.GetString("Extras.Umfg.RtxHair.HsfNote"));
            return;
        }

        // Status of the two files
        var family = GpuInfoService.Cached is { } gpus ? GpuInfoService.Primary(gpus)?.Family : null;
        bool dx12 = _window.ViewModel.UmfgRunsDx12(card);
        var owned = Rtx40MfgService.DxcFilesOwnedIn(installPath);
        var present = Rtx40MfgService.DxcFiles.Where(f => File.Exists(Path.Combine(installPath, f))).ToList();
        if (family is GpuFamily.Rtx50 or GpuFamily.Newer)
            cog.AddNote(Loc.GetString("Extras.Umfg.RtxHair.Rtx50Note", GpuInfoService.FamilyLabel(family.Value)));
        else if (!dx12)
            cog.AddNote(card.RequiresVulkanInstall || card.IsVulkanOnly
                ? Loc.GetString("Extras.Umfg.RtxHair.VulkanNote")
                : Loc.GetString("Extras.Umfg.RtxHair.NoDx12Note"));
        else if (present.Count == Rtx40MfgService.DxcFiles.Length)
            cog.AddNote(owned.Count == present.Count
                ? Loc.GetString("Extras.Umfg.RtxHair.Installed", mfgSvc.DxcStagedVersion is { } v ? $" ({v})" : "")
                : owned.Count == 0 ? Loc.GetString("Extras.Umfg.RtxHair.GamesOwn")
                : Loc.GetString("Extras.Umfg.RtxHair.Mixed"));
        else if (family is GpuFamily.Rtx20 or GpuFamily.NotRtx)
            cog.AddNote(Loc.GetString("Extras.Umfg.RtxHair.NotSupported"));
        else
            cog.AddWarning(Loc.GetString("Extras.Umfg.RtxHair.Missing",
                string.Join(", ", Rtx40MfgService.DxcFiles.Except(present, StringComparer.OrdinalIgnoreCase))));

        bool on = Rtx40MfgService.IsRtxHairOn(installPath);
        cog.AddCombo(Loc.GetString("Extras.Umfg.RtxHair.Section"),
            Loc.GetString("Extras.Umfg.RtxHair.Combo.Help", Rtx40MfgService.RtxHairIniName),
            new[] { Loc.GetString("Option.On"), Loc.GetString("Option.Off") }, on ? 0 : 1,
            idx =>
            {
                bool ok = Rtx40MfgService.SetRtxHairOn(installPath, idx == 0);
                if (ok) cog.SetSuccess(idx == 0 ? Loc.GetString("Extras.Umfg.RtxHair.On.Success") : Loc.GetString("Extras.Umfg.RtxHair.Off.Success"));
                else cog.SetError(Loc.GetString("Extras.Umfg.RtxHair.WriteFailed", Rtx40MfgService.RtxHairIniName));
                return ok;
            });
    }

    private void AddUmfgDlss5Section(CogDialog cog, GameCardViewModel card, Rtx40MfgService mfgSvc, bool isInstalled, bool isHsfMods)
    {
        var installPath = card.InstallPath ?? "";
        cog.AddSection(Loc.GetString("Extras.Umfg.Dlss5.Section"));
        cog.AddIntro(Loc.GetString("Extras.Umfg.Dlss5.Intro"));
        if (string.IsNullOrEmpty(installPath)) return;

        var dest = Path.Combine(installPath, Rtx40MfgService.NrDllName);
        bool present = File.Exists(dest);
        bool gamesOwn = present && !File.Exists(dest + ".original");
        bool owned = Rtx40MfgService.OwnsNrDll(installPath);
        var others = present
            ? Models.RhiInstallManifest.GetSharedFileOwners(installPath, Rtx40MfgService.NrDllName)
                .Where(o => !o.Equals(Rtx40MfgService.NrOwnerName, StringComparison.OrdinalIgnoreCase)).ToList()
            : new System.Collections.Generic.List<string>();
        string? ver = null;
        if (present)
        {
            try
            {
                var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(DlssNrCostScalerService.ResolveRealNrDllPath(dest));
                ver = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { CrashReporter.Log($"[UmfgCog] Couldn't read the NR DLL version — {ex.Message}"); }
        }
        string verText = ver != null ? $" ({ver})" : "";
        cog.AddNote(!present ? Loc.GetString("Extras.Umfg.Dlss5.NotPresent")
            : gamesOwn ? Loc.GetString("Extras.Umfg.Dlss5.GamesOwn", verText)
            : others.Count > 0 ? Loc.GetString("Extras.Umfg.Dlss5.InstalledByOthers", verText, string.Join(", ", others))
            : Loc.GetString("Extras.Umfg.Dlss5.Installed", verText));

        if (!isInstalled || isHsfMods || gamesOwn) return;
        var family = GpuInfoService.Cached is { } gpus ? GpuInfoService.Primary(gpus)?.Family : null;
        if (family is GpuFamily.NotRtx)
        {
            cog.AddNote(Loc.GetString("Extras.Umfg.Dlss5.NotRtxNote"));
            return;
        }
        var version = Rtx40MfgService.NrVersionFor(family);

        bool on = owned;
        bool suppress = false;
        Microsoft.UI.Xaml.Controls.ComboBox? combo = null;
        void Revert()
        {
            if (combo == null) return;
            suppress = true;
            combo.SelectedIndex = on ? 1 : 0;
            suppress = false;
        }

        async Task ApplyAsync(bool enable)
        {
            if (enable == on) return;
            if (!enable)
            {
                await Task.Run(() => mfgSvc.RemoveNrDll(installPath));
                on = false;
                cog.SetSuccess(others.Count > 0
                    ? Loc.GetString("Extras.Umfg.Dlss5.RemovedShared")
                    : Loc.GetString("Extras.RtxEncore.Nr.Removed"));
                RequestExtrasRebuild(card);
                await RefreshDlssAfterNrDllChangeAsync(card); // #174: NR column / NR section
                return;
            }
            if (combo != null) combo.IsEnabled = false;
            try
            {
                var dlssS = App.Services.GetRequiredService<IDlssStreamlineService>();
                var cached = ResolveCachedNrDllPath(version);
                if (cached == null)
                {
                    cog.SetStatus(Loc.GetString("Extras.RtxEncore.Nr.Downloading", version));
                    cached = await dlssS.EnsureSpecificDlssnrCachedAsync(version);
                }
                if (cached == null)
                {
                    Revert();
                    cog.SetError(Loc.GetString("Extras.Umfg.Dlss5.DownloadFailed", version));
                    return;
                }
                var result = await Task.Run(() => mfgSvc.DeployNrDll(installPath, cached));
                switch (result)
                {
                    case Rtx40MfgService.NrDeployResult.Failed:
                        Revert();
                        cog.SetError(Loc.GetString("Extras.Umfg.Dlss5.CopyFailed"));
                        return;
                    case Rtx40MfgService.NrDeployResult.GamesOwn:
                        Revert();
                        cog.SetError(Loc.GetString("Extras.Umfg.Dlss5.GamesOwnError"));
                        return;
                    case Rtx40MfgService.NrDeployResult.SharedWithOther:
                        cog.SetSuccess(Loc.GetString("Extras.Umfg.Dlss5.SharedSuccess"));
                        break;
                    default:
                        cog.SetSuccess(Loc.GetString("Extras.Umfg.Dlss5.InstalledSuccess", version));
                        break;
                }
                // A game with no DLSS of its own may be in the scan skip cache: un-skip it so the NR DLL shows
                dlssS.RecordDlssFound(card.GameName);
                on = true;
                RequestExtrasRebuild(card);
                await RefreshDlssAfterNrDllChangeAsync(card); // #174: NR column / NR section
            }
            finally
            {
                if (combo != null) combo.IsEnabled = true;
            }
        }

        combo = cog.AddCombo(Loc.GetString("Extras.Umfg.Dlss5.Combo.Label"),
            Loc.GetString("Extras.Umfg.Dlss5.Combo.Help", version,
                family is { } f ? GpuInfoService.FamilyLabel(f) : Loc.GetString("Extras.Umfg.Dlss5.Combo.FallbackCard")),
            new[] { Loc.GetString("Option.No"), Loc.GetString("Option.Yes") }, on ? 1 : 0, i =>
            {
                if (suppress) return true;
                _ = ApplySafeAsync(i == 1);
                return true;
            });

        async Task ApplySafeAsync(bool enable)
        {
            try { await ApplyAsync(enable); }
            catch (Exception ex)
            {
                CrashReporter.Log($"[UmfgCog] NR DLL change failed — {ex.Message}");
                Revert();
                cog.SetError(Loc.GetString("Extras.RtxEncore.Nr.ChangeFailed", ex.Message));
            }
        }
    }
}
