// DetailPanelBuilder.Extras.MfgUnlockDlss5.cs — MFG Unlock ⚙ › DLSS 5 (MFG Unlock 1.12.0+): NVIDIA's DLSS 5 neural
// rendering (NR) on the game's frames in DirectX 12 and Vulkan games, off by default. The mod's settings are under
// [DLSS5] in MFGAdaUnlock.ini beside the game's exe (docs/settings.md): Enabled=0/1 is the switch, the rest (look,
// resolution, passes, hotkeys) is in the mod's DLSS 5 tab. The mod doesn't ship or download nvngx_dlssnr.dll: it
// must be beside the exe, so turning DLSS 5 on here also installs RHI's copy (owner "MfgUnlock" in rhi_install.txt,
// Rtx40MfgService.Dlss5.cs) unless one is already there; turning it off or removing the mod drops that claim.
// With another DLSS 5 add-on loaded the mod's NR stands aside ([DLSS5] Coexist=0, the default).

using Microsoft.Extensions.DependencyInjection;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private void AddMfgUnlockDlss5Section(CogDialog cog, GameCardViewModel card, string iniPath, HashSet<GraphicsApiType> apis)
    {
        var installPath = card.InstallPath ?? "";
        const string Owner = Rtx40MfgService.MfgUnlockNrOwnerName;
        cog.AddSection("DLSS 5");
        cog.AddIntro(Loc.GetString("Extras.MfgDlss5.Intro"));
        if (string.IsNullOrEmpty(installPath)) return;

        // DirectX 11 and older aren't supported (an unknown API is let through)
        bool apiOk = apis.Contains(GraphicsApiType.DirectX12) || apis.Contains(GraphicsApiType.Vulkan)
                     || apis.All(a => a == GraphicsApiType.Unknown);
        var family = GpuInfoService.Cached is { } gpus ? GpuInfoService.Primary(gpus)?.Family : null;
        bool isInstalled = card.MfgAdaInstalled;

        // The NR DLL beside the exe
        var mfgSvc = App.Services.GetRequiredService<Rtx40MfgService>();
        var dest = Path.Combine(installPath, Rtx40MfgService.NrDllName);
        bool present = File.Exists(dest);
        bool gamesOwn = present && !File.Exists(dest + ".original");
        var owners = present
            ? Models.RhiInstallManifest.GetSharedFileOwners(installPath, Rtx40MfgService.NrDllName).ToList()
            : new List<string>();
        string? ver = null;
        if (present)
        {
            try
            {
                var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(DlssNrCostScalerService.ResolveRealNrDllPath(dest));
                ver = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { CrashReporter.Log($"[MfgAdaCog] Couldn't read the NR DLL version — {ex.Message}"); }
        }
        string verText = ver != null ? $" ({ver})" : "";

        if (!apiOk)
        {
            cog.AddNote(Loc.GetString("Extras.MfgDlss5.Note.ApiUnsupported", ApiDisplayName(card.GraphicsApi, apis)));
            return;
        }
        if (family is GpuFamily.NotRtx)
        {
            cog.AddNote(Loc.GetString("Extras.MfgDlss5.Note.NoRtx"));
            return;
        }
        if (!isInstalled)
        {
            cog.AddNote(Loc.GetString("Extras.MfgDlss5.Note.InstallFirst"));
            return;
        }

        cog.AddNote(!present ? Loc.GetString("Extras.MfgDlss5.Note.DllNotPresent")
            : gamesOwn ? $"nvngx_dlssnr.dll{verText}" + Loc.GetString("Extras.MfgDlss5.Note.DllOwn")
            : owners.Count > 0 ? $"nvngx_dlssnr.dll{verText}" + Loc.GetString("Extras.MfgDlss5.Note.DllInstalledBy", string.Join(", ", owners))
            : $"nvngx_dlssnr.dll{verText}" + Loc.GetString("Extras.MfgDlss5.Note.DllInstalled"));

        // Another DLSS 5 add-on in the game: MFG Unlock's NR stands aside while it's loaded
        var rdx5Svc = App.Services.GetRequiredService<Renodx5AddonService>();
        var otherAddons = new List<string>();
        try
        {
            if (rdx5Svc.IsInstalledIn(installPath)) otherAddons.Add("DLSS5 Tool");
            if (rdx5Svc.IsSfInstalledIn(installPath)) otherAddons.Add("ShortFuse's DLSS 5");
            if (File.Exists(Path.Combine(installPath, FeederDeployFile64))) otherAddons.Add("DLSS5 Feeder");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { CrashReporter.Log($"[MfgAdaCog] DLSS 5 add-on check failed — {ex.Message}"); }

        bool enabled = Dlssg20_30Service.ReadIniValue(iniPath, "DLSS5", "Enabled") == "1";
        var version = Rtx40MfgService.NrVersionFor(family);
        // On in the ini but the DLL is gone (deleted by antivirus or another tool): show Off, so On installs it again
        bool on = enabled && present;
        if (enabled && !present)
            cog.AddWarning(Loc.GetString("Extras.MfgDlss5.Warning.DllMissing"));
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
                Dlssg20_30Service.WriteIniValue(iniPath, "DLSS5", "Enabled", "0");
                await Task.Run(() => mfgSvc.RemoveNrDll(installPath, Owner));
                on = false;
                CrashReporter.Log($"[MfgAdaCog] '{card.GameName}': DLSS 5 = Off");
                cog.SetSuccess(Loc.GetString("Extras.MfgDlss5.Success.Off"));
                RequestExtrasRebuild(card);
                await RefreshDlssAfterNrDllChangeAsync(card);
                return;
            }
            if (combo != null) combo.IsEnabled = false;
            try
            {
                if (!gamesOwn)
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
                        cog.SetError(Loc.GetString("Extras.MfgDlss5.Error.DownloadFailed", version));
                        return;
                    }
                    var result = await Task.Run(() => mfgSvc.DeployNrDll(installPath, cached, Owner));
                    if (result == Rtx40MfgService.NrDeployResult.Failed)
                    {
                        Revert();
                        cog.SetError(Loc.GetString("Extras.MfgDlss5.Error.CopyFailed"));
                        return;
                    }
                    // A game with no DLSS of its own may be in the scan skip cache: un-skip it so the NR DLL shows
                    dlssS.RecordDlssFound(card.GameName);
                }
                Dlssg20_30Service.WriteIniValue(iniPath, "DLSS5", "Enabled", "1");
                on = true;
                CrashReporter.Log($"[MfgAdaCog] '{card.GameName}': DLSS 5 = On");
                cog.SetSuccess(Loc.GetString("Extras.MfgDlss5.Success.On"));
                RequestExtrasRebuild(card);
                await RefreshDlssAfterNrDllChangeAsync(card);
            }
            finally
            {
                if (combo != null) combo.IsEnabled = true;
            }
        }

        async Task ApplySafeAsync(bool enable)
        {
            try { await ApplyAsync(enable); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or System.Net.Http.HttpRequestException or TaskCanceledException)
            {
                CrashReporter.Log($"[MfgAdaCog] DLSS 5 change failed — {ex.Message}");
                Revert();
                cog.SetError(Loc.GetString("Extras.MfgDlss5.Error.ChangeFailed", ex.Message));
            }
        }

        combo = cog.AddCombo(Loc.GetString("Extras.MfgDlss5.Enable.Label"),
            Loc.GetString("Extras.MfgDlss5.Enable.Desc", version),
            new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Xaml.On") }, on ? 1 : 0, i =>
            {
                if (suppress) return true;
                _ = ApplySafeAsync(i == 1);
                return true;
            });

        if (otherAddons.Count > 0)
            cog.AddWarning(Loc.GetString("Extras.MfgDlss5.Warning.OtherAddons", string.Join(Loc.GetString("Extras.MfgDlss5.AddonJoin"), otherAddons)));
        if (apis.Contains(GraphicsApiType.Vulkan) && !apis.Contains(GraphicsApiType.DirectX12))
            cog.AddNote(Loc.GetString("Extras.MfgDlss5.Note.VulkanRestart"));
    }
}
