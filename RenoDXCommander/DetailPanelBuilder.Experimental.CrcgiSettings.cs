// DetailPanelBuilder.Experimental.CrcgiSettings.cs -- CRCGI's ⚙: the effect's main settings and quality switches in the
// game's ReShade preset (compact grid, help in tooltips), the add-on's level (crcgi_injector.ini next to the game exe),
// the author's ready-made preset for this game when the repo has one for the installed version, and a reset to CRCGI's
// own defaults. The ~140 Expert settings and the add-on's buffer setup stay in ReShade's overlay (tuned per game while
// watching it). Writes go through CrcgiService.Settings.cs; values follow ReShade's own format (floats as 1.000000).

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    /// <summary>One ⚙ setting: a uniform (key in the effect's section) or a compile-time switch (IsDefine).</summary>
    private sealed record CrcgiSetting(string Label, string Help, string Key, bool IsDefine, string[] Displays, string[] Values, string Default);

    private static string F(double v) => v.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string[] CrcgiOffOn => new[] { Loc.GetString("Xaml.Off"), Loc.GetString("Xaml.On") };
    private static readonly string[] CrcgiZeroOne = { "0", "1" };

    /// <summary>Defaults and ranges from CRCGI_v2.fx (README: every setting at 1 or half its range).</summary>
    private static CrcgiSetting[] CrcgiSettings => new CrcgiSetting[]
    {
        new(Loc.GetString("Extras.Crcgi.Setting.GiIntensity.Label"), Loc.GetString("Extras.Crcgi.Setting.GiIntensity.Help"),
            "Intensity", false, new[] { "0.5", "0.75", Loc.GetString("Extras.Crcgi.Opt.OneDefault"), "1.25", "1.5", "2", "3" },
            new[] { F(0.5), F(0.75), F(1), F(1.25), F(1.5), F(2), F(3) }, F(1)),
        new(Loc.GetString("Extras.Crcgi.Setting.ShadowAmount.Label"), Loc.GetString("Extras.Crcgi.Setting.ShadowAmount.Help"),
            "ShadowAmount", false, new[] { Loc.GetString("Extras.Crcgi.Opt.MinusHalfLighter"), Loc.GetString("Extras.Crcgi.Opt.ZeroOff"), "0.5", Loc.GetString("Extras.Crcgi.Opt.OneDefault") },
            new[] { F(-0.5), F(0), F(0.5), F(1) }, F(1)),
        new(Loc.GetString("Extras.Crcgi.Setting.Multibounce.Label"), Loc.GetString("Extras.Crcgi.Setting.Multibounce.Help"),
            "Multibounce", false, CrcgiOffOn, CrcgiZeroOne, "1"),
        new(Loc.GetString("Extras.Crcgi.Setting.MultibounceStrength.Label"), Loc.GetString("Extras.Crcgi.Setting.MultibounceStrength.Help"),
            "MultibounceAmount", false, new[] { "0.5", Loc.GetString("Extras.Crcgi.Opt.OneDefault"), "1.5", "2" },
            new[] { F(0.5), F(1), F(1.5), F(2) }, F(1)),
        new(Loc.GetString("Extras.Crcgi.Setting.SkyLight.Label"), Loc.GetString("Extras.Crcgi.Setting.SkyLight.Help"),
            "ProcessSkyColor", false, CrcgiOffOn, CrcgiZeroOne, "0"),
        new(Loc.GetString("Extras.Crcgi.Setting.DarkAreaBoost.Label"), Loc.GetString("Extras.Crcgi.Setting.DarkAreaBoost.Help"),
            "DarkAreaBoost", false, new[] { Loc.GetString("Extras.Crcgi.Opt.ZeroDefault"), "0.5", "1", "2" },
            new[] { F(0), F(0.5), F(1), F(2) }, F(0)),
        new(Loc.GetString("Extras.Crcgi.Setting.FramesAccumulated.Label"), Loc.GetString("Extras.Crcgi.Setting.FramesAccumulated.Help"),
            "TemporalFrames", false, new[] { "8", Loc.GetString("Extras.Crcgi.Opt.SixteenDefault"), "32", "64" },
            new[] { F(8), F(16), F(32), F(64) }, F(16)),
        new(Loc.GetString("Extras.Crcgi.Setting.FarCascadesHalfRate.Label"), Loc.GetString("Extras.Crcgi.Setting.FarCascadesHalfRate.Help"),
            "FarHalfRate", false, CrcgiOffOn, CrcgiZeroOne, "0"),
        new(Loc.GetString("Extras.Crcgi.Setting.EffectResolution.Label"), Loc.GetString("Extras.Crcgi.Setting.EffectResolution.Help"),
            "CRCGI_RESOLUTION_DIVISOR", true, new[] { Loc.GetString("Extras.Crcgi.Opt.Full"), Loc.GetString("Extras.Crcgi.Opt.Half"), Loc.GetString("Extras.Crcgi.Opt.Third"), Loc.GetString("Extras.Crcgi.Opt.QuarterDefault"), Loc.GetString("Extras.Crcgi.Opt.Fifth"), Loc.GetString("Extras.Crcgi.Opt.Sixth") },
            new[] { "1", "2", "3", "4", "5", "6" }, "4"),
        new(Loc.GetString("Extras.Crcgi.Setting.Cascades.Label"), Loc.GetString("Extras.Crcgi.Setting.Cascades.Help"),
            "CRCGI_CASCADES", true, new[] { "4", Loc.GetString("Extras.Crcgi.Opt.FiveDefault"), "6" },
            new[] { "4", "5", "6" }, "5"),
    };

    private static int CrcgiIndex(string[] values, string? current, string def)
    {
        var v = current ?? def;
        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i], v, StringComparison.OrdinalIgnoreCase)) return i;
            if (double.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && Math.Abs(a - b) < 1e-4)
                return i;
        }
        return -1;
    }

    internal async Task ShowCrcgiSettingsAsync(GameCardViewModel card)
    {
        var installPath = card.InstallPath ?? "";
        bool isInstalled = CrcgiService.IsInstalledIn(installPath);
        bool isOurs = isInstalled && CrcgiService.IsRhiInstalled(installPath);
        var svc = App.Services.GetRequiredService<CrcgiService>();
        var section = isInstalled ? CrcgiService.EffectSection(installPath) : null;
        bool canEdit = section != null;
        bool reopen = false;

        // Gathered before the dialog: the preset text, the add-on's level, the author's preset for this game (GitHub)
        var presetText = canEdit ? await Task.Run(() => CrcgiService.ReadPreset(installPath)) : null;
        var exeDir = isInstalled ? await Task.Run(() => CrcgiService.ExeDir(installPath, App.Services.GetRequiredService<IPeHeaderService>())) : installPath;
        int level = isInstalled ? CrcgiService.ReadInjectorLevel(exeDir) : 0;
        CrcgiService.AuthorPresetSet? authorSet = null;
        if (canEdit)
        {
            var lookup = svc.FindAuthorPresetsAsync(card.GameName, section);
            if (await Task.WhenAny(lookup, Task.Delay(5000)) == lookup) authorSet = await lookup;
        }

        var cog = new CogDialog("CRCGI", _window.Content.XamlRoot);
        cog.AddIntro(Loc.GetString("Extras.Crcgi.Intro"));
        cog.AddWarning(Loc.GetString("Extras.Crcgi.Warning.SinglePlayer"));
        if (card.IsRunning)
            cog.AddWarning(Loc.GetString("Extras.Crcgi.Warning.GameRunning"));

        cog.AddSection(Loc.GetString("Extras.Crcgi.Section.ThisGame"));
        cog.AddNote(!isInstalled ? Loc.GetString("Extras.Crcgi.Note.NotInstalled")
            : !isOurs ? Loc.GetString("Extras.Crcgi.Note.Manual")
            : Loc.GetString("Extras.Crcgi.Note.Installed",
                CrcgiService.InstalledVersion(installPath) ?? "?", section, card.Is32Bit ? "32" : "64"));

        // ── Author's preset ───────────────────────────────────────────────────
        if (authorSet != null)
        {
            cog.AddSection(Loc.GetString("Extras.Crcgi.Section.AuthorPreset"));
            cog.AddNote(Loc.GetString("Extras.Crcgi.Note.AuthorPreset", authorSet.Folder,
                authorSet.Targets != null ? Loc.GetString("Extras.Crcgi.Note.AuthorPresetTargets", authorSet.Targets.Name) : ""));
            int pick = 0;
            var names = authorSet.Presets.Select(p => Path.GetFileNameWithoutExtension(p.Name)).ToList();
            if (names.Count > 1)
                cog.AddCompactGrid(cog.CompactCombo(Loc.GetString("Dialog.Preset"), Loc.GetString("Extras.Crcgi.AuthorPreset.Preset.Help"), names, 0, i => { pick = i; return true; }));
            else
                cog.AddNote(names[0]);
            var buttons = new List<Microsoft.UI.Xaml.Controls.Button>();
            Microsoft.UI.Xaml.Controls.Button? applyBtn = null;
            applyBtn = cog.MakeButton(Loc.GetString("Extras.Crcgi.AuthorPreset.Use.Button"), CogButtonKind.Primary, async () =>
            {
                applyBtn!.IsEnabled = false;
                cog.SetStatus(Loc.GetString("Extras.Crcgi.AuthorPreset.Downloading"));
                var err = await svc.ApplyAuthorPresetAsync(installPath, authorSet.Presets[pick], authorSet.Targets, exeDir,
                    App.Services.GetRequiredService<UpliftService>());
                applyBtn.IsEnabled = true;
                if (err != null) { cog.SetError(err); return; }
                reopen = true; // show the new values
                cog.Hide();
            });
            buttons.Add(applyBtn);
            if (authorSet.ReadmeUrl != null)
                buttons.Add(cog.MakeButton(Loc.GetString("Extras.Crcgi.AuthorPreset.Notes.Button"), CogButtonKind.Secondary,
                    async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(authorSet.ReadmeUrl)); }));
            cog.AddButtons(buttons.ToArray());
        }

        // ── Main settings + quality ───────────────────────────────────────────
        CogDialog.CompactCell Cell(CrcgiSetting s)
        {
            var current = !canEdit ? null
                : s.IsDefine ? CrcgiPreset.GetDefine(presetText, section!, s.Key)
                : CrcgiPreset.GetValue(presetText, section!, s.Key);
            var displays = s.Displays;
            var values = s.Values;
            int idx = CrcgiIndex(values, current, s.Default);
            if (idx < 0 && current != null)
            {
                // A value set in the overlay that isn't in the list: show it as it is
                displays = displays.Append(current).ToArray();
                values = values.Append(current).ToArray();
                idx = values.Length - 1;
            }
            if (idx < 0) idx = Math.Max(0, Array.IndexOf(values, s.Default));
            return cog.CompactCombo(s.Label, s.Help, displays, idx, i =>
            {
                if (!canEdit) return false;
                var value = values[i];
                bool ok = svc.EditPreset(installPath,
                    t => s.IsDefine ? CrcgiPreset.SetDefine(t, section!, s.Key, value) : CrcgiPreset.SetValue(t, section!, s.Key, value),
                    $"{s.Key}={value}");
                if (ok) cog.SetSuccess(card.IsRunning ? Loc.GetString("Extras.Crcgi.Saved.Reload") : Loc.GetString("Extras.Crcgi.Saved"));
                else cog.SetError(Loc.GetString("Extras.Crcgi.Error.WritePreset"));
                return ok;
            }, isEnabled: canEdit);
        }

        cog.AddSection(Loc.GetString("Extras.Crcgi.Section.Light"));
        cog.AddCompactGrid(CrcgiSettings.Where(s => !s.IsDefine && s.Key is not ("TemporalFrames" or "FarHalfRate")).Select(Cell).ToArray());
        cog.AddSection(Loc.GetString("Extras.Crcgi.Section.QualitySpeed"));
        cog.AddNote(Loc.GetString("Extras.Crcgi.Note.QualitySpeed"));
        cog.AddCompactGrid(CrcgiSettings.Where(s => s.IsDefine || s.Key is "TemporalFrames" or "FarHalfRate").Select(Cell).ToArray());

        // ── Add-on ────────────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.Crcgi.Section.Injector"));
        cog.AddCompactGrid(cog.CompactCombo(Loc.GetString("Extras.Crcgi.Injector.Level.Label"),
            Loc.GetString("Extras.Crcgi.Injector.Level.Help"),
            new[] { Loc.GetString("Extras.Crcgi.Opt.ADefault"), "B", "C" }, level, i =>
            {
                if (!isInstalled) return false;
                bool ok = svc.WriteInjectorLevel(exeDir, i);
                if (ok) cog.SetSuccess(Loc.GetString("Extras.Crcgi.Injector.Saved"));
                else cog.SetError(Loc.GetString("Extras.Crcgi.Injector.WriteError"));
                return ok;
            }, isEnabled: isInstalled));
        cog.AddNote(Loc.GetString("Extras.Crcgi.Note.Injector"));

        // ── Files / reset ─────────────────────────────────────────────────────
        cog.AddSection(Loc.GetString("Extras.Crcgi.Section.More"));
        cog.AddNote(Loc.GetString("Extras.Crcgi.Note.More"));
        var resetBtn = cog.MakeButton(Loc.GetString("Extras.Crcgi.Reset.Button"), CogButtonKind.Secondary, () =>
        {
            if (!canEdit) return;
            bool ok = svc.EditPreset(installPath, t =>
            {
                var r = CrcgiPreset.RemoveKeys(t, section!, CrcgiSettings.Where(s => !s.IsDefine).Select(s => s.Key));
                foreach (var d in CrcgiSettings.Where(s => s.IsDefine)) r = CrcgiPreset.SetDefine(r, section!, d.Key, null);
                return r;
            }, "Reset to defaults");
            if (ok) { reopen = true; cog.Hide(); }
            else cog.SetError(Loc.GetString("Extras.Crcgi.Error.WritePreset"));
        });
        resetBtn.IsEnabled = canEdit;
        cog.AddButtons(resetBtn,
            cog.MakeOpenFileButton(Loc.GetString("Extras.Crcgi.OpenPreset.Button"), canEdit ? CrcgiService.PresetFile(installPath) : null),
            cog.MakeButton(Loc.GetString("Extras.Crcgi.OpenReadme.Button"), CogButtonKind.Secondary,
                async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(CrcgiService.ReadmeUrl)); }));

        await cog.ShowAsync();
        if (reopen) await ShowCrcgiSettingsAsync(card);
    }
}
