// DetailPanelBuilder.Extras.HsfEngine.cs: HSF ⚙ › Frame generation engine (DLSS-G / XeMFG), HSF.ini [HSF] FgEngine.
// Shown when the game's HSF build knows XeMFG and Intel's XeSS frame generation files are there (HsfService.FgEngine).
// XeMFG without Universal MFG Unlocker (or MFG Ada Unlock) is allowed but limited to 2x, on every RTX card.

using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private void AddHsfFgEngineSection(CogDialog cog, GameCardViewModel card, HsfService svc)
    {
        var installPath = card.InstallPath ?? "";
        if (!HsfService.SupportsXeMfg(installPath, card.Is32Bit) || !HsfService.HasXeFgFiles(installPath)) return;

        cog.AddSection(Loc.GetString("Extras.HsfEngine.Title"));
        cog.AddIntro(Loc.GetString("Extras.HsfEngine.Intro"));
        var unlockerNote = cog.AddNote("");
        // XeMFG needs Universal MFG Unlocker for 3x-6x on every RTX card: offer it right here (HSF 0.10 expects it)
        Microsoft.UI.Xaml.Controls.Button? installMfgBtn = null;
        Microsoft.UI.Xaml.FrameworkElement? mfgButtons = null;
        installMfgBtn = cog.MakeButton(Loc.GetString("Extras.HsfEngine.InstallButton"), CogButtonKind.Primary, async () =>
        {
            installMfgBtn!.IsEnabled = false;
            cog.SetStatus(Loc.GetString("Extras.HsfEngine.Installing"));
            var error = await _window.ViewModel.InstallMfgForHsfAutoAsync(card);
            installMfgBtn.IsEnabled = true;
            if (error != null) { cog.SetError(error); return; }
            cog.SetSuccess(Loc.GetString("Extras.HsfEngine.Installed"));
            ShowUnlocker(true);
            RequestExtrasRebuild(card);
        }, Loc.GetString("Extras.HsfEngine.InstallButton.Tooltip"));
        mfgButtons = cog.AddButtons(installMfgBtn);
        void ShowUnlocker(bool xemfg)
        {
            bool missing = xemfg && !card.Rtx40MfgInstalled && !card.MfgAdaInstalled;
            unlockerNote.Text = !xemfg
                ? Loc.GetString("Extras.HsfEngine.Note.DlssG")
                : missing
                    ? Loc.GetString("Extras.HsfEngine.Note.Limited")
                    : Loc.GetString("Extras.HsfEngine.Note.Unlocker");
            if (mfgButtons != null) mfgButtons.Visibility = missing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        bool isXeMfg = HsfService.ReadFgEngine(installPath) == HsfService.EngineXeMfg;
        ShowUnlocker(isXeMfg);
        cog.AddCombo(Loc.GetString("Extras.RtxEncore.Settings.Engine.Label"),
            Loc.GetString("Extras.HsfEngine.Engine.Desc"),
            new[] { "DLSS-G", "XeMFG" }, isXeMfg ? 1 : 0,
            i =>
            {
                var engine = i == 1 ? HsfService.EngineXeMfg : HsfService.EngineDlssg;
                if (!svc.WriteFgEngine(installPath, engine))
                {
                    cog.SetError(Loc.GetString("Extras.Hsf.Keys.SaveFailed"));
                    return false;
                }
                ShowUnlocker(i == 1);
                cog.SetSuccess(i == 1
                    ? Loc.GetString("Extras.HsfEngine.Set.XeMfg")
                    : Loc.GetString("Extras.HsfEngine.Set.DlssG"));
                return true;
            });
        cog.AddNote(Loc.GetString("Extras.HsfEngine.Note.ScRgb"));
    }
}
