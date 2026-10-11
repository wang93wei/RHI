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

- `RenoDXCommander/` — main WinExe, `Services/`, `ViewModels/` (Main/GameCard/Filter/Settings + `*.{Capability}.cs` partials), `Models/` (DTOs), `Collections/`, `Controls/`, `Converters/`, `Themes/DarkTheme.xaml`, `Assets/` + UI builders and handlers (`DetailPanelBuilder*.cs`, `DragDropHandler*.cs`, `UIFactory.cs`, `HotkeyManager.cs`)
- `RenoDXCommander.Tests/` — xUnit + FsCheck. `CoreLogicTests.cs` and `PcgwServiceTests.cs` cover pure logic; `LocalizationTests.cs` injects catalogs through `SetCatalogForTesting`. `DropOverlayFormTests.cs` creates a Windows Forms overlay on an STA thread and checks its window style; the suite is not pure-logic only.
- `RHI.DropHelper/` — non-elevated helper for drag-drop when main app is elevated
- `tools/RHI-Stats/`, `tools/RHI-ManifestEditor/` — auxiliary tools
- `Directory.Build.props` defaults `Platform=x64` when `AnyCPU`; CI uses `x64` (`win-x64`)

## Build / Test / Publish

Run the following commands on a Windows runner. This macOS workspace cannot build or test WinUI. Do not install .NET locally or run local validation when the task requires CI-only verification.

```bash
dotnet build RenoDXCommander.sln -c Release -p:Platform=x64
dotnet test RenoDXCommander.Tests/RenoDXCommander.Tests.csproj -c Release -p:Platform=x64 --no-build
# Full publish (mirrors CI + publish.bat)
dotnet publish RenoDXCommander/RenoDXCommander.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:Platform=x64 --self-contained false -o publish/RHI
```
- CI consists of two independent, manual (`workflow_dispatch`) workflows on `windows-latest`:
  - `.github/workflows/build.yml` — **Build & Test**: i18n catalog check → Release x64 solution build → tests with `--no-build`.
  - `.github/workflows/package.yml` — **Package**: i18n catalog check → app publish and drop-helper build → assemble content and language packs → Inno Setup (`RHI Setup.iss`) and portable zip → `RHI-Setup` / `RHI-portable` artifacts. It does not run tests.
- Both workflows set up .NET 8 and cache `~/.nuget/packages`, with separate build/package keys. Keep this default path: the app copies the ClrMD DLL from `$(UserProfile)\.nuget\packages`. Independent setup, packaging and upload steps use `parallel` blocks.
- Single test: `dotnet test --filter "FullyQualifiedName~ResolveAutoReShadeFilename"` (xUnit filter).
- For CI-only tasks, dispatch both workflows on the current branch and report the commit SHA and run links. A successful build/test run does not establish package success or Windows UI behavior.
- `publish.bat` kills running `RHI.exe`/`RHI.DropHelper.exe` before publish and copies content files (`7z.exe/dll`, `*.ini`, `Assets/icons/*`, `OptiScaler*.ini`) next to single-file exe.

## Architecture Notes

- **DI**: `App.xaml.cs` registers services and VMs as singletons; `MainWindow` is transient. Shared `HttpClient` uses `SocketsHttpHandler` with `EnableMultipleHttp2Connections=true`, `MaxConnectionsPerServer=16`, `User-Agent:RHI/2.0`, `Timeout=10min`. Use `Lazy<T>` to break cycles. `IAuxFileService` reuses the `AuxInstallService` instance. Static helpers such as `ShortcutService` are not DI registrations.
- **Partial decomposition**: large services and VMs are split by capability, including `AuxInstallService`, `DlssPresetService`, `DlssStreamlineService`, `DxvkService`, `GameDetectionService`, `MainViewModel`, and `GameCardViewModel`. Suffix = capability (`.Detection`, `.Install`, `.Swap`, `.NvidiaProfile`, `.UI`), never `Part1`.
- **Models**: `Models/RemoteManifest.cs` (remote JSON, needs `[JsonPropertyName]`) and `Models/SavedGameLibrary.cs` (local persistence, keys are `GameName` or `GameName|Store` with `OrdinalIgnoreCase`). New manifest fields silently drop without the attribute.
- **VMs**: `CommunityToolkit.Mvvm`, `[ObservableProperty]` — never hand-write `INotifyPropertyChanged`.
- **Logging**: No Serilog/ILogger — use `Services/CrashReporter.Log("[Service] msg")` (ring buffer 300 + `%LocalAppData%\RHI\logs\` rotation 10). `VerboseLogging` switch in `SettingsViewModel`.
- **Entrypoint**: `App.xaml.cs` → `MainWindow.xaml` + `MainWindow.*.cs` + `ViewModels/MainViewModel*.cs`. UI builders also use capability partials such as `DetailPanelBuilder.Extras.cs`.
- **Auto HDR**: `IAutoHdrService` / `AutoHdrService` resolves the executable from user override → manifest override → largest executable. It enables Auto HDR through Direct3D registry entries and stores strength in per-executable GPU preferences. Disabling Auto HDR preserves unrelated GPU preference flags.
- **RTX Encore**: `RtxEncoreService` handles staging, proxy-name installation, ASI placement, updates and uninstall. Optional `nvngx_dlssnr.dll` must use `NrRequiredVer`; shared-file ownership is recorded in `RhiInstallManifest` so another component's DLL is not removed.
- **Shortcuts**: static `ShortcutService` creates desktop `.lnk` files that run `RHI.exe --launch "GameName"`. It resolves executable overrides and selects game, Xbox artwork or RHI fallback icons.
- **Manifest paths**: `RemoteManifest.DlssPathOverrides` (`dlssPathOverrides`) and `StreamlinePathOverrides` (`streamlinePathOverrides`) are separate relative paths from the game root. `LaunchExeOverrides` (`launchExeOverrides`) controls executable selection. Preserve all three when merging detection and launch code.
- **i18n**: `ILocalizationService`/`LocalizationService` (singleton, `App.xaml.cs`) with `LanguageChanged` event for live switching. Read `.trellis/spec/app/i18n.md` before touching any user-visible text.

## Internationalization (i18n)

- **Catalogs**: `RenoDXCommander/Assets/Languages/{en-US,zh-CN,zh-TW,ja-JP,ko-KR}.json` — flat `key -> value`, packaged as `Content`. `en-US.json` is the authoritative baseline; every other language must have the same keys present (1:1). Keys must be unique **case-insensitively** per file — duplicates break the runtime catalog loader and fail CI (`28cc1ee`).
- **Key naming**: `Area.Component.Key` with `.Tooltip` / `.Placeholder` / `.Button` suffixes, placeholders as `{0}` (`string.Format`), e.g. `Dialog.UnknownDxgi.Content`.
- **XAML**: `Text="{Binding [Settings.Title], Source={StaticResource Loc}}"` (indexer binding). `StaticResource Loc` is a design-time placeholder in `App.xaml` replaced with the singleton in `App.xaml.cs` before `MainWindow` loads. Never `x:Bind` and never hard-code user-visible text (`Text="Settings"` fails the coverage scan).
- **C#**: `ILocalizationService.GetString(key, args)` — fallback is current language → en-US → raw key, never throws; missing key logs once via `CrashReporter`. `LocConverter` (`Converters/LocConverter.cs`) for `ConverterParameter` cases.
- **Protocol values**: localize displayed labels only. Preserve command arguments (`--launch`), DLL names, registry keys, INI values, manifest fields, component IDs and persisted driver values such as `Default`, `On`, and `Off`. Use stable `Tag` values for selectors; do not use translated `Content` as a value for lookup, comparison or persistence. Keep format placeholders consistent across all five catalogs.
- **Persistence**: `settings.json` key `Language`, values `System|en-US|zh-CN|zh-TW|ja-JP|ko-KR`, default `System`; `ResolveSystemLanguage()` maps aliases/prefixes (`zh-Hans→zh-CN`, `en-GB→en-US` …) → `en-US`.
- **Validation**: both CI workflows run `python tools/check-i18n-coverage.py --strict`. It rejects duplicate keys and, in strict mode, missing/invalid catalogs or coverage below 50%. It reports untranslated values and hard-coded strings in `MainWindow.xaml`; this report does not enforce full key parity, placeholder parity or C# coverage. Review these requirements separately. Whitelisted fragments include `RHI`, `by `, `Licence`, `github.com`, `▶`/`↺`.
- **Tests**: `RenoDXCommander.Tests/LocalizationTests.cs` — `dotnet test --filter Localization`.

## Spec & Workflow

- Read `.trellis/spec/app/index.md` and the relevant guide before coding: `directory-structure.md`, `service-patterns.md`, `viewmodel-patterns.md`, `error-handling.md`, `logging-guidelines.md`, `quality-guidelines.md`, `i18n.md` (mandatory before adding/changing any user-visible text).
- Trellis phases: `workflow.md` — Plan (`prd.md`/`design.md`/`implement.md`) → Execute (only after `task.py start` → `in_progress`) → Finish (verify → spec update → commit).
- Before-dev: `trellis-before-dev`; after-edit check: `trellis-check` (available under `.zcode/skills/`, `.agents/skills/`, `.opencode/skills/`).

## Conventions & Gotchas

- Interface `I{Name}Service` + `NameService` in `RenoDXCommander.Services`; small services may skip interface (e.g. `DofFixService`).
- New service must be registered in `App.xaml.cs` or `GetRequiredService` throws at runtime.
- `InternalsVisibleTo RenoDXCommander.Tests` — tests can access `internal` (e.g. `ResolveAutoReShadeFilename`).
- Keep the direct `Microsoft.WindowsAppSDK` versions in the app and test projects equal during upstream SDK updates. Preserve the test reference's `ExcludeAssets="buildTransitive"`; a stale direct version can cause `NU1605` during restore.
- File ops must be `try/catch` + `CrashReporter.Log` + `continue` — single file failure must not abort batch.
- `SavedGameLibrary` collections need `StringComparer.OrdinalIgnoreCase`; `GameDetectionService` must reuse `MaxScanDepth=4` / `_engineCache`.
- `app.manifest` is `asInvoker` (admin via Task Scheduler, not manifest). `NoWarn NU1902` is suppressed.
- `.gitignore` excludes `bin/obj`, `.vs`, `publish.bat` (local path), `tools/*.md`.
- No hard-coded user-visible strings in XAML (`Text="..."`) or C# (dialog titles/status text) — always use Loc keys. Adding a key requires all five language catalogs, even when the CI coverage threshold would still pass.
