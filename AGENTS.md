<!-- TRELLIS:START -->
# Trellis Instructions

These instructions are for AI assistants working in this project.

This project is managed by Trellis. The working knowledge you need lives under `.trellis/`:

- `.trellis/workflow.md` — development phases, when to create tasks, skill routing
- `.trellis/spec/` — package- and layer-scoped coding guidelines (read before writing code in a given layer)
- `.trellis/workspace/` — per-developer journals and session traces
- `.trellis/tasks/` — active and archived tasks (PRDs, research, jsonl context)

If a Trellis command is available on your platform (e.g. `/trellis:finish-work`, `/trellis:continue`), prefer it over manual steps. Not every platform exposes every command.

If you're using Codex or another agent-capable tool, additional project-scoped helpers may live in:
- `.agents/skills/` — reusable Trellis skills
- `.codex/agents/` — optional custom subagents

Managed by Trellis. Edits outside this block are preserved; edits inside may be overwritten by a future `trellis update`.

<!-- TRELLIS:END -->

# RHI — Agent Guide

Windows-only desktop app (WinUI 3, .NET 8, `net8.0-windows10.0.19041.0`). AssemblyName `RHI`, `AllowUnsafeBlocks=true`, `Nullable enable`, `ImplicitUsings enable`. No `opencode.json` at root — opencode config lives in `.opencode/`. ZCode platform config (SessionStart/UserPromptSubmit/PreToolUse hooks + trellis skills) lives in `.zcode/`; Trellis skills are mirrored across `.zcode/skills/`, `.agents/skills/`, `.opencode/skills/` — edit the source once via Trellis, not the mirrors.

## Solution Layout

- `RenoDXCommander/` — main WinExe (`RenoDXCommander.csproj:1-24`), ~125 `Services/*.cs`, `ViewModels/` (Main/GameCard/Filter/Settings + `*.{Capability}.cs` partials), `Models/` (pure DTOs), `Collections/`, `Controls/`, `Converters/`, `Themes/DarkTheme.xaml`, `Assets/` + loose handlers (`DetailPanelBuilder*.cs`, `DragDropHandler*.cs`, `UIFactory.cs`, `HotkeyManager.cs`)
- `RenoDXCommander.Tests/` — xUnit, pure-logic only (no FS/network/DI): `CoreLogicTests.cs` + `LocalizationTests.cs` (catalogs injected in-memory via `SetCatalogForTesting`)
- `RHI.DropHelper/` — non-elevated helper for drag-drop when main app is elevated
- `tools/RHI-Stats/`, `tools/RHI-ManifestEditor/` — auxiliary tools
- `Directory.Build.props:2` defaults `Platform=x64` when `AnyCPU`; primary platform is `x64` (`win-x64`)

## Build / Test / Publish

```bash
dotnet build RenoDXCommander.sln -c Release -p:Platform=x64
dotnet test RenoDXCommander.Tests/RenoDXCommander.Tests.csproj -c Release -p:Platform=x64 --no-build
# Full publish (mirrors CI + publish.bat)
dotnet publish RenoDXCommander/RenoDXCommander.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:Platform=x64 --self-contained false -o publish/RHI
```
- CI: `.github/workflows/build.yml` — `workflow_dispatch` only, `windows-latest`, build → test → publish → Inno Setup (`RHI Setup.iss`) → artifacts (installer + portable zip).
- Single test: `dotnet test --filter "FullyQualifiedName~ResolveAutoReShadeFilename"` (xUnit filter).
- On macOS dev machine, `net8.0-windows10.0.19041.0` with `UseWinUI=true` cannot build/test locally — requires Windows runner/VM.
- `publish.bat` kills running `RHI.exe`/`RHI.DropHelper.exe` before publish and copies content files (`7z.exe/dll`, `*.ini`, `Assets/icons/*`, `OptiScaler*.ini`) next to single-file exe.

## Architecture Notes

- **DI**: `App.xaml.cs:26-140` registers everything as `AddSingleton` (except `MainWindow:Transient`, `HttpClient:Singleton` with `SocketsHttpHandler` — `EnableMultipleHttp2Connections=true`, `MaxConnectionsPerServer=16`, `User-Agent:RHI/2.0`, `Timeout=10min`). Use `Lazy<T>` to break cycles. `IAuxFileService` reuses `AuxInstallService` instance.
- **Partial decomposition** is mandatory for large classes: `AuxInstallService` (5 files), `DlssPresetService` (6), `DxvkService` (4), `GameDetectionService` (4), `MainViewModel` (13), `GameCardViewModel` (11). Suffix = capability (`.Detection`, `.Install`, `.Swap`), never `Part1`.
- **Models**: `Models/RemoteManifest.cs` (remote JSON, needs `[JsonPropertyName]`) and `Models/SavedGameLibrary.cs` (local persistence, keys are `GameName` or `GameName|Store` with `OrdinalIgnoreCase`). New manifest fields silently drop without the attribute.
- **VMs**: `CommunityToolkit.Mvvm`, `[ObservableProperty]` — never hand-write `INotifyPropertyChanged`.
- **Logging**: No Serilog/ILogger — use `Services/CrashReporter.Log("[Service] msg")` (ring buffer 300 + `%LocalAppData%\RHI\logs\` rotation 10). `VerboseLogging` switch in `SettingsViewModel`.
- **Entrypoint**: `App.xaml.cs` → `MainWindow.xaml` + `MainWindow.*.cs` (5 partials) + `ViewModels/MainViewModel*.cs`.
- **i18n**: `ILocalizationService`/`LocalizationService` (singleton, `App.xaml.cs`) with `LanguageChanged` event for live switching. Read `.trellis/spec/app/i18n.md` before touching any user-visible text.

## Internationalization (i18n)

- **Catalogs**: `RenoDXCommander/Assets/Languages/{en-US,zh-CN,zh-TW,ja-JP,ko-KR}.json` — flat `key -> value`, packaged as `Content`. `en-US.json` is the authoritative baseline; every other language must have the same keys present (1:1). Keys must be unique **case-insensitively** per file — duplicates break the runtime catalog loader and fail CI (`28cc1ee`).
- **Key naming**: `Area.Component.Key` with `.Tooltip` / `.Placeholder` / `.Button` suffixes, placeholders as `{0}` (`string.Format`), e.g. `Dialog.UnknownDxgi.Content`.
- **XAML**: `Text="{Binding [Settings.Title], Source={StaticResource Loc}}"` (indexer binding). `StaticResource Loc` is a design-time placeholder in `App.xaml` replaced with the singleton in `App.xaml.cs` before `MainWindow` loads. Never `x:Bind` and never hard-code user-visible text (`Text="Settings"` fails the coverage scan).
- **C#**: `ILocalizationService.GetString(key, args)` — fallback is current language → en-US → raw key, never throws; missing key logs once via `CrashReporter`. `LocConverter` (`Converters/LocConverter.cs`) for `ConverterParameter` cases.
- **Persistence**: `settings.json` key `Language`, values `System|en-US|zh-CN|zh-TW|ja-JP|ko-KR`, default `System`; `ResolveSystemLanguage()` maps aliases/prefixes (`zh-Hans→zh-CN`, `en-GB→en-US` …) → `en-US`.
- **Validation**: CI runs `python tools/check-i18n-coverage.py --strict` (`.github/workflows/build.yml`) — exits 1 on case-insensitive duplicate keys, missing files, or any language < 50% coverage; also reports hard-coded XAML strings (whitelist: `RHI`, `by `, `Licence`, `github.com`, `▶`/`↺` etc.).
- **Tests**: `RenoDXCommander.Tests/LocalizationTests.cs` — `dotnet test --filter Localization`.

## Spec & Workflow

- Read `.trellis/spec/app/index.md` and the relevant guide before coding: `directory-structure.md`, `service-patterns.md`, `viewmodel-patterns.md`, `error-handling.md`, `logging-guidelines.md`, `quality-guidelines.md`, `i18n.md` (mandatory before adding/changing any user-visible text).
- Trellis phases: `workflow.md` — Plan (`prd.md`/`design.md`/`implement.md`) → Execute (only after `task.py start` → `in_progress`) → Finish (verify → spec update → commit).
- Before-dev: `trellis-before-dev`; after-edit check: `trellis-check` (available under `.zcode/skills/`, `.agents/skills/`, `.opencode/skills/`).

## Conventions & Gotchas

- Interface `I{Name}Service` + `NameService` in `RenoDXCommander.Services`; small services may skip interface (e.g. `DofFixService`).
- New service must be registered in `App.xaml.cs` or `GetRequiredService` throws at runtime.
- `InternalsVisibleTo RenoDXCommander.Tests` — tests can access `internal` (e.g. `ResolveAutoReShadeFilename`).
- File ops must be `try/catch` + `CrashReporter.Log` + `continue` — single file failure must not abort batch.
- `SavedGameLibrary` collections need `StringComparer.OrdinalIgnoreCase`; `GameDetectionService` must reuse `MaxScanDepth=4` / `_engineCache`.
- `app.manifest` is `asInvoker` (admin via Task Scheduler, not manifest). `NoWarn NU1902` is suppressed.
- `.gitignore` excludes `bin/obj`, `.vs`, `publish.bat` (local path), `tools/*.md`.
- No hard-coded user-visible strings in XAML (`Text="..."`) or C# (dialog titles/status text) — always Loc keys (see i18n section); adding a new key means updating all 5 language JSONs or CI coverage check fails.
