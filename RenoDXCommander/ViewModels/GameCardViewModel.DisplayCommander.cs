using Microsoft.UI.Xaml;
using RenoDXCommander.Models;

namespace RenoDXCommander.ViewModels;

// Display Commander status, install state, and computed properties
public partial class GameCardViewModel
{
    // ── DC computed properties ─────────────────────────────────────────────────────

    /// <summary>Per-component status dot for Display Commander.</summary>
    public string DcStatusDot => DcStatus == GameStatus.UpdateAvailable ? "🟢"
        : DcStatus == GameStatus.Installed ? "🟢" : "⚪";

    // "DC" short form keeps the narrow detail-row button compact (same as original)
    public string DcActionLabel => DcIsInstalling ? Tr("Status.Installing")
        : (!IsRsInstalled && !ExcludeFromUpdateAllReShade) ? Tr("Action.ReShadeRequired")
        : DcStatus == GameStatus.UpdateAvailable ? Tr("Action.Update", Tr("Detail.DC"))
        : DcStatus == GameStatus.Installed ? Tr("Action.Reinstall", Tr("Detail.DC"))
        : Tr("Action.Install", Tr("Detail.DC"));

    public string DcBtnBackground  => DcStatus == GameStatus.UpdateAvailable ? "#201838" : "#182840";
    public string DcBtnForeground  => DcStatus == GameStatus.UpdateAvailable ? "#B898E8" : "#7AACDD";
    public string DcBtnBorderBrush => DcStatus == GameStatus.UpdateAvailable ? "#3A2860" : "#2A4468";

    public Visibility DcProgressVisibility => DcIsInstalling ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DcMessageVisibility  => string.IsNullOrEmpty(DcActionMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DcDeleteVisibility   => DcStatus == GameStatus.Installed || DcStatus == GameStatus.UpdateAvailable
        ? Visibility.Visible : Visibility.Collapsed;

    public string DcStatusText => DcIsInstalling ? Tr("Status.InstallingShort")
        : DcStatus == GameStatus.UpdateAvailable ? Tr("Status.UpdateShort")
        : DcStatus == GameStatus.Installed ? (DcInstalledVersion ?? Tr("Status.Installed"))
        : Tr("Status.Ready");
    public string DcStatusColor => DcIsInstalling ? "#D4A856"
        : DcStatus == GameStatus.UpdateAvailable ? "#B898E8"
        : DcStatus == GameStatus.Installed ? "#5ECB7D"
        : "#A0AABB";
    public string DcShortAction => DcIsInstalling ? "…"
        : DcStatus == GameStatus.UpdateAvailable ? Tr("Action.UpdateShort")
        : DcStatus == GameStatus.Installed ? Tr("Action.ReinstallShort")
        : Tr("Action.InstallShort");

    public bool IsDcNotInstalling => !DcIsInstalling;
    public bool IsDcInstalled => DcStatus == GameStatus.Installed || DcStatus == GameStatus.UpdateAvailable;

    /// <summary>True when DisplayCommander.ini is present in the inis folder — enables the 📋 button.</summary>
    public bool DcIniExists => File.Exists(Services.AuxInstallService.DcIniPath);

    /// <summary>DC install button disabled when installing, when ReLimiter is installed (mutual exclusion), when normal ReShade is active, or when ReShade is not installed.</summary>
    public bool DcInstallEnabled => !DcIsInstalling && !IsUlInstalled && !UseNormalReShade && (IsRsInstalled || ExcludeFromUpdateAllReShade);

    // ── Card grid properties ──────────────────────────────────────────────────────
    public string CardDcStatusDot => DcIsInstalling ? "#2196F3"
        : DcStatus == GameStatus.UpdateAvailable ? "#4CAF50"
        : DcStatus == GameStatus.Installed ? "#4CAF50" : "#5A6880";
    public bool CardDcInstallEnabled => !DcIsInstalling && (IsRsInstalled || ExcludeFromUpdateAllReShade);

    /// <summary>
    /// DC row is always visible (available in both standard and Luma modes).
    /// </summary>
    public Visibility DcRowVisibility => Visibility.Visible;

    // ── Targeted notification: DcStatus changed ───────────────────────────────────
    private void NotifyDcStatusDependents()
    {
        OnPropertyChanged(nameof(DcStatusDot));
        OnPropertyChanged(nameof(DcActionLabel));
        OnPropertyChanged(nameof(DcBtnBackground));
        OnPropertyChanged(nameof(DcBtnForeground));
        OnPropertyChanged(nameof(DcBtnBorderBrush));
        OnPropertyChanged(nameof(DcDeleteVisibility));
        OnPropertyChanged(nameof(DcStatusText));
        OnPropertyChanged(nameof(DcStatusColor));
        OnPropertyChanged(nameof(DcShortAction));
        OnPropertyChanged(nameof(IsDcInstalled));
        OnPropertyChanged(nameof(DcInstallEnabled));
        OnPropertyChanged(nameof(CardDcStatusDot));
        OnPropertyChanged(nameof(CardDcInstallEnabled));
        OnPropertyChanged(nameof(UlInstallEnabled));
        OnPropertyChanged(nameof(UpdateBadgeVisibility));
        // Managed state (DC is now part of IsManaged)
        OnPropertyChanged(nameof(IsManaged));
        OnPropertyChanged(nameof(SidebarItemForeground));
        // Card grid
        OnPropertyChanged(nameof(CardPrimaryActionLabel));
    }

    // ── Targeted notification: DcIsInstalling changed ─────────────────────────────
    private void NotifyDcIsInstallingDependents()
    {
        OnPropertyChanged(nameof(DcActionLabel));
        OnPropertyChanged(nameof(DcProgressVisibility));
        OnPropertyChanged(nameof(IsDcNotInstalling));
        OnPropertyChanged(nameof(DcInstallEnabled));
        OnPropertyChanged(nameof(DcStatusText));
        OnPropertyChanged(nameof(DcStatusColor));
        OnPropertyChanged(nameof(DcShortAction));
        OnPropertyChanged(nameof(CardDcStatusDot));
        OnPropertyChanged(nameof(CardDcInstallEnabled));
        OnPropertyChanged(nameof(CanCardInstall));
    }

    partial void OnDcStatusChanged(GameStatus value) => NotifyDcStatusDependents();
    partial void OnDcIsInstallingChanged(bool value) => NotifyDcIsInstallingDependents();
    partial void OnDcInstalledVersionChanged(string? value) => OnPropertyChanged(nameof(DcStatusText));
    partial void OnDcActionMessageChanged(string value) => OnPropertyChanged(nameof(DcMessageVisibility));
}
