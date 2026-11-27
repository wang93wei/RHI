// MainWindow.Events.Components.Rdx.ModMatching.cs — RenoDX ⚙ › Mod matching (v2.9.6, from Game Overrides, removed in
// 2.9.6.3): the wiki mod name the game is looked up by and whether it's looked up on the RenoDX / Luma wikis at all.
// The logic is in DetailPanelBuilder.Overrides.GameShared.cs.

using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private void AddRdxModMatchingSection(CogDialog cog, GameCardViewModel card)
    {
        var dp = _detailPanelBuilder;
        if (dp == null) return;
        var gameName = card.GameName;

        cog.AddSection(Loc.GetString("Rdx.ModMatching.Section"));
        var wikiBox = CogDialog.MakeTextBox(dp.GetWikiName(gameName), Loc.GetString("Rdx.ModMatching.WikiName.Placeholder"));
        void SaveWiki()
        {
            var text = wikiBox.Text?.Trim() ?? "";
            if (text == dp.GetWikiName(gameName)) return;
            dp.SetWikiName(gameName, text);
            dp.QueueOverridesRebuild(gameName);
            cog.SetSuccess(text.Length > 0
                ? Loc.GetString("Rdx.ModMatching.SavedWithName", text)
                : Loc.GetString("Rdx.ModMatching.SavedOwnName"));
        }
        wikiBox.KeyDown += (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; SaveWiki(); } };
        wikiBox.LostFocus += (s, e) => SaveWiki();
        cog.AddRow(Loc.GetString("Overrides.WikiName.Header"), wikiBox,
            Loc.GetString("Rdx.ModMatching.WikiName.Help"));

        cog.AddCombo(Loc.GetString("Rdx.ModMatching.Lookup.Label"),
            Loc.GetString("Rdx.ModMatching.Lookup.Help"),
            new[] { DetailPanelBuilder.WikiIncludedItem, DetailPanelBuilder.WikiExcludedItem },
            dp.GetWikiLookupDisplay(gameName) == DetailPanelBuilder.WikiExcludedItem ? 1 : 0,
            i =>
            {
                dp.SetWikiLookup(gameName, i == 1 ? DetailPanelBuilder.WikiExcludedItem : DetailPanelBuilder.WikiIncludedItem);
                dp.QueueOverridesRebuild(gameName);
                cog.SetSuccess(i == 1 ? Loc.GetString("Rdx.ModMatching.LookupExcluded.Status") : Loc.GetString("Rdx.ModMatching.LookupIncluded.Status"));
            });
    }
}
