// MainWindow.ComponentRows.cs -- one table of the Components section's rows (controls + click
// handlers). The Tabs layout's Overview cards copy their state from these rows and call the same
// handlers, so every per-component rule stays in DetailPanelBuilder.UpdateDetailComponentRows.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RenoDXCommander;

/// <summary>A Components section row: its controls and what each of its buttons does.</summary>
internal sealed record ComponentRowRef(
    string Key,
    string Caption,
    string FallbackName,
    Grid Row,
    TextBlock? Label,
    TextBlock Status,
    Button Info,
    Button Install,
    Button Cog,
    Button Delete,
    ProgressBar Progress,
    TextBlock Message,
    RoutedEventHandler OnInfo,
    RoutedEventHandler OnInstall,
    RoutedEventHandler OnCog,
    RoutedEventHandler OnDelete);

public sealed partial class MainWindow
{
    private IReadOnlyList<ComponentRowRef>? _componentRows;

    /// <summary>The Components rows in display order (same order as the Classic section).</summary>
    internal IReadOnlyList<ComponentRowRef> ComponentRows => _componentRows ??= new[]
    {
        new ComponentRowRef("Ref", Loc.GetString("MainWindow.ComponentRow.ModFramework"), Loc.GetString("Detail.REFramework"), DetailRefRow, null, DetailRefStatus,
            DetailRefInfoBtn, DetailRefInstallBtn, DetailRefCogBtn, DetailRefDeleteBtn, DetailRefProgress, DetailRefMessage,
            InfoButton_Click, InstallRefButton_Click, RefCogButton_Click, UninstallRefButton_Click),
        new ComponentRowRef("Rs", Loc.GetString("MainWindow.ComponentRow.Injector"), Loc.GetString("Detail.ReShade"), DetailRsRow, null, DetailRsStatus,
            DetailRsInfoBtn, DetailRsInstallBtn, DetailRsIniBtn, DetailRsDeleteBtn, DetailRsProgress, DetailRsMessage,
            InfoButton_Click, InstallRsButton_Click, RsCogButton_Click, UninstallRsButton_Click),
        new ComponentRowRef("Rdx", Loc.GetString("MainWindow.ComponentRow.HdrMod"), Loc.GetString("Detail.RenoDX"), DetailRdxRow, DetailRdxLabel, DetailRdxStatus,
            DetailRdxInfoBtn, DetailRdxInstallBtn, DetailUeExtendedBtn, DetailRdxDeleteBtn, DetailRdxProgress, DetailRdxMessage,
            InfoButton_Click, InstallButton_Click, RdxCogButton_Click, UninstallButton_Click),
        new ComponentRowRef("Luma", Loc.GetString("MainWindow.ComponentRow.HdrMod"), Loc.GetString("Detail.Luma"), DetailLumaRow, DetailLumaLabel, DetailLumaStatus,
            DetailLumaInfoBtn, DetailLumaInstallBtn, DetailLumaIniBtn, DetailLumaDeleteBtn, DetailLumaProgress, DetailLumaMessage,
            InfoButton_Click, InstallLumaButton_Click, LumaCogButton_Click, UninstallLumaButton_Click),
        new ComponentRowRef("DofFix", Loc.GetString("MainWindow.ComponentRow.Recommended"), Loc.GetString("Detail.DofFix"), DetailDofFixRow, DetailDofFixLabel, DetailDofFixStatus,
            DetailDofFixInfoBtn, DetailDofFixInstallBtn, DetailDofFixCogBtn, DetailDofFixDeleteBtn, DetailDofFixProgress, DetailDofFixMessage,
            DofFixInfoButton_Click, InstallDofFixButton_Click, DofFixCogButton_Click, UninstallDofFixButton_Click),
        new ComponentRowRef("Ul", Loc.GetString("MainWindow.ComponentRow.FrameLimiter"), Loc.GetString("Detail.ReLimiter"), DetailUlRow, DetailUlLabel, DetailUlStatus,
            DetailUlInfoBtn, DetailUlInstallBtn, DetailUlIniBtn, DetailUlDeleteBtn, DetailUlProgress, DetailUlMessage,
            InfoButton_Click, InstallUlButton_Click, UlCogButton_Click, UninstallUlButton_Click),
        new ComponentRowRef("Dc", Loc.GetString("MainWindow.ComponentRow.FrameLimiter"), Loc.GetString("Detail.DisplayCommander"), DetailDcRow, DetailDcLabel, DetailDcStatus,
            DetailDcInfoBtn, DetailDcInstallBtn, DetailDcIniBtn, DetailDcDeleteBtn, DetailDcProgress, DetailDcMessage,
            InfoButton_Click, InstallDcButton_Click, DcCogButton_Click, UninstallDcButton_Click),
    };

    /// <summary>"No RenoDX mod available" note under the rows (shown by UpdateDetailComponentRows).</summary>
    internal Border NoModMessage => DetailNoModMsg;
}
