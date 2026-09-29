namespace RenoDXCommander.ViewModels;

// Cached NVIDIA driver profile values — populated after the first NVAPI background scan
// and used to render the NVIDIA Profile section immediately on subsequent game selections.
public partial class GameCardViewModel
{
    // Set to true after the first successful NVAPI scan for this card.
    // False = never fetched this session; panel uses null data (all "Default").
    public bool CachedNvidiaProfileValid { get; set; }

    // ── DlssProfileData fields ─────────────────────────────────────────────────
    public bool CachedSrDriverOverride { get; set; }
    public bool CachedRrDriverOverride { get; set; }
    public bool CachedFgDriverOverride { get; set; }
    public bool CachedNrDriverOverride { get; set; }
    public uint CachedSrPreset { get; set; }
    public uint CachedRrPreset { get; set; }
    public uint CachedFgPreset { get; set; }
    public uint CachedNrPreset { get; set; }
    public uint CachedSrRenderScale { get; set; }
    public uint CachedRrRenderScale { get; set; }
    public uint CachedMfgMode { get; set; }

    // ── DriverProfileData fields ───────────────────────────────────────────────
    public uint CachedVSyncMode { get; set; }
    public uint? CachedGlobalVSyncMode { get; set; }
    public uint CachedVSyncTearControl { get; set; }
    public uint CachedLowLatencyMode { get; set; }
    public uint CachedSmoothMotionEnable { get; set; }
    public uint CachedSmoothMotionApis { get; set; }
    public uint CachedSmoothMotionFlipPacingFs { get; set; }
    public uint CachedPowerManagementMode { get; set; }
    public bool CachedPerGameGSyncEnabled { get; set; }
    public ulong CachedReBarSizeLimit { get; set; }
    public uint CachedReBarEnableMode { get; set; }
    public uint CachedReBarMode { get; set; }
    public ulong CachedGlobalReBarSizeLimit { get; set; }

    /// <summary>Updates the cached DlssProfileData fields from a live scan result.</summary>
    public void UpdateCachedDlssProfile(
        bool srDriverOverride, bool rrDriverOverride, bool fgDriverOverride, bool nrDriverOverride,
        uint srPreset, uint rrPreset, uint fgPreset, uint nrPreset,
        uint srRenderScale, uint rrRenderScale, uint mfgMode)
    {
        CachedSrDriverOverride = srDriverOverride;
        CachedRrDriverOverride = rrDriverOverride;
        CachedFgDriverOverride = fgDriverOverride;
        CachedNrDriverOverride = nrDriverOverride;
        CachedSrPreset = srPreset;
        CachedRrPreset = rrPreset;
        CachedFgPreset = fgPreset;
        CachedNrPreset = nrPreset;
        CachedSrRenderScale = srRenderScale;
        CachedRrRenderScale = rrRenderScale;
        CachedMfgMode = mfgMode;
    }

    /// <summary>Updates the cached DriverProfileData fields from a live scan result.</summary>
    public void UpdateCachedDriverProfile(
        uint vSyncMode, uint? globalVSyncMode, uint vSyncTearControl,
        uint lowLatencyMode, uint smoothMotionEnable, uint smoothMotionApis,
        uint smoothMotionFlipPacingFs, uint powerManagementMode,
        bool perGameGSyncEnabled, ulong reBarSizeLimit,
        uint reBarEnableMode, uint reBarMode, ulong globalReBarSizeLimit)
    {
        CachedVSyncMode = vSyncMode;
        CachedGlobalVSyncMode = globalVSyncMode;
        CachedVSyncTearControl = vSyncTearControl;
        CachedLowLatencyMode = lowLatencyMode;
        CachedSmoothMotionEnable = smoothMotionEnable;
        CachedSmoothMotionApis = smoothMotionApis;
        CachedSmoothMotionFlipPacingFs = smoothMotionFlipPacingFs;
        CachedPowerManagementMode = powerManagementMode;
        CachedPerGameGSyncEnabled = perGameGSyncEnabled;
        CachedReBarSizeLimit = reBarSizeLimit;
        CachedReBarEnableMode = reBarEnableMode;
        CachedReBarMode = reBarMode;
        CachedGlobalReBarSizeLimit = globalReBarSizeLimit;
        CachedNvidiaProfileValid = true;
    }
}
