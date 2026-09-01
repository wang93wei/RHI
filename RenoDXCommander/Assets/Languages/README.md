# Languages

JSON-based i18n resource packs. `en-US.json` is the authoritative full set. Other languages fall back to `en-US` for missing keys.

## Files

- `en-US.json` — English (US), fallback, ~848 keys
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

## Fallback

`CurrentLanguage → en-US → key` — never throws. Missing keys are logged via `CrashReporter.Log` once per session.

## Coverage

`Coverage(lang)` = translated keys / total en-US keys. CI prints coverage; missing keys do not block build but should be translated for key user journeys.
