using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.ViewModels;

// UI state: IsSelected, CardHighlighted, ComponentExpanded, sidebar props, visibility, display
public partial class GameCardViewModel
{
    // ── Localization plumbing ─────────────────────────────────────────────────────
    // Cached localization service — cards are per-game instances, so avoid a DI
    // lookup per property read. Null when DI is unavailable (unit tests); callers
    // fall back to the key itself in that case.
    private static ILocalizationService? _locService;
    private static bool _locResolved;
    private static readonly object _locLock = new();

    private static ILocalizationService? LocService
    {
        get
        {
            if (_locResolved) return _locService;
            lock (_locLock)
            {
                if (_locResolved) return _locService;
                try
                {
                    _locService = App.Services?.GetService(typeof(ILocalizationService)) as ILocalizationService;
                }
                catch
                {
                    _locService = null; // DI not available (unit tests) — labels fall back to keys
                }
                _locResolved = true;
                return _locService;
            }
        }
    }

    /// <summary>Localized string lookup; returns the key when the service is unavailable.</summary>
    private static string Tr(string key) => LocService?.GetString(key) ?? key;

    /// <summary>Localized format lookup; returns the key when the service is unavailable.</summary>
    private static string Tr(string key, params object[] args) => LocService?.GetString(key, args) ?? key;

    // ── Live language refresh ─────────────────────────────────────────────────────
    // One card instance exists per game; subscribing each instance directly to the
    // singleton LanguageChanged event would leak (the service outlives every card).
    // A single static subscription forwards the event to live cards via weak refs.
    private static readonly object _langLock = new();
    private static readonly List<WeakReference<GameCardViewModel>> _langTargets = new();
    private static bool _langHooked;

    public GameCardViewModel()
    {
        RegisterForLanguageChanges();
    }

    private void RegisterForLanguageChanges()
    {
        try
        {
            lock (_langLock)
            {
                _langTargets.Add(new WeakReference<GameCardViewModel>(this));
                // Occasionally prune collected cards so the list stays bounded
                // between language changes (cards are recreated on every scan).
                if (_langTargets.Count > 512)
                {
                    for (var i = _langTargets.Count - 1; i >= 0; i--)
                        if (!_langTargets[i].TryGetTarget(out _)) _langTargets.RemoveAt(i);
                }
                if (_langHooked) return;
                if (LocService is { } loc)
                {
                    loc.LanguageChanged += OnLanguageChangedBroadcast;
                    _langHooked = true;
                }
            }
        }
        catch
        {
            // DI unavailable (unit tests) — live refresh is not needed there.
        }
    }

    private static void OnLanguageChangedBroadcast(object? sender, string language)
    {
        GameCardViewModel[] alive;
        lock (_langLock)
        {
            var list = new List<GameCardViewModel>(_langTargets.Count);
            for (var i = _langTargets.Count - 1; i >= 0; i--)
            {
                if (_langTargets[i].TryGetTarget(out var vm)) list.Add(vm);
                else _langTargets.RemoveAt(i); // prune collected cards
            }
            alive = list.ToArray();
        }
        foreach (var vm in alive)
            vm.NotifyLanguageChanged();
    }

    /// <summary>Raises PropertyChanged for every localized computed label so the UI re-reads them.</summary>
    private void NotifyLanguageChanged()
    {
        // Card grid / sidebar
        OnPropertyChanged(nameof(CardPrimaryActionLabel));
        OnPropertyChanged(nameof(HideButtonLabel));
        OnPropertyChanged(nameof(WikiStatusLabel));
        // RenoDX row
        OnPropertyChanged(nameof(InstallActionLabel));
        OnPropertyChanged(nameof(GenericModLabel));
        OnPropertyChanged(nameof(UeExtendedLabel));
        OnPropertyChanged(nameof(CombinedActionLabel));
        OnPropertyChanged(nameof(RdxStatusText));
        OnPropertyChanged(nameof(RdxShortAction));
        OnPropertyChanged(nameof(ExternalDisplayLabel));
        // ReShade row
        OnPropertyChanged(nameof(RsActionLabel));
        OnPropertyChanged(nameof(RsStatusText));
        OnPropertyChanged(nameof(RsShortAction));
        // Luma row
        OnPropertyChanged(nameof(LumaActionLabel));
        OnPropertyChanged(nameof(LumaStatusText));
        OnPropertyChanged(nameof(LumaShortAction));
        OnPropertyChanged(nameof(LumaBadgeLabel));
        // Frame limiters / OptiScaler / DXVK / RE Framework / DOF Fix
        OnPropertyChanged(nameof(UlActionLabel));
        OnPropertyChanged(nameof(UlStatusText));
        OnPropertyChanged(nameof(UlShortAction));
        OnPropertyChanged(nameof(DcActionLabel));
        OnPropertyChanged(nameof(DcStatusText));
        OnPropertyChanged(nameof(DcShortAction));
        OnPropertyChanged(nameof(OsActionLabel));
        OnPropertyChanged(nameof(OsStatusText));
        OnPropertyChanged(nameof(OsShortAction));
        OnPropertyChanged(nameof(DxvkActionLabel));
        OnPropertyChanged(nameof(DxvkStatusText));
        OnPropertyChanged(nameof(DxvkShortAction));
        OnPropertyChanged(nameof(DxvkToggleTooltip));
        OnPropertyChanged(nameof(RefActionLabel));
        OnPropertyChanged(nameof(RefStatusText));
        OnPropertyChanged(nameof(RefShortAction));
        OnPropertyChanged(nameof(DofFixActionLabel));
        OnPropertyChanged(nameof(DofFixStatusText));
    }

    // ── Sidebar item styling (computed from IsSelected + managed state) ────────────
    public string SidebarItemBackground => IsRunning ? "#1A3A20" : IsSelected ? "#1A2840" : "Transparent";
    public string SidebarItemBorderBrush => IsRunning ? "#2A5A30" : IsSelected ? "#2A4060" : "Transparent";
    public string SidebarItemForeground => IsSelected ? "#E2E8FF"
        : IsManaged ? "#C8D4E8"   // brighter — something is installed
        : "#5A6880";              // dimmer — untouched game

    // Card highlight styling (computed from CardHighlighted)
    public string CardBackground => CardHighlighted ? "#1A2840" : "#141820";
    public string CardBorderBrush => CardHighlighted ? "#2A4060" : "#1E2430";

    // ── Card grid: component status dot colors ────────────────────────────────────
    private static string StatusDotColor(GameStatus s, bool installing) =>
        installing   ? "#2196F3"
        : s == GameStatus.Installed       ? "#4CAF50"
        : s == GameStatus.UpdateAvailable ? "#FF9800"
        : "#5A6880";

    public string CardRdxStatusDot  => StatusDotColor(Status, IsInstalling);
    public string CardRsStatusDot   => RequiresVulkanInstall
        ? (RsIsInstalling ? "#2196F3" : IsLayerInstalledFunc() ? "#4CAF50" : "#5A6880")
        : StatusDotColor(RsStatus, RsIsInstalling);
    public string CardLumaStatusDot => StatusDotColor(LumaStatus, IsLumaInstalling);

    /// <summary>True when the Luma status dot should be visible on the card grid.</summary>
    public bool CardLumaVisible => LumaFeatureEnabled && LumaMod != null && IsLumaInstalled;

    // ── Card grid: action and info properties ─────────────────────────────────────
    /// <summary>Label for the card's primary action button.</summary>
    public string CardPrimaryActionLabel
    {
        get
        {
            // Both RenoDX and Luma rows are always visible — use RenoDX status for the primary action
            var effectiveStatus = Status;
            var effectiveInstalling = IsInstalling;

            if (effectiveInstalling) return Tr("Status.Installing");
            if (IsManaged)
            {
                // Any component has an update available → show update icon
                if (effectiveStatus == GameStatus.UpdateAvailable
                    || RsStatus == GameStatus.UpdateAvailable
                    || LumaStatus == GameStatus.UpdateAvailable)
                    return Tr("Action.ManageUpdate");
                return Tr("Action.Manage");
            }
            return Tr("Action.InstallPlain");
        }
    }

    /// <summary>True when the game has notes or a wiki/name link — shows info indicator on card.</summary>
    public bool HasInfoIndicator => HasNotes || HasNameUrl;

    /// <summary>False when any component is currently installing — disables card install button.</summary>
    public bool CanCardInstall => !IsInstalling && !RsIsInstalling && !IsLumaInstalling && !UlIsInstalling && !DcIsInstalling;

    // ── Per-component install enabled (card install flyout) ───────────────────────
    public bool CardRdxInstallEnabled  => !IsInstalling && Mod?.SnapshotUrl != null && !IsExternalOnly && (IsRsInstalled || ExcludeFromUpdateAllReShade);
    public bool CardRsInstallEnabled   => !RsIsInstalling && !(IsREEngineGame && !IsRefInstalled && !EffectiveLumaMode && !ExcludeFromUpdateAllRef);
    public bool CardLumaInstallEnabled => !IsLumaInstalling && LumaMod?.DownloadUrl != null && (IsRsInstalled || ExcludeFromUpdateAllReShade || LumaStatus != GameStatus.NotInstalled);

    private void NotifySidebarProps()
    {
        OnPropertyChanged(nameof(SidebarItemBackground));
        OnPropertyChanged(nameof(SidebarItemBorderBrush));
        OnPropertyChanged(nameof(SidebarItemForeground));
        OnPropertyChanged(nameof(IsManaged));
    }

    partial void OnIsSelectedChanged(bool value) => NotifySidebarProps();
    partial void OnIsRunningChanged(bool value) => NotifySidebarProps();
    partial void OnCardHighlightedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardBackground));
        OnPropertyChanged(nameof(CardBorderBrush));
    }

    /// <summary>Visibility for the individual component detail section.</summary>
    public Visibility ComponentDetailVisibility => ComponentExpanded ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Chevron glyph for expand/collapse.</summary>
    public string ExpandChevron => ComponentExpanded ? "▲" : "▼";

    partial void OnComponentExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ComponentDetailVisibility));
        OnPropertyChanged(nameof(ExpandChevron));
    }

    // ── Derived display ───────────────────────────────────────────────────────────

    // WikiStatus data values ("✅"/"🚧"/"?"…) are logic keys — only the labels are localized.
    public string WikiStatusLabel => WikiStatus == "✅" ? Tr("Status.Wiki.Working")
                                   : WikiStatus == "🚧" ? Tr("Status.Wiki.InProgress")
                                   : WikiStatus == "?"  ? Tr("Status.Wiki.MayWork")
                                   : WikiStatus == "💬" ? Tr("Status.Wiki.Discord")
                                   : WikiStatus == "🌐" ? Tr("Status.Wiki.Nexus")
                                   : WikiStatus == "—" && IsGenericMod ? Tr("Status.Wiki.MayWork")
                                   : "";

    /// <summary>
    /// Returns just the wiki status icon for grid card display.
    /// </summary>
    public string WikiStatusIcon => EffectiveLumaMode ? ""
                                  : WikiStatus == "✅" ? ""
                                  : WikiStatus == "🚧" ? "🚧"
                                  : WikiStatus == "?"  ? "⚠️"
                                  : WikiStatus == "💬" ? "💬"
                                  : WikiStatus == "🌐" ? "🌐"
                                  : WikiStatus == "—" && IsGenericMod ? "⚠️"
                                  : "❓";

    /// <summary>Whether the wiki status icon should be visible on grid cards (hidden in Luma mode).</summary>
    public bool WikiStatusIconVisible => !EffectiveLumaMode;

    // Badge colours change per status to make them visually distinct
    public string WikiStatusBadgeBackground  => WikiStatus == "💬" ? "#201838"
                                              : WikiStatus == "🌐" ? "#182840"
                                              : WikiStatus == "?"  ? "#201C10"
                                              : "#1A2030";
    public string WikiStatusBadgeBorderBrush => WikiStatus == "💬" ? "#3A2860"
                                              : WikiStatus == "🌐" ? "#2A4468"
                                              : WikiStatus == "?"  ? "#403018"
                                              : "#283240";
    public string WikiStatusBadgeForeground  => WikiStatus == "💬" ? "#B898E8"
                                              : WikiStatus == "🌐" ? "#7AACDD"
                                              : WikiStatus == "?"  ? "#D4A856"
                                              : "#A0AABB";

    public string SourceIcon => Source switch
    {
        "Steam" => "🟦", "GOG" => "🟣", "Epic" => "🟤", "EA App" => "🟧",
        "Ubisoft" => "🟠", "Manual" => "🔧", _ => "🎮"
    };

    public string? SourceIconPath => Source switch
    {
        "Steam"      => "Assets/icons/steam.ico",
        "GOG"        => "Assets/icons/gog.ico",
        "Epic"       => "Assets/icons/epic.ico",
        "EA App"     => "Assets/icons/ea.ico",
        "Xbox"       => "Assets/icons/xbox.ico",
        "Ubisoft"    => "Assets/icons/ubisoft.ico",
        "Battle.net" => "Assets/icons/battlenet.ico",
        "Rockstar"   => "Assets/icons/rockstar.ico",
        _            => null
    };

    /// <summary>
    /// Returns a pack URI for the source icon, or a dummy transparent URI when no icon exists.
    /// </summary>
    public Uri SourceIconUri => SourceIconPath != null
        ? new Uri($"ms-appx:///{SourceIconPath}")
        : new Uri("ms-appx:///Assets/icons/steam.ico");

    public bool HasSourceIcon => SourceIconPath != null;

    public Visibility SourceIconImageVisibility =>
        SourceIconPath != null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SourceIconTextVisibility =>
        SourceIconPath == null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visible when the game is flagged as 32-bit (shows badge next to source/engine).</summary>
    public Visibility Is32BitBadgeVisibility => Is32Bit ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visible on 32-bit UE cards — shows WIP placeholder instead of install button.</summary>
    public Visibility Is32BitUeWipVisibility =>
        (Is32Bit && IsGenericMod && EngineHint.Contains("Unreal") && !EngineHint.Contains("Legacy"))
            ? Visibility.Visible : Visibility.Collapsed;

    public string InstallPathDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(InstallPath)) return "";
            var parts = InstallPath.TrimEnd('\\', '/').Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 2 ? $"...\\{parts[^2]}\\{parts[^1]}" : InstallPath;
        }
    }

    public string InstalledFileLabel  => InstalledAddonFileName != null ? $"📦 {InstalledAddonFileName}" : "";
    public bool HasNotes              => !string.IsNullOrWhiteSpace(Notes);
    public bool IsUnityGeneric        => IsGenericMod && EngineHint.Contains("Unity");
    public bool HasDualBitMod         => Mod?.HasBothBitVersions == true;
    public bool HasExtraLinks         => NexusUrl != null || (DiscordUrl != null && EffectiveLumaMode) || IsExternalOnly;
    public bool HasNexusModsUrl       => !string.IsNullOrEmpty(NexusModsUrl);
    public bool HasPcgwUrl            => !string.IsNullOrEmpty(PcgwUrl);
    public bool HasUwFixUrl        => !string.IsNullOrEmpty(UwFixUrl);
    public bool HasUltraPlusUrl    => !string.IsNullOrEmpty(UltraPlusUrl);
    public bool HasNameUrl            => !string.IsNullOrEmpty(NameUrl);
    public string HideButtonLabel     => IsHidden ? Tr("Card.Show") : Tr("Card.Hide");
    public string StarForeground       => IsFavourite ? "#FFD700" : "#282840";
    public Visibility IsFavouriteVisibility      => IsFavourite ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsNotFavouriteVisibility   => IsFavourite ? Visibility.Collapsed : Visibility.Visible;

    // ── Visibility ────────────────────────────────────────────────────────────────

    public Visibility SourceBadgeVisibility      => string.IsNullOrEmpty(Source) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GenericBadgeVisibility     => IsGenericMod ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EngineBadgeVisibility      => !string.IsNullOrEmpty(EngineHint) ? Visibility.Visible : Visibility.Collapsed;
    public string GraphicsApiLabel               => GraphicsApiDetector.GetMultiLabel(DetectedApis, GraphicsApi);
    public bool HasGraphicsApiBadge              => GraphicsApi != GraphicsApiType.Unknown;
    public Visibility NotesButtonVisibility      => HasNotes ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProgressVisibility         => IsInstalling ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MessageVisibility          => string.IsNullOrEmpty(ActionMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ExternalBtnVisibility      => IsExternalOnly && !EffectiveLumaMode && CombinedRowVisibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ExtraLinkVisibility        => HasExtraLinks ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InstalledFileLabelVisible  => !string.IsNullOrEmpty(InstalledAddonFileName) && (!EffectiveLumaMode || LumaRenodxCompatible) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InstallOnlyBtnVisibility   => (!IsExternalOnly && Mod?.SnapshotUrl != null
                                                      && Status == GameStatus.Available
                                                      && Is32BitUeWipVisibility == Visibility.Collapsed
                                                      && (!EffectiveLumaMode || LumaRenodxCompatible))
                                                      ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ReinstallRowVisibility     => (!IsExternalOnly && Mod?.SnapshotUrl != null
                                                      && (Status == GameStatus.Installed || Status == GameStatus.UpdateAvailable)
                                                      && (!EffectiveLumaMode || LumaRenodxCompatible))
                                                      ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DualBitInstallVisibility   => Visibility.Collapsed;
    public Visibility UpdateBadgeVisibility      => ((Status == GameStatus.UpdateAvailable && !ExcludeFromUpdateAllRenoDx && (!string.IsNullOrEmpty(RdxInstalledVersion) || !string.IsNullOrEmpty(InstalledAddonFileName) || IsExternalOnly || IsEmulator))
                                                      || (RsStatus == GameStatus.UpdateAvailable && !ExcludeFromUpdateAllReShade && !EffectiveLumaMode)
                                                      || (UlStatus == GameStatus.UpdateAvailable && !ExcludeFromUpdateAllUl)
                                                      || (DcStatus == GameStatus.UpdateAvailable && !ExcludeFromUpdateAllDc)
                                                      || (OsStatus == GameStatus.UpdateAvailable && !ExcludeFromUpdateAllOs)
                                                      || (DxvkStatus == GameStatus.UpdateAvailable && !ExcludeFromUpdateAllDxvk)
                                                      || (LumaStatus == GameStatus.UpdateAvailable)
                                                      || (RefStatus == GameStatus.UpdateAvailable && !ExcludeFromUpdateAllRef)
                                                      || (DofFixStatus == GameStatus.UpdateAvailable && !ExcludeFromUpdateAllDofFix))
                                                      ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsHiddenVisibility         => IsHidden ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsNotHiddenVisibility      => IsHidden ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NameLinkVisibility         => HasNameUrl ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoModVisibility            => Visibility.Collapsed;
    public Visibility SwitchToLumaVisibility     => (Mod == null && string.IsNullOrEmpty(InstalledAddonFileName)
                                                      && !EffectiveLumaMode
                                                      && LumaFeatureEnabled && IsLumaAvailable)
                                                      ? Visibility.Visible : Visibility.Collapsed;

    partial void OnVulkanRenderingPathChanged(string value)
    {
        OnPropertyChanged(nameof(RequiresVulkanInstall));
        OnPropertyChanged(nameof(IsVulkanOnly));
        OnPropertyChanged(nameof(CardRsStatusDot));
    }

    partial void OnIsHiddenChanged(bool value) => OnPropertyChanged(nameof(HideButtonLabel));
    partial void OnIsFavouriteChanged(bool value)
    {
        OnPropertyChanged(nameof(StarForeground));
        OnPropertyChanged(nameof(IsFavouriteVisibility));
        OnPropertyChanged(nameof(IsNotFavouriteVisibility));
    }
    partial void OnInstallPathChanged(string value) => OnPropertyChanged(nameof(InstallPathDisplay));
    partial void OnSourceChanged(string value) => OnPropertyChanged(nameof(SourceBadgeVisibility));

    // ── Mod author computed properties ────────────────────────────────────────────

    /// <summary>
    /// Returns the author(s) to display for this game card.
    /// - Generic UE (not UE-Extended): ShortFuse
    /// - UE-Extended (manifest or toggled): Marat only
    /// - Generic Unity: Voosh
    /// - Named mods: wiki Maintainer field, split on "&amp;"
    /// </summary>
    /// <summary>Display-name overrides for wiki maintainer handles.</summary>
    private static readonly Dictionary<string, string> AuthorDisplayNames =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["oopydoopy"] = "Jon",
    };

    /// <summary>Donation page URLs keyed by display name (after resolution).</summary>
    private static readonly Dictionary<string, string> AuthorDonationUrls =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["ShortFuse"] = "https://ko-fi.com/shortfuse",
        ["Jon"]       = "https://ko-fi.com/kickfister",
        ["Forge"]     = "https://ko-fi.com/forge87682",
        ["Voosh"]     = "https://ko-fi.com/notvoosh",
        ["Musa"]      = "https://ko-fi.com/musaqh",
        ["Pumbo"]     = "https://ko-fi.com/pumbo",
        ["Nukem"]     = "https://ko-fi.com/nukem9",
        ["Lilium"]    = "https://ko-fi.com/endlesslyflowering",
        ["Bit Viper"] = "https://ko-fi.com/bitviper",
    };

    /// <summary>Returns the donation URL for the given author display name, or null if none is known.</summary>
    public static string? GetAuthorDonationUrl(string displayName) =>
        AuthorDonationUrls.TryGetValue(displayName, out var url) ? url : null;

    /// <summary>
    /// Merges manifest-provided donation URLs and display-name overrides into the
    /// hardcoded dictionaries. Manifest entries take priority over hardcoded ones.
    /// </summary>
    public static void MergeManifestAuthorData(
        Dictionary<string, string>? donationUrls,
        Dictionary<string, string>? displayNames)
    {
        if (displayNames != null)
            foreach (var (key, value) in displayNames)
                AuthorDisplayNames[key] = value;

        if (donationUrls != null)
            foreach (var (key, value) in donationUrls)
                AuthorDonationUrls[key] = value;
    }

    /// <summary>Splits an author string on '&amp;' or ' and ' (case-insensitive), trims, and drops empties.</summary>
    private static IEnumerable<string> SplitAuthors(string raw) =>
        System.Text.RegularExpressions.Regex.Split(raw, @"\s+and\s+|&", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(a => a.Trim())
            .Where(a => a.Length > 0);

    /// <summary>Resolves a single author segment to its display name.
    /// Strips parenthesised aliases (e.g. "oopydoopy (Jon)") before lookup.</summary>
    private static string ResolveAuthorName(string raw)
    {
        // Strip trailing parenthesised alias: "oopydoopy (Jon)" → "oopydoopy"
        var parenIdx = raw.IndexOf('(');
        var key = (parenIdx > 0 ? raw[..parenIdx].Trim() : raw);
        return AuthorDisplayNames.TryGetValue(key, out var display) ? display : raw;
    }

    public string[] AuthorList
    {
        get
        {
            // Luma mode: show the Luma mod author instead of the RenoDX author
            if (EffectiveLumaMode && LumaMod != null && !string.IsNullOrWhiteSpace(LumaMod.Author))
                return SplitAuthors(LumaMod.Author).ToArray();

            // UE-Extended overrides everything — credit goes to Marat alone
            if (UseUeExtended || IsManifestUeExtended)
                return new[] { "Marat" };

            // Named mod with a maintainer from the wiki — resolve display names
            if (!string.IsNullOrWhiteSpace(Maintainer))
                return SplitAuthors(Maintainer).Select(ResolveAuthorName).ToArray();

            // Generic engine mods without a named maintainer
            if (IsGenericMod)
            {
                if (EngineHint?.Contains("Unreal", StringComparison.OrdinalIgnoreCase) == true)
                    return new[] { "ShortFuse" };
                if (EngineHint?.Contains("Unity", StringComparison.OrdinalIgnoreCase) == true)
                    return new[] { "Voosh" };
            }

            return Array.Empty<string>();
        }
    }

    /// <summary>True when at least one author name is present.</summary>
    public bool HasAuthors => AuthorList.Length > 0;
}
