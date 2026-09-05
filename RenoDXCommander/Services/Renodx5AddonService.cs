using System.Text.Json;
using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// Manages the RenoDX DLSS5 addon and the ShortFuse SF variant.
/// Both are staged to %LocalAppData%\RHI\rdx5\.
/// Original: renodx-dlss5.addon64 / version.txt  (renodx-dlss5- tag prefix)
/// SF variant: renodx-dlss.addon64 / version-sf.txt (renodx-dlss-SF- tag prefix)
/// The two are mutually exclusive — installing one removes the other from the game folder.
/// </summary>
public class Renodx5AddonService
{
    // ── Original DLSS5 Tool ───────────────────────────────────────────────────
    private const string StagedFileName   = "renodx-dlss5.addon64";
    private const string DeployFileName   = "renodx-dlss5.addon64";
    private const string TagPrefix        = "renodx-dlss5-";

    // ── ShortFuse SF variant ──────────────────────────────────────────────────
    private const string SfStagedFileName = "renodx-dlss.addon64";
    private const string SfDeployFileName = "renodx-dlss.addon64";
    private const string SfTagPrefix      = "renodx-dlss-SF-";

    private static readonly string GitHubApiUrl =
        "https://api.github.com/repos/RankFTW/rhi-repo/releases?per_page=100";

    private readonly HttpClient _http;
    private readonly ICrashReporter _crashReporter;
    private readonly IGameLibraryService _gameLibraryService;
    private readonly IDlssStreamlineService _dlssStreamlineService;

    private readonly string _stagingDir;
    private readonly string _versionFile;
    private readonly string _sfVersionFile;

    public Renodx5AddonService(
        HttpClient http,
        ICrashReporter crashReporter,
        IGameLibraryService gameLibraryService,
        IDlssStreamlineService dlssStreamlineService)
    {
        _http                  = http;
        _crashReporter         = crashReporter;
        _gameLibraryService    = gameLibraryService;
        _dlssStreamlineService = dlssStreamlineService;

        _stagingDir    = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RHI", "rdx5");
        _versionFile   = Path.Combine(_stagingDir, "version.txt");
        _sfVersionFile = Path.Combine(_stagingDir, "version-sf.txt");
    }

    // ── Original properties ───────────────────────────────────────────────────

    public bool IsStagingReady => File.Exists(Path.Combine(_stagingDir, StagedFileName));
    public string StagedFilePath => Path.Combine(_stagingDir, StagedFileName);
    public string? StagedVersion
        => File.Exists(_versionFile) ? File.ReadAllText(_versionFile).Trim() : null;
    public bool HasUpdate { get; private set; }
    public string? LatestVersion { get; private set; }

    // ── SF properties ─────────────────────────────────────────────────────────

    public bool IsSfStagingReady => File.Exists(Path.Combine(_stagingDir, SfStagedFileName));
    public string SfStagedFilePath => Path.Combine(_stagingDir, SfStagedFileName);
    public string? SfStagedVersion
        => File.Exists(_sfVersionFile) ? File.ReadAllText(_sfVersionFile).Trim() : null;
    public bool SfHasUpdate { get; private set; }
    public string? SfLatestVersion { get; private set; }

    // ── Original public API ───────────────────────────────────────────────────

    public async Task<bool> CheckForUpdateAsync()
    {
        var (version, _) = await FetchLatestReleaseInfoAsync(TagPrefix, StagedFileName, "renodx-dlss5").ConfigureAwait(false);
        if (string.IsNullOrEmpty(version))
        {
            _crashReporter.Log("[Renodx5AddonService.CheckForUpdateAsync] Could not resolve latest version");
            return false;
        }
        LatestVersion = version;
        var current = StagedVersion;
        HasUpdate = !string.Equals(current, version, StringComparison.OrdinalIgnoreCase);
        _crashReporter.Log($"[Renodx5AddonService.CheckForUpdateAsync] Cached={current ?? "(none)"}, Remote={version}, HasUpdate={HasUpdate}");
        return HasUpdate;
    }

    public async Task EnsureStagingAsync(IProgress<(string message, double percent)>? progress = null)
    {
        if (IsStagingReady && !HasUpdate)
        {
            _crashReporter.Log("[Renodx5AddonService.EnsureStagingAsync] Staging already valid — skipping");
            return;
        }
        await DownloadAndStageAsync(TagPrefix, StagedFileName, "renodx-dlss5", _versionFile,
            "RenoDX DLSS5 addon", progress,
            v => { HasUpdate = false; LatestVersion = v; },
            () => AutoRedeployAsync(StagedFileName, DeployFileName, "Renodx5AddonService.AutoRedeployAsync")).ConfigureAwait(false);
    }

    public async Task DeployNrDllIfAbsentAsync(string installPath)
    {
        if (string.IsNullOrEmpty(installPath)) return;
        var nrDllDest = Path.Combine(installPath, "nvngx_dlssnr.dll");
        var sentinel  = nrDllDest + ".original";

        // Sentinel exists → already deployed by RHI, skip
        if (File.Exists(sentinel)) return;
        // File exists without sentinel → game-original, don't touch
        if (File.Exists(nrDllDest)) return;

        try
        {
            var cachedNr = await _dlssStreamlineService.EnsureNewestDlssnrCachedAsync().ConfigureAwait(false);
            if (cachedNr != null)
            {
                File.Copy(cachedNr, nrDllDest, overwrite: false);
                File.WriteAllBytes(sentinel, Array.Empty<byte>()); // 0-byte sentinel — RHI placed this
                _crashReporter.Log($"[Renodx5AddonService.DeployNrDllIfAbsentAsync] Deployed nvngx_dlssnr.dll to '{installPath}' (sentinel written)");
            }
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[Renodx5AddonService.DeployNrDllIfAbsentAsync] Failed for '{installPath}' — {ex.Message}");
        }
    }

    public async Task InstallAsync(string installPath)
    {
        if (string.IsNullOrEmpty(installPath)) return;
        await EnsureStagingAsync().ConfigureAwait(false);
        if (!IsStagingReady) { _crashReporter.Log("[Renodx5AddonService.InstallAsync] Staging not ready"); return; }

        try
        {
            var deployDir = ModInstallService.GetAddonDeployPath(installPath);
            Directory.CreateDirectory(deployDir);
            File.Copy(StagedFilePath, Path.Combine(deployDir, DeployFileName), overwrite: true);
            _crashReporter.Log($"[Renodx5AddonService.InstallAsync] Deployed to '{deployDir}'");
            // Mutual exclusivity — remove SF variant if present
            RemoveSfAddonFromFolder(deployDir, installPath);
        }
        catch (Exception ex) { _crashReporter.Log($"[Renodx5AddonService.InstallAsync] Failed — {ex.Message}"); }

        await DeployNrDllIfAbsentAsync(installPath).ConfigureAwait(false);
    }

    public void Uninstall(string installPath)
    {
        if (string.IsNullOrEmpty(installPath)) return;
        var deployDir = ModInstallService.GetAddonDeployPath(installPath);
        TryDelete(Path.Combine(deployDir, DeployFileName), "Renodx5AddonService.Uninstall");
        // Use sentinel pattern to decide whether to remove nvngx_dlssnr.dll:
        // 0-byte .original → RHI placed it, delete both
        // non-zero .original → game-original, restore it
        // no .original → game-original (pre-sentinel installs or game shipped it), leave alone
        var nrDllPath = Path.Combine(installPath, "nvngx_dlssnr.dll");
        var sentinel  = nrDllPath + ".original";
        if (File.Exists(sentinel))
        {
            var info = new FileInfo(sentinel);
            if (info.Length == 0)
            {
                // Sentinel — RHI placed nvngx_dlssnr.dll, game had nothing → delete both
                TryDelete(nrDllPath, "Renodx5AddonService.Uninstall (sentinel cleanup)");
                TryDelete(sentinel,  "Renodx5AddonService.Uninstall (sentinel delete)");
            }
            else
            {
                // Non-zero backup — game had its own copy → restore it
                try { File.Copy(sentinel, nrDllPath, overwrite: true); File.Delete(sentinel); }
                catch (Exception ex) { _crashReporter.Log($"[Renodx5AddonService.Uninstall] Restore failed — {ex.Message}"); }
            }
        }
        else
        {
            _crashReporter.Log($"[Renodx5AddonService.Uninstall] No sentinel for nvngx_dlssnr.dll — leaving untouched");
        }
    }

    public bool IsInstalledIn(string installPath)
    {
        if (string.IsNullOrEmpty(installPath)) return false;
        return File.Exists(Path.Combine(ModInstallService.GetAddonDeployPath(installPath), DeployFileName));
    }

    /// <summary>
    /// Removes nvngx_dlssnr.dll from the game folder using the sentinel pattern.
    /// Called when DLSS5 Tool addon is removed (e.g. switching back to Global addons).
    /// </summary>
    public void RemoveNrDll(string installPath)
    {
        if (string.IsNullOrEmpty(installPath)) return;
        var nrDllPath = Path.Combine(installPath, "nvngx_dlssnr.dll");
        var sentinel  = nrDllPath + ".original";
        if (!File.Exists(sentinel))
        {
            _crashReporter.Log($"[Renodx5AddonService.RemoveNrDll] No sentinel — leaving nvngx_dlssnr.dll untouched");
            return;
        }
        var info = new FileInfo(sentinel);
        if (info.Length == 0)
        {
            TryDelete(nrDllPath, "Renodx5AddonService.RemoveNrDll (sentinel cleanup)");
            TryDelete(sentinel,  "Renodx5AddonService.RemoveNrDll (sentinel delete)");
        }
        else
        {
            try { File.Copy(sentinel, nrDllPath, overwrite: true); File.Delete(sentinel); }
            catch (Exception ex) { _crashReporter.Log($"[Renodx5AddonService.RemoveNrDll] Restore failed — {ex.Message}"); }
        }
    }

    // ── SF public API ─────────────────────────────────────────────────────────

    public async Task<bool> CheckForSfUpdateAsync()
    {
        var (version, _) = await FetchLatestReleaseInfoAsync(SfTagPrefix, SfStagedFileName, "renodx-dlss").ConfigureAwait(false);
        if (string.IsNullOrEmpty(version))
        {
            _crashReporter.Log("[Renodx5AddonService.CheckForSfUpdateAsync] Could not resolve latest version");
            return false;
        }
        SfLatestVersion = version;
        var current = SfStagedVersion;
        SfHasUpdate = !string.Equals(current, version, StringComparison.OrdinalIgnoreCase);
        _crashReporter.Log($"[Renodx5AddonService.CheckForSfUpdateAsync] Cached={current ?? "(none)"}, Remote={version}, HasUpdate={SfHasUpdate}");
        return SfHasUpdate;
    }

    public async Task EnsureSfStagingAsync(IProgress<(string message, double percent)>? progress = null)
    {
        if (IsSfStagingReady && !SfHasUpdate)
        {
            _crashReporter.Log("[Renodx5AddonService.EnsureSfStagingAsync] Staging already valid — skipping");
            return;
        }
        await DownloadAndStageAsync(SfTagPrefix, SfStagedFileName, "renodx-dlss", _sfVersionFile,
            "DLSS Tool (ShortFuse)", progress,
            v => { SfHasUpdate = false; SfLatestVersion = v; },
            () => AutoRedeployAsync(SfStagedFileName, SfDeployFileName, "Renodx5AddonService.SfAutoRedeployAsync")).ConfigureAwait(false);
    }

    /// <summary>
    /// Installs the SF variant addon and co-deploys DLSS SR/RR/FG/NR + Streamline DLLs
    /// using the sentinel .original pattern for clean uninstall.
    /// </summary>
    public async Task InstallSfAsync(string installPath, DlssDetectionResult? detection = null)
    {
        if (string.IsNullOrEmpty(installPath)) return;
        await EnsureSfStagingAsync().ConfigureAwait(false);
        if (!IsSfStagingReady) { _crashReporter.Log("[Renodx5AddonService.InstallSfAsync] Staging not ready"); return; }

        try
        {
            var deployDir = ModInstallService.GetAddonDeployPath(installPath);
            Directory.CreateDirectory(deployDir);
            File.Copy(SfStagedFilePath, Path.Combine(deployDir, SfDeployFileName), overwrite: true);
            _crashReporter.Log($"[Renodx5AddonService.InstallSfAsync] Deployed SF addon to '{deployDir}'");
            // Mutual exclusivity — remove original DLSS5 Tool if present
            RemoveOriginalAddonFromFolder(deployDir, installPath);
        }
        catch (Exception ex) { _crashReporter.Log($"[Renodx5AddonService.InstallSfAsync] Addon deploy failed — {ex.Message}"); }

        // Co-deploy DLSS/Streamline files using sentinel .original pattern
        await DeploySfDllsAsync(installPath, detection).ConfigureAwait(false);
    }

    public void UninstallSf(string installPath, DlssDetectionResult? detection = null)
    {
        if (string.IsNullOrEmpty(installPath)) return;
        var deployDir = ModInstallService.GetAddonDeployPath(installPath);
        TryDelete(Path.Combine(deployDir, SfDeployFileName), "Renodx5AddonService.UninstallSf");

        // Restore co-deployed DLLs using .original sentinel pattern
        RestoreSfDlls(installPath, detection);
    }

    public bool IsSfInstalledIn(string installPath)
    {
        if (string.IsNullOrEmpty(installPath)) return false;
        return File.Exists(Path.Combine(ModInstallService.GetAddonDeployPath(installPath), SfDeployFileName));
    }

    // ── SF DLL co-deploy / restore (sentinel .original pattern) ──────────────

    private async Task DeploySfDllsAsync(string installPath, DlssDetectionResult? detection)
    {
        // Resolve each DLL's destination — use detected path if present, otherwise install root
        var dllMappings = new[]
        {
            ("nvngx_dlss.dll",  detection?.DlssPath),
            ("nvngx_dlssd.dll", detection?.DlssdPath),
            ("nvngx_dlssg.dll", detection?.DlssgPath),
            ("nvngx_dlssnr.dll", detection?.DlssnrPath),
        };

        // Fetch newest cached paths
        var cachedSr  = await _dlssStreamlineService.EnsureNewestDlssCachedAsync().ConfigureAwait(false);
        var cachedRr  = await _dlssStreamlineService.EnsureNewestDlssdCachedAsync().ConfigureAwait(false);
        var cachedFg  = await _dlssStreamlineService.EnsureNewestDlssgCachedAsync().ConfigureAwait(false);
        var cachedNr  = await _dlssStreamlineService.EnsureNewestDlssnrCachedAsync().ConfigureAwait(false);
        var cachedSlDir = await _dlssStreamlineService.EnsureNewestStreamlineCachedAsync().ConfigureAwait(false);

        var sources = new[] { cachedSr, cachedRr, cachedFg, cachedNr };

        for (int i = 0; i < dllMappings.Length; i++)
        {
            var (dllName, detectedPath) = dllMappings[i];
            var src = sources[i];
            if (src == null) continue;

            var dest = detectedPath ?? Path.Combine(installPath, dllName);
            SentinelDeploy(src, dest, $"[Renodx5AddonService.DeploySfDlls] {dllName}");
        }

        // Streamline — deploy DLLs to detected folder or install root
        if (cachedSlDir != null)
        {
            var slDest = !string.IsNullOrEmpty(detection?.StreamlineFolder)
                ? detection!.StreamlineFolder
                : installPath;

            foreach (var slDll in DlssStreamlineService.KnownStreamlineDlls)
            {
                var srcPath = Path.Combine(cachedSlDir, slDll);
                if (!File.Exists(srcPath)) continue;
                var destPath = Path.Combine(slDest, slDll);
                SentinelDeploy(srcPath, destPath, $"[Renodx5AddonService.DeploySfDlls] {slDll}");
            }
        }
    }

    private void RestoreSfDlls(string installPath, DlssDetectionResult? detection)
    {
        var dllNames = new[] { "nvngx_dlss.dll", "nvngx_dlssd.dll", "nvngx_dlssg.dll", "nvngx_dlssnr.dll" };
        var detectedPaths = new[]
        {
            detection?.DlssPath, detection?.DlssdPath, detection?.DlssgPath, detection?.DlssnrPath
        };

        for (int i = 0; i < dllNames.Length; i++)
        {
            var dest = detectedPaths[i] ?? Path.Combine(installPath, dllNames[i]);
            SentinelRestore(dest, $"[Renodx5AddonService.RestoreSfDlls] {dllNames[i]}");
        }

        // Streamline DLLs
        var slFolder = !string.IsNullOrEmpty(detection?.StreamlineFolder)
            ? detection!.StreamlineFolder
            : installPath;
        foreach (var slDll in DlssStreamlineService.KnownStreamlineDlls)
        {
            SentinelRestore(Path.Combine(slFolder, slDll), $"[Renodx5AddonService.RestoreSfDlls] {slDll}");
        }
    }

    /// <summary>
    /// Deploys src to dest using the sentinel .original pattern.
    /// - Dest exists, no .original → backup dest as .original, overwrite
    /// - Dest exists, .original exists → overwrite only
    /// - Dest does not exist → copy src to dest, write 0-byte .original sentinel
    /// </summary>
    private void SentinelDeploy(string src, string dest, string logPrefix)
    {
        try
        {
            var backup = dest + ".original";
            if (File.Exists(dest))
            {
                if (!File.Exists(backup))
                    File.Copy(dest, backup); // preserve game original
                File.Copy(src, dest, overwrite: true);
                _crashReporter.Log($"{logPrefix} → updated '{dest}'");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(src, dest);
                File.WriteAllBytes(backup, Array.Empty<byte>()); // sentinel — RHI placed this
                _crashReporter.Log($"{logPrefix} → placed '{dest}' (no prior file, sentinel written)");
            }
        }
        catch (Exception ex) { _crashReporter.Log($"{logPrefix} Deploy failed for '{dest}' — {ex.Message}"); }
    }

    /// <summary>
    /// Restores or cleans up a sentinel-deployed file.
    /// - .original is non-zero → restore game original
    /// - .original is 0-byte sentinel → delete both
    /// - No .original → leave untouched
    /// </summary>
    private void SentinelRestore(string dest, string logPrefix)
    {
        try
        {
            var backup = dest + ".original";
            if (!File.Exists(backup)) return; // no record of placing it — leave alone
            var info = new FileInfo(backup);
            if (info.Length == 0)
            {
                // Sentinel — RHI placed this file, game had nothing
                TryDelete(dest, logPrefix + " (sentinel cleanup)");
                TryDelete(backup, logPrefix + " (sentinel delete)");
            }
            else
            {
                // Real backup — restore game original
                TryDelete(dest, logPrefix + " (pre-restore delete)");
                File.Move(backup, dest);
                _crashReporter.Log($"{logPrefix} → restored '{dest}'");
            }
        }
        catch (Exception ex) { _crashReporter.Log($"{logPrefix} Restore failed for '{dest}' — {ex.Message}"); }
    }

    // ── Shared private helpers ────────────────────────────────────────────────

    private void RemoveSfAddonFromFolder(string deployDir, string installPath)
    {
        TryDelete(Path.Combine(deployDir, SfDeployFileName), "[Renodx5AddonService] mutual exclusivity remove SF");
        if (!deployDir.Equals(installPath, StringComparison.OrdinalIgnoreCase))
            TryDelete(Path.Combine(installPath, SfDeployFileName), "[Renodx5AddonService] mutual exclusivity remove SF (root)");
    }

    private void RemoveOriginalAddonFromFolder(string deployDir, string installPath)
    {
        TryDelete(Path.Combine(deployDir, DeployFileName), "[Renodx5AddonService] mutual exclusivity remove original");
        if (!deployDir.Equals(installPath, StringComparison.OrdinalIgnoreCase))
            TryDelete(Path.Combine(installPath, DeployFileName), "[Renodx5AddonService] mutual exclusivity remove original (root)");
    }

    private void TryDelete(string path, string logPrefix)
    {
        try { if (File.Exists(path)) { File.Delete(path); _crashReporter.Log($"{logPrefix} deleted '{path}'"); } }
        catch (Exception ex) { _crashReporter.Log($"{logPrefix} delete failed '{path}' — {ex.Message}"); }
    }

    private async Task AutoRedeployAsync(string stagedFileName, string deployFileName, string logCtx)
    {
        try
        {
            var staged = Path.Combine(_stagingDir, stagedFileName);
            if (!File.Exists(staged)) return;
            var lib = _gameLibraryService.Load();
            if (lib == null) return;

            foreach (var game in lib.Games.Concat(lib.ManualGames).Where(g => !string.IsNullOrEmpty(g.InstallPath)))
            {
                try
                {
                    var deployDir = ModInstallService.GetAddonDeployPath(game.InstallPath!);
                    var dest      = Path.Combine(deployDir, deployFileName);
                    var destRoot  = Path.Combine(game.InstallPath!, deployFileName);
                    bool inDeploy = File.Exists(dest);
                    bool inRoot   = !dest.Equals(destRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(destRoot);

                    // Only redeploy if the file exists AND is tracked in addon_deployments.json
                    // This prevents re-adding files the user intentionally removed
                    if (inDeploy)
                    {
                        bool tracked = AddonPackService.IsAddonTrackedInDeployments(deployDir, deployFileName)
                                    || AddonPackService.IsAddonTrackedInDeployments(game.InstallPath!, deployFileName);
                        if (!tracked) { inDeploy = false; }
                    }
                    if (inRoot)
                    {
                        bool tracked = AddonPackService.IsAddonTrackedInDeployments(game.InstallPath!, deployFileName);
                        if (!tracked) { inRoot = false; }
                    }

                    if (!inDeploy && !inRoot) continue;
                    if (inDeploy) { File.Copy(staged, dest, overwrite: true); _crashReporter.Log($"[{logCtx}] Updated '{game.Name}' at '{deployDir}'"); }
                    if (inRoot)   { File.Copy(staged, destRoot, overwrite: true); _crashReporter.Log($"[{logCtx}] Updated '{game.Name}' at root"); }
                }
                catch (Exception ex) { _crashReporter.Log($"[{logCtx}] Failed for '{game.Name}' — {ex.Message}"); }
            }
        }
        catch (Exception ex) { _crashReporter.Log($"[{logCtx}] Loop failed — {ex.Message}"); }
        await Task.CompletedTask;
    }

    private async Task DownloadAndStageAsync(
        string tagPrefix, string stagedFileName, string zipPrefix, string versionFile,
        string displayName, IProgress<(string, double)>? progress,
        Action<string> onComplete, Func<Task> autoRedeploy)
    {
        Directory.CreateDirectory(_stagingDir);
        progress?.Report(($"Downloading {displayName}...", 10));

        var (version, downloadUrl) = await FetchLatestReleaseInfoAsync(tagPrefix, stagedFileName, zipPrefix).ConfigureAwait(false);
        if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(downloadUrl))
        {
            _crashReporter.Log($"[Renodx5AddonService.DownloadAndStageAsync] Could not resolve latest release for '{tagPrefix}'");
            return;
        }

        progress?.Report(($"Downloading {displayName}...", 30));

        try
        {
            var destPath = Path.Combine(_stagingDir, stagedFileName);
            var bytes    = await _http.GetByteArrayAsync(downloadUrl).ConfigureAwait(false);

            if (downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var tempZip = Path.Combine(_stagingDir, "_dl_tmp.zip");
                await File.WriteAllBytesAsync(tempZip, bytes).ConfigureAwait(false);
                using (var zip = System.IO.Compression.ZipFile.OpenRead(tempZip))
                {
                    var entry = zip.Entries.FirstOrDefault(e =>
                        string.Equals(e.Name, stagedFileName, StringComparison.OrdinalIgnoreCase));
                    if (entry == null)
                    {
                        _crashReporter.Log($"[Renodx5AddonService] '{stagedFileName}' not found in zip");
                        File.Delete(tempZip);
                        return;
                    }
                    using var es = entry.Open();
                    using var os = File.Create(destPath);
                    await es.CopyToAsync(os).ConfigureAwait(false);
                }
                File.Delete(tempZip);
            }
            else
            {
                await File.WriteAllBytesAsync(destPath, bytes).ConfigureAwait(false);
            }

            File.WriteAllText(versionFile, version);
            onComplete(version);
            _crashReporter.Log($"[Renodx5AddonService.DownloadAndStageAsync] Staged {displayName} v{version} ({new FileInfo(destPath).Length} bytes)");
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[Renodx5AddonService.DownloadAndStageAsync] Download failed for '{tagPrefix}' — {ex.Message}");
            progress?.Report(($"{displayName} download failed: {ex.Message}", 0));
            return;
        }

        progress?.Report(($"{displayName} ready", 90));
        await autoRedeploy().ConfigureAwait(false);
        progress?.Report(($"{displayName} ready", 100));
    }

    private async Task<(string? version, string? downloadUrl)> FetchLatestReleaseInfoAsync(
        string tagPrefix, string stagedFileName, string zipPrefix)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.Add("User-Agent", "RHI");
            request.Headers.Add("Accept", "application/vnd.github+json");

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _crashReporter.Log($"[Renodx5AddonService] GitHub API returned {response.StatusCode}");
                return (null, null);
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var candidates = new List<(string version, string downloadUrl, Version parsed)>();

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (!release.TryGetProperty("tag_name", out var tagEl)) continue;
                var tag = tagEl.GetString();
                if (tag == null || !tag.StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                var version = tag.Substring(tagPrefix.Length);

                string? downloadUrl = null;
                if (release.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var assetName = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                        if (assetName == null) continue;

                        bool isAddon = string.Equals(assetName, stagedFileName, StringComparison.OrdinalIgnoreCase);
                        bool isZip   = assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                    && assetName.StartsWith(zipPrefix, StringComparison.OrdinalIgnoreCase);

                        if ((isAddon || isZip) && asset.TryGetProperty("browser_download_url", out var urlEl))
                        {
                            downloadUrl = urlEl.GetString();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl)) continue;

                candidates.Add(Version.TryParse(version, out var parsed)
                    ? (version, downloadUrl!, parsed)
                    : (version, downloadUrl!, new Version(0, 0)));
            }

            if (candidates.Count == 0)
            {
                _crashReporter.Log($"[Renodx5AddonService] No release found with tag prefix '{tagPrefix}'");
                return (null, null);
            }

            var best = candidates.OrderByDescending(c => c.parsed).First();
            return (best.version, best.downloadUrl);
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[Renodx5AddonService] FetchLatestReleaseInfo failed — {ex.Message}");
            return (null, null);
        }
    }
}
