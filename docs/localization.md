# UI Localization (zh-Hans)

RHI ships with a runtime translation layer instead of per-string resource
files. English literals remain the single source of truth in code and XAML;
a dictionary maps them to Chinese at display time.

## How it works

- `RenoDXCommander/Loc.cs` — the entry point.
  - `Loc.Initialize()` is called once in the `App` constructor (right after
    `CrashReporter.Register`). It decides whether translation is active:
    - `%LocalAppData%\RHI\language.txt` containing `zh` (or `en`) forces the
      language and wins over auto-detection.
    - Otherwise translation turns on automatically when the Windows UI
      language is Chinese.
  - `Loc.Tr(string)` is a pure dictionary lookup: missing entry or disabled
    localisation returns the input unchanged, so untranslated strings simply
    stay English. Call it anywhere new UI text is created.
  - `Loc.Apply(Window)` walks a window's XAML-declared tree once (called in
    `MainWindow` and `SetupWindow` right after `InitializeComponent`) and
    translates TextBlock/Run text, string contents, TextBox headers and
    placeholders, tooltips, and the window title.
- `RenoDXCommander/Loc.ZhHans.cs` — the dictionary (823 entries). Keys are
  the exact English runtime strings.
- `UIFactory.MakeLabel / MakeActionButton / MakeStatusDot` wrap their
  text arguments with `Loc.Tr` internally, so all call sites inherit
  translation without changes.

Most C# UI literals are already wrapped as `Text = Loc.Tr("...")` etc. by
`tools/localize/wrap_literals.py`. The wrapping is intentionally narrow:

- Only plain string literals assigned to display properties
  (`Text`, `Content`, `Header`, `Title`, `PlaceholderText`, dialog button
  texts, `ToolTipService.SetToolTip` second argument, and plain-literal
  fragments of concatenations) are wrapped.
- Interpolated strings (`$"..."`), `Items.Add("...")` strings, and
  `ItemsSource` arrays are left alone — several combo-box vocabularies are
  mapped back to INI values by string comparison (see `SKIP_VALUES` in the
  script) and must stay English.
- ViewModel `Status`/`StatusMessage` strings are logic-coupled (compared in
  code) and are not translated.

## Known gaps (by design or pending)

- Interpolated multi-line dialog texts translate only their plain-literal
  fragments; the `$"..."` parts stay English. Mixed-language paragraphs can
  appear in a few long dialogs.
- Game-card status dot labels come from ViewModel state strings and remain
  English.
- Dynamic InfoBar/status messages bound via `{x:Bind}` are not translated.

## Maintenance workflow

```bash
# 1. Re-extract the current string inventory (no files modified)
python tools/localize/wrap_literals.py extract

# 2. Wrap any new display-property literals
python tools/localize/wrap_literals.py apply

# 3. Add translations for new keys to RenoDXCommander/Loc.ZhHans.cs,
#    then verify coverage / escapes / stale entries
python tools/localize/verify_dictionary.py

# 4. Optional: verify the wrapping is mechanically reversible
python tools/localize/verify_wraps.py
```

`verify_dictionary.py` decodes C# escapes on both sides, so `\n` in a key
must be written with the same escape the call site uses; entries whose value
equals the key (product names, links) are intentional.

## Rules for new UI code

- Prefer routing text through `UIFactory` helpers or `Loc.Tr("...")` at
  creation time; `Loc.Apply` only covers XAML declared at load.
- Never translate strings that are compared, parsed, or written to config
  files — those must stay byte-identical in both languages.
- The single-player/anti-cheat warning and any text surfaced by
  `CrashReporter.Log` should stay English for log readability.
