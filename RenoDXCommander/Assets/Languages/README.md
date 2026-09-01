# Languages

JSON-based i18n resource packs. `en-US.json` is the authoritative full set. Other languages fall back to `en-US` for missing keys.

## Files

- `en-US.json` — English (US), fallback, ~1160 keys
- `zh-CN.json` — Simplified Chinese (简体中文)
- `zh-TW.json` — Traditional Chinese (繁體中文)
- `ja-JP.json` — Japanese (日本語)
- `ko-KR.json` — Korean (한국어)

## Adding a new language

1. Copy `en-US.json` to `{lang}.json` (e.g. `fr-FR.json`)
2. Translate values, keep keys unchanged
3. Add the code to `LocalizationService._supported` in `Services/LocalizationService.cs`
4. Add UI entry in `SettingsViewModel` / `SettingsHandler` language combo
5. Run coverage check: `python3 tools/check-i18n-coverage.py` (or `pwsh tools/check-i18n-coverage.ps1`)

## Key naming

`Area.Component.Key` with suffixes:

- `.Tooltip` — tooltip text
- `.Placeholder` — placeholder
- `.Button` — button label

Parameterized strings use `{0}`, `{1}` or `{name}` placeholders via `string.Format`.

### `Option.*` — combo-box option labels

`LocOpt.T("Off")` in code resolves to `"Option.Off"` in the catalogs. The key suffix is the
canonical English label verbatim (including spaces/punctuation, e.g. `"Option.App Controlled"`,
`"Option.Up to 3x"`). Rules:

- `en-US.json` defines **every** key that passes through `LocOpt.T`, with the English label as
  the value — this includes untranslated technical values (`"Option.DX12"`, `"Option.4GB"`) so
  the fallback chain never logs them as missing.
- Other languages translate the semantic labels only; technical values are omitted and fall
  back to English.
- Parameterized option formats: `Option.GlobalFormat` (`Global ({0})`),
  `Option.VersionDefaultFormat` (`{0} (Default)`), `Option.CustomPercentFormat` (`Custom ({0}%)`).
- When a combo's display text is localized, callbacks must resolve the logical value by index
  against the raw string array (or `ComboBox.SelectedIndex`) — never compare the display text
  against English literals.

## Fallback

`CurrentLanguage → en-US → key` — never throws. Missing keys are logged via `CrashReporter.Log` once per session.

## Coverage

`Coverage(lang)` = translated keys / total en-US keys. CI prints coverage; missing keys do not block build but should be translated for key user journeys.

## Intentionally untranslated (i18n R3.4)

The following stay in English by design and have no keys:

- `CrashReporter.Log` messages, exception messages, and game support reports (`GameReportEncoder`) — diagnostics shared with the support team.
- File names, registry paths, DLL names, URLs, version numbers, Shader/Addon IDs and other technical identifiers.
- FAQ long-form body/tip paragraphs in `MainWindow.FaqBuilder.cs` — only the structural section titles and link labels are localized; the prose remains English.
- Transient install progress messages assigned to `*ActionMessage` (set by install pipelines/services).
- Filter/search logic tokens in `FilterViewModel` (`Detected`, `Installed`, `Unreal`, …) — these are matching keys, not display text; the visible chip labels come from `Filter.*` / `Detail.*` keys.
- The OptiScaler nightly cog dialog's coupled dropdowns (upscaler/FG input/FG output/HUD fix/combined/DMV/flip, `MainWindow.Events.Components.cs`) — their display strings double as INI mapping keys and captured preset values (`FgInputToIni` etc.); localizing them requires decoupling display from persistence first.
