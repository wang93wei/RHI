using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RenoDXCommander.Services;

/// <summary>
/// Manages the RTX 40 MFG Unlock component — staging, install, and uninstall.
/// GitHub: dashdogy/RTX40MFG-Unlock. Deploys a single RTXMFG.dll renamed to a
/// user-chosen proxy DLL name (e.g. version.dll, dinput8.dll).
/// </summary>
public class Rtx40MfgService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/dashdogy/RTX40MFG-Unlock/releases/latest";
    private const string RepoUrl      = "https://github.com/dashdogy/RTX40MFG-Unlock";

    /// <summary>The filename of the staged/zip-extracted DLL.</summary>
    public const string StagedDllName = "RTXMFG.dll";

    /// <summary>Legacy filename — only used for migration detection.</summary>
    public const string AsiFileName   = "RTX40MFG.asi";

    /// <summary>All valid proxy DLL names that RTXMFG.dll may be deployed as.</summary>
    public static readonly string[] KnownProxyNames =
    {
        "version.dll", "dinput8.dll", "winmm.dll", "d3d9.dll", "d3d10.dll",
        "d3d11.dll", "d3d12.dll", "dxgi.dll", "dsound.dll", "wininet.dll",
        "winhttp.dll", "binkw64.dll", "bink2w64.dll", "xinput1_1.dll",
        "xinput1_2.dll", "xinput1_3.dll", "xinput1_4.dll", "xinput9_1_0.dll",
        "xinputuap.dll",
    };

    private readonly HttpClient      _http;
    private readonly ICrashReporter  _crashReporter;
    private readonly GitHubETagCache _etagCache;
    private readonly string          _stagingDir;
    private readonly string          _versionFile;

    public Rtx40MfgService(HttpClient http, ICrashReporter crashReporter, GitHubETagCache etagCache)
    {
        _http          = http;
        _crashReporter = crashReporter;
        _etagCache     = etagCache;
        _stagingDir    = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RHI", "rtx40mfg");
        _versionFile   = Path.Combine(_stagingDir, "version.txt");

        // One-time migration: wipe old ASI-based staging so new RTXMFG.dll will be downloaded
        var legacyAsi = Path.Combine(_stagingDir, AsiFileName);
        if (File.Exists(legacyAsi))
        {
            try
            {
                foreach (var f in Directory.GetFiles(_stagingDir))
                    File.Delete(f);
                _crashReporter.Log("[Rtx40MfgService] Wiped legacy ASI staging — new RTXMFG.dll will be downloaded");
            }
            catch { }
        }
    }

    public string? StagedVersion => File.Exists(_versionFile)
        ? File.ReadAllText(_versionFile).Trim() : null;

    /// <summary>Returns true when RTXMFG.dll is present in the staging directory.</summary>
    public bool IsStagingReady => File.Exists(Path.Combine(_stagingDir, StagedDllName));

    public bool HasUpdate        { get; private set; }
    public string? LatestVersion { get; private set; }

    /// <summary>Returns true if the component is installed in the given game folder (by stored DLL name).</summary>
    public bool IsInstalledIn(string installPath, string? installedAs) =>
        !string.IsNullOrEmpty(installPath) &&
        !string.IsNullOrEmpty(installedAs) &&
        File.Exists(Path.Combine(installPath, installedAs));

    // ── Staging ───────────────────────────────────────────────────────────────

    public async Task EnsureStagingAsync()
    {
        if (IsStagingReady && !HasUpdate)
        {
            _crashReporter.Log("[Rtx40MfgService.EnsureStaging] Already up to date");
            return;
        }

        Directory.CreateDirectory(_stagingDir);

        var (version, downloadUrl) = await FetchLatestReleaseInfoAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(downloadUrl))
        {
            _crashReporter.Log("[Rtx40MfgService.EnsureStaging] Could not resolve latest release");
            return;
        }

        try
        {
            var bytes = await _http.GetByteArrayAsync(downloadUrl).ConfigureAwait(false);
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

            foreach (var entry in zip.Entries)
            {
                var name = Path.GetFileName(entry.FullName);
                if (string.IsNullOrEmpty(name)) continue;

                if (name.Equals(StagedDllName, StringComparison.OrdinalIgnoreCase))
                {
                    var dest = Path.Combine(_stagingDir, StagedDllName);
                    using var src = entry.Open();
                    using var dst = File.Create(dest);
                    await src.CopyToAsync(dst).ConfigureAwait(false);
                    break;
                }
            }

            File.WriteAllText(_versionFile, version);
            HasUpdate = false;
            _crashReporter.Log($"[Rtx40MfgService.EnsureStaging] Staged {version}");
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[Rtx40MfgService.EnsureStaging] Download failed — {ex.Message}");
        }
    }

    public async Task CheckForUpdateAsync(bool forceRefresh = false)
    {
        var (version, _) = await FetchLatestReleaseInfoAsync(forceRefresh).ConfigureAwait(false);
        if (string.IsNullOrEmpty(version)) return;

        LatestVersion = version;
        var current = StagedVersion;
        HasUpdate = !string.Equals(current, version, StringComparison.OrdinalIgnoreCase);
        _crashReporter.Log($"[Rtx40MfgService.CheckForUpdate] Cached={current ?? "(none)"}, Remote={version}, HasUpdate={HasUpdate}");
    }

    private async Task<(string? version, string? downloadUrl)> FetchLatestReleaseInfoAsync(bool forceRefresh = false)
    {
        try
        {
            string? json;
            if (forceRefresh)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
                req.Headers.Add("User-Agent", "RHI");
                var resp = await _http.SendAsync(req).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _crashReporter.Log($"[Rtx40MfgService.FetchRelease] HTTP {(int)resp.StatusCode}");
                    return (null, null);
                }
                json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            else
            {
                json = await _etagCache.GetWithETagAsync(_http, GitHubApiUrl).ConfigureAwait(false);
            }
            if (json == null) return (null, null);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
            if (string.IsNullOrEmpty(tag)) return (null, null);

            if (!root.TryGetProperty("assets", out var assets)) return (null, null);
            foreach (var asset in assets.EnumerateArray())
            {
                var assetName = asset.TryGetProperty("name", out var np) ? np.GetString() : null;
                if (assetName?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true
                    && !assetName.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                {
                    var url = asset.TryGetProperty("browser_download_url", out var up) ? up.GetString() : null;
                    return (tag, url);
                }
            }
            return (tag, null);
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[Rtx40MfgService.FetchRelease] {ex.Message}");
            return (null, null);
        }
    }

    // ── Install ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Deploys RTXMFG.dll to the game folder renamed to <paramref name="dllName"/>.
    /// Writes a sentinel so uninstall knows whether to restore or delete.
    /// </summary>
    public bool Install(string installPath, string dllName)
    {
        if (string.IsNullOrEmpty(installPath) || !IsStagingReady) return false;
        try
        {
            var src  = Path.Combine(_stagingDir, StagedDllName);
            var dest = Path.Combine(installPath, dllName);
            AuxInstallService.SentinelBackup(dest);
            File.Copy(src, dest, overwrite: true);
            _crashReporter.Log($"[Rtx40MfgService.Install] Deployed as '{dllName}' to '{installPath}'");
            return true;
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[Rtx40MfgService.Install] Failed — {ex.Message}");
            return false;
        }
    }

    // ── Uninstall ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the installed DLL using sentinel restore.
    /// Falls back to deleting legacy ASI files if <paramref name="installedAs"/> is null.
    /// </summary>
    public void Uninstall(string installPath, string? installedAs)
    {
        if (string.IsNullOrEmpty(installPath)) return;
        try
        {
            if (!string.IsNullOrEmpty(installedAs))
            {
                AuxInstallService.SentinelRestore(Path.Combine(installPath, installedAs));
            }
            else
            {
                // Legacy fallback: remove the old ASI-based files
                foreach (var legacyFile in new[] { AsiFileName, "RTX40MFGCore.dll", "RTX40MFG-UI.addon64" })
                {
                    var p = Path.Combine(installPath, legacyFile);
                    try { if (File.Exists(p)) File.Delete(p); } catch { }
                }
            }
            _crashReporter.Log($"[Rtx40MfgService.Uninstall] Removed from '{installPath}'");
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[Rtx40MfgService.Uninstall] Failed — {ex.Message}");
        }
    }
}
