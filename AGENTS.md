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

# RHI — ReShade HDR Installer

WinUI 3 desktop app (.NET 8, `net8.0-windows10.0.19041.0`, C#, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection) that detects a user's game library across 8 storefronts and installs/manages ReShade, RenoDX/Luma HDR mods, DLSS/Streamline DLLs, OptiScaler, ReLimiter/Display Commander, RE Framework, DXVK and NVIDIA driver profiles. AssemblyName `RHI`, `AllowUnsafeBlocks=true`, `Nullable enable`, `ImplicitUsings enable`. The project folder is still named `RenoDXCommander` (old name); the shipped exe is `RHI.exe`. Upstream is maintained by a single developer (RankFTW) with frequent releases and a large Discord/Nexus user base; this clone is the `wang93wei/RHI` fork.

`CLAUDE.md` is a symlink to this file — this file is canonical; edit here only.

Workspace-specific config: no `opencode.json` at root — opencode config lives in `.opencode/`. ZCode platform config (SessionStart/UserPromptSubmit/PreToolUse hooks + trellis skills) lives in `.zcode/`; Trellis skills are mirrored across `.zcode/skills/`, `.agents/skills/`, `.opencode/skills/` — edit the source once via Trellis, not the mirrors.

## Where knowledge lives

- **This file**: core rules and workflow. Always loaded.
- **`.claude/rules/*.md`**: topic knowledge (DLSS/NVIDIA, DXVK, install/update, manifest, ReShade, UI). Loaded automatically when working on matching files. Copied from `.kiro/steering/`; keep both in sync while Kiro is still used.
- **`.kiro/steering/rhi-knowledge.md`**: large (~230 KB) history of architecture decisions, past bugs and fixes, and pitfalls. Too big to load every session. **Before changing a feature, grep it for the feature/class name** (`grep -n "^##" .kiro/steering/rhi-knowledge.md` lists sections). Many bugs there have already been fixed once; don't reintroduce them.
- **`.kiro/specs/<feature>/`**: requirements/design/tasks for past features.
- **`.trellis/spec/`**: Trellis coding guidelines; entry point `.trellis/spec/app/index.md`.
- **`docs/code-health-plan.md`**: the audit's to-do list (safety fixes, background speed-ups, dead code, refactors, tooling). Tick items off there as they're done.
- When you learn something durable (a pitfall, a pattern, a root cause), add it to the relevant rules file or the knowledge doc, not here, unless it's a core rule.

## Solution Layout

- `RenoDXCommander/` — main WinExe. `Services/` (all real work; big services are partial classes, most have an `I*` interface registered in DI), `ViewModels/` (`MainViewModel.*.cs` — Init, Install, Update, Settings, Dxvk, GameMatching, BuildCards, CacheLoad, BackgroundScan… — and `GameCardViewModel.*.cs`, one per feature), `Models/` (records/enums, `*InstalledRecord.cs`, `GameKey`), `Collections/`, `Controls/`, `Converters/`, `Themes/DarkTheme.xaml`, `Assets/`, plus UI builders and handlers (`DetailPanelBuilder*.cs`, `*PopupHelper.cs`, `*Dialog.cs`, `DragDropHandler*.cs`, `UIFactory.cs`, `HotkeyManager.cs`), and `MainWindow.xaml` + `MainWindow.*.cs`.
- **UI is built in C# code, not XAML templates.** There is one overrides builder (`DetailPanelBuilder`) and one layout only (Detail; Compact view was removed): game list + detail panel with 7 collapsible, reorderable sections (see `.claude/rules/rhi-ui.md`).
- Root `.ini` files are templates shipped beside the exe.
- `RenoDXCommander.Tests/` — xUnit + FsCheck. `CoreLogicTests.cs` and `PcgwServiceTests.cs` cover pure logic; `LocalizationTests.cs` injects catalogs through `SetCatalogForTesting`. `DropOverlayFormTests.cs` creates a Windows Forms overlay on an STA thread and checks its window style; the suite is not pure-logic only.
- `RHI.DropHelper/` — non-elevated helper for drag-drop when main app is elevated.
- `tools/` — `RHI-Stats/`, `RHI-ManifestEditor/`, `PCGW-Editor/`, `RenoDXdb-Editor/`, Python/PowerShell scripts. `game-db/` — community DB + merge scripts. `docs/` — guides, research, handovers. `database/`, `engine-files/`, `submissions/`.
- Gitignored reference copies: `Luma/`, `OptiScaler-DLSSNR-v*/`, `nvidiaProfileInspector-master/`.
- `Directory.Build.props` defaults `Platform=x64` when `AnyCPU`; CI uses `x64` (`win-x64`).

## Build / Test / Publish

**After every code change on the Windows machine, run `publish.bat`.** That is the build the user actually runs (`C:\Users\Mark\OneDrive\Documents\RDXC\Publish\RHI\RHI.exe`); plain `dotnet build` output is not the running app. `publish.bat` force-closes any running RHI first.

```bash
& "g:\RDXC\publish.bat"                                                          # build + deploy (Release, win-x64, single file)
dotnet build RenoDXCommander/RenoDXCommander.csproj -p:Platform=x64 -v q         # quick compile check
dotnet test  RenoDXCommander.Tests/RenoDXCommander.Tests.csproj -p:Platform=x64  # tests (xUnit + FsCheck)

# CI-equivalent commands (also what the GitHub workflows run)
dotnet build RenoDXCommander.sln -c Release -p:Platform=x64
dotnet test RenoDXCommander.Tests/RenoDXCommander.Tests.csproj -c Release -p:Platform=x64 --no-build
dotnet publish RenoDXCommander/RenoDXCommander.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:Platform=x64 --self-contained false -o publish/RHI
```

- Always use platform x64. Never add `--no-restore` to the publish step.
- `publish.bat` kills running `RHI.exe`/`RHI.DropHelper.exe` before publish, then copies loose content files (ini templates, 7z, icons, ReShade.ini, patch notes, ClrMD DLL) next to the single-file exe. **A new file the app reads from its install folder must be added to `publish.bat`**, or it's missing in release.
- Keep the default NuGet path: the app copies the ClrMD DLL from `$(UserProfile)\.nuget\packages`.
- `"RHI Setup.iss"` is the Inno Setup installer script (run after publish, by the user).
- **XAML constraint**: don't add new named elements (`x:Name`) or new controls to `MainWindow.xaml` unless unavoidable. The publish pipeline has generated duplicate `MainWindow.g.i.cs` (CS0102). Build new UI in C# (see `SettingsHandler.cs`). Adding `x:Name` to an existing element is safe.
- The test project's `Microsoft.WindowsAppSDK` version must match the app's (currently 2.5.1), or restore fails with NU1605. Preserve the test reference's `ExcludeAssets="buildTransitive"`.
- The app has ~190 nullable warnings. Don't add new ones.
- Single test: `dotnet test --filter "FullyQualifiedName~ResolveAutoReShadeFilename"` (xUnit filter).

**This macOS workspace cannot build or test WinUI.** Do not install .NET locally, and do not run local validation when the task requires CI-only verification. Use the two independent, manual (`workflow_dispatch`) workflows on `windows-latest`:

- `.github/workflows/build.yml` — **Build & Test**: i18n catalog check → Release x64 solution build → tests with `--no-build`.
- `.github/workflows/package.yml` — **Package**: i18n catalog check → app publish and drop-helper build → assemble content and language packs → Inno Setup (`RHI Setup.iss`) and portable zip → `RHI-Setup` / `RHI-portable` artifacts. It does not run tests.
- Both set up .NET 8 and cache `~/.nuget/packages`, with separate build/package keys. Independent setup, packaging and upload steps use `parallel` blocks.
- For CI-only tasks, dispatch both workflows on the current branch and report the commit SHA and run links. A successful build/test run does not establish package success or Windows UI behavior.

## Git, versions, patch notes

- Single developer, pushes straight to `main`. **Always ask before committing or pushing.**
- **Never bump the version** (`AssemblyVersion`/`FileVersion` in `RenoDXCommander.csproj`). The user does it manually right before a release.
- Commit subjects: short, prefixed: `Fix:`, `Feat:`, `Changes:`, `Manifest:`, `DLSS manifest:`, `MOTD:`, `Release vX.Y.Z`.
- Remotes in this clone: `upstream` = RankFTW/RHI (canonical, formerly RankFTW/RenoDXChecker), `origin` = wang93wei/RHI (this fork). Pushing to `upstream/main` ships to every user — ask first. `lazorr410` is another contributor fork; never push there.
- **Patch notes (`RenoDXCommander/RHI_PatchNotes.md`) are updated in the same turn as the change, without being asked**, for every bug fix, feature, UI change and manifest change.
  - Sections: `### New` (genuinely new features; drop the section if nothing qualifies), `### Changes` (improvements to existing features), `### Bug Fixes`, `### Maintenance` (internal/infrastructure), `### Manifest Updates`.
  - Don't list fixes for features introduced in the same version.
  - No teaser language ("groundwork for…", "prepares for…").
  - Internal changes are framed as bug fixes ("Fixed X showing incorrectly") unless users will notice and appreciate them.

## Live data files — pushing to `main` is a deploy

The running app fetches these from GitHub `main` for every user:

| File | Purpose |
|---|---|
| `manifest.json` | Blacklist, name mappings, per-game overrides, install warnings, addon packs, feature flags. Field reference: `docs/manifest-field-reference.md` and `.claude/rules/rhi-manifest.md` |
| `dlss_manifest.json` | DLSS / DLSSD / DLSSG / Streamline / NR versions and download URLs |
| `optiscaler_presets.json` | Recommended presets in the OptiScaler cog. Built-in copies in `OsPresetService.BuiltInPresets`: keep them in sync |
| `motd.md` | Message of the day |
| `database/pcgw_data.json` | PCGamingWiki data (~5 MB). Regenerated by `tools/PCGW-Editor`; never hand-edit |

- Before committing, run `python tools/check-manifest.py` (valid JSON, duplicate keys, case collisions). A GitHub Action runs it on push too. Make targeted edits; never reformat or re-sort. Keep CRLF line endings (`sed -i` strips them; check `git diff --stat` shows only your lines).
- No duplicate keys, and **no keys that differ only by case in the fields `ManifestService.Normalize()` makes case-insensitive**: it throws and the app silently falls back to the stale cached manifest. Other fields (e.g. `engineHintOverrides`, `pdUpscalerGames`) are looked up exactly, so they may need both spellings of a name.
- Game names must match the detected name exactly (Ubisoft uses folder names, e.g. `Watch_Dogs2`).

**"Push manifest" workflow**: (0) `python tools/check-manifest.py`; (1) `git diff manifest.json`; (2) pick out user-facing changes; (3) add them to the current version's `### Manifest Updates` in `RHI_PatchNotes.md` (skip housekeeping like re-ordering or trailing commas); (4) commit and push `manifest.json` and `RHI_PatchNotes.md` together (still ask first).

Other databases (`RenoDXdb*.json`, `engine-files/`) live in the separate `RankFTW/rhi-repo` repository.

## Architecture Notes

- **DI**: `App.xaml.cs` registers services and VMs as singletons; `MainWindow` is transient. Shared `HttpClient` uses `SocketsHttpHandler` with `EnableMultipleHttp2Connections=true`, `MaxConnectionsPerServer=16`, `User-Agent:RHI/2.0`, `Timeout=10min`. Use `Lazy<T>` to break cycles. `IAuxFileService` reuses the `AuxInstallService` instance. Static helpers such as `ShortcutService` are not DI registrations. `AddonPackService` is not in DI — use `ViewModel.AddonPackServiceInstance`.
- **Partial decomposition**: large services and VMs are split by capability, including `AuxInstallService`, `DlssPresetService`, `DlssStreamlineService`, `DxvkService`, `GameDetectionService`, `MainViewModel`, and `GameCardViewModel`. Suffix = capability (`.Detection`, `.Install`, `.Swap`, `.NvidiaProfile`, `.UI`), never `Part1`; target no source file over 50 KB.
- **Models**: `Models/RemoteManifest.cs` (remote JSON, needs `[JsonPropertyName]`) and `Models/SavedGameLibrary.cs` (local persistence, keys are `GameName` or `GameName|Store` with `OrdinalIgnoreCase`). New manifest fields silently drop without the attribute.
- **VMs**: `CommunityToolkit.Mvvm`, `[ObservableProperty]` — never hand-write `INotifyPropertyChanged`.
- **Logging**: No Serilog/ILogger — use `Services/CrashReporter.Log("[Class.Method] msg")` (ring buffer 300 + `%LocalAppData%\RHI\logs\` rotation 10). `VerboseLogging` switch in `SettingsViewModel`.
- **Entrypoint**: `App.xaml.cs` → `MainWindow.xaml` + `MainWindow.*.cs` + `ViewModels/MainViewModel*.cs`. UI builders also use capability partials such as `DetailPanelBuilder.Extras.cs`.
- **Auto HDR**: `IAutoHdrService` / `AutoHdrService` resolves the executable from user override → manifest override → largest executable. It enables Auto HDR through Direct3D registry entries and stores strength in per-executable GPU preferences. Disabling Auto HDR preserves unrelated GPU preference flags.
- **RTX Encore**: `RtxEncoreService` handles staging, proxy-name installation, ASI placement, updates and uninstall. Optional `nvngx_dlssnr.dll` must use `NrRequiredVer`; shared-file ownership is recorded in `RhiInstallManifest` so another component's DLL is not removed.
- **Shortcuts**: static `ShortcutService` creates desktop `.lnk` files that run `RHI.exe --launch "GameName"`. It resolves executable overrides and selects game, Xbox artwork or RHI fallback icons.
- **Manifest paths**: `RemoteManifest.DlssPathOverrides` (`dlssPathOverrides`) and `StreamlinePathOverrides` (`streamlinePathOverrides`) are separate relative paths from the game root. `LaunchExeOverrides` (`launchExeOverrides`) controls executable selection. Preserve all three when merging detection and launch code.
- **i18n**: `ILocalizationService`/`LocalizationService` (singleton, `App.xaml.cs`) with `LanguageChanged` event for live switching. Read `.trellis/spec/app/i18n.md` before touching any user-visible text.

## Conventions

**Code style**

- Log significant operations with `CrashReporter.Log("[ClassName.MethodName] …")`.
- `StringComparison.OrdinalIgnoreCase` for all game-name and path comparisons. Normalize `/` to `\` before comparing paths.
- UI updates via `DispatcherQueue?.TryEnqueue(() => …)`. Never block the UI thread on NVAPI or file/HTTP work; use `await Task.Run(...)`.
- Settings persist via `SaveNameMappings()` after every change (it debounces 250 ms so bursts of changes write once; don't add other deferred or batched saves). Boolean settings persist both states explicitly.
- No new emojis in UI text (existing ⚙ 📋 ⚡ stay). Plain text for new UI.
- Use the shared DI `HttpClient`. Use `Path.GetDirectoryName(Environment.ProcessPath)`, not `AppContext.BaseDirectory` (wrong for single-file publish).
- Background work respects `_window.LifetimeToken` / `IsShuttingDown`. Progress dialogs use `DialogCoordinator` (`ShowProgressAsync` + `await using`).
- Don't add empty `catch {}`. Catch specific exceptions and log them. Avoid new `async void` except UI event handlers, and wrap those in try/catch.
- File ops must be `try/catch` + `CrashReporter.Log` + `continue` — a single file failure must not abort a batch.
- File splits: `FileName.SectionName.cs` partials; target no source file over 50 KB.

**Naming**: internal string values are PascalCase without spaces (`"LiliumHdr"`); display labels are human-readable (`"Lilium HDR"`); fields `_camelCase`. Interface `I{Name}Service` + `NameService` in `RenoDXCommander.Services`; small services may skip the interface (e.g. `DofFixService`). A new service must be registered in `App.xaml.cs`, or `GetRequiredService` throws at runtime.

**Per-game override (all 10 steps)**: (1) field in `GameNameService.cs`; (2) public accessor; (3) `LoadNameMappings()`/`SaveNameMappings()`; (4) `RenameGame()` migration (`MigrateDict`/`MigrateHashSet` or composite variants); (5) `IGameNameService.cs`; (6) expose in `MainViewModel.cs`; (7) Get/Set/Resolve in `MainViewModel.Settings.cs`; (8) UI in `DetailPanelBuilder.Overrides.cs`; (9) reset overrides handler; (10) `GameReportEncoder.cs`.

**Composite keys**: per-store settings use `"GameName|Store"` via `GameKey.FromCard(name, source).ToKey()` / `GameKey.From(name, store).ToKey()`, with an optional `string store = ""` parameter on Get/Set methods. Legacy keys migrate by appending `|`. Name-only exceptions: wiki exclusions, UE-Extended opt-in/out, RTX HDR. `SavedGameLibrary` collections need `StringComparer.OrdinalIgnoreCase`.

**Staging new downloadable variants**: separate staging folder and `version.txt` per variant; only the globally selected variant downloads at startup; others on demand; `EnsureStagingAsync()` is a no-op when valid and no update.

**Shader/addon modes**: shader `"Global"|"Custom"|"Select"|"Off"`; addon `"Global"|"Select"|"Off"`; both disabled under the "No Addons" RS channel (`card.UseNormalReShade`).

**Dual init paths**: logic that must run on launch and on Refresh goes in both `RunBackgroundScanAndMergeAsync` and the full-scan path of `InitializeAsync` (Refresh uses `InitializeAsync(forceRescan: true)`). Anything `BuildCards` sets from the manifest must also be set in `CacheLoad` (using `cachedManifest`, not `_manifest`).

## Safety rules for writing into game folders

This app writes DLLs and ini files into users' game installs; mistakes break games.

- Back up before overwriting. For components that may or may not replace an existing file, use the **`.original` sentinel pattern** (non-empty = restore on uninstall; 0-byte = RHI-owned, delete; absent = leave alone). See `rhi-knowledge.md`.
- OptiScaler: `BackupOriginalIfExists` before every root-file copy in `InstallAsync`.
- Never write into `C:\Program Files\WindowsApps\...` (Game Pass, access-denied). Detect and tell the user. WindowsApps paths change version segments on game update; don't rely on exact stored paths.
- `C:\ProgramData\ReShade\` needs admin: use `CopyFileWithElevation()`.
- Check folder exists/writable before copying; surface failures instead of throwing. Expect `UnauthorizedAccessException` on restrictive ACLs.
- Respect the foreign-DLL warning (DXVK, Special K, ENB) and `RsIniLockedGames` (any new "apply to all" reshade.ini loop must check `GetKeepRsIniUpdated`).
- Never overwrite user-edited ini templates. Update All must not override a user's explicit per-game toggles.
- After deploying DLSS/Streamline files, call `_dlssStreamlineService.RecordDlssFound(gameName)` so the skip cache doesn't hide them.
- Any relative path that comes from an archive entry, a saved record or remote data (manifest, wiki) must go through `FileHelper.SafeCombine(baseDir, rel)` (returns null if it escapes `baseDir`), never plain `Path.Combine`. `ZipFile.ExtractToDirectory` is already safe.
- Never put the GitHub token in `HttpClient.DefaultRequestHeaders` or add it per request: `GitHubTokenHandler` (wrapping the shared client) attaches it to `api.github.com` only (download hosts return 404 for a token they don't accept). Set it via `GitHubAuthService.ApplyTokenToHttpClient`.

## Internationalization (i18n)

- **Catalogs**: `RenoDXCommander/Assets/Languages/{en-US,zh-CN,zh-TW,ja-JP,ko-KR}.json` — flat `key -> value`, packaged as `Content`. `en-US.json` is the authoritative baseline; every other language must have the same keys present (1:1). Keys must be unique **case-insensitively** per file — duplicates break the runtime catalog loader and fail CI (`28cc1ee`).
- **Key naming**: `Area.Component.Key` with `.Tooltip` / `.Placeholder` / `.Button` suffixes, placeholders as `{0}` (`string.Format`), e.g. `Dialog.UnknownDxgi.Content`.
- **XAML**: `Text="{Binding [Settings.Title], Source={StaticResource Loc}}"` (indexer binding). `StaticResource Loc` is a design-time placeholder in `App.xaml` replaced with the singleton in `App.xaml.cs` before `MainWindow` loads. Never `x:Bind` and never hard-code user-visible text (`Text="Settings"` fails the coverage scan).
- **C#**: `ILocalizationService.GetString(key, args)` — fallback is current language → en-US → raw key, never throws; missing key logs once via `CrashReporter`. `LocConverter` (`Converters/LocConverter.cs`) for `ConverterParameter` cases.
- **Protocol values**: localize displayed labels only. Preserve command arguments (`--launch`), DLL names, registry keys, INI values, manifest fields, component IDs and persisted driver values such as `Default`, `On`, and `Off`. Use stable `Tag` values for selectors; do not use translated `Content` as a value for lookup, comparison or persistence. Keep format placeholders consistent across all five catalogs.
- **Persistence**: `settings.json` key `Language`, values `System|en-US|zh-CN|zh-TW|ja-JP|ko-KR`, default `System`; `ResolveSystemLanguage()` maps aliases/prefixes (`zh-Hans→zh-CN`, `en-GB→en-US` …) → `en-US`.
- **Validation**: both CI workflows run `python tools/check-i18n-coverage.py --strict`. Duplicate keys fail in all modes. Strict mode also rejects missing/invalid catalogs, missing/extra keys, empty/non-string values and differing format-index counts. Escaped braces are literal text. Untranslated values and hard-coded strings in `MainWindow.xaml` remain report-only; C# coverage requires source review. Build & Test runs the catalog gate regression tests on the Windows runner. Whitelisted fragments include `RHI`, `by `, `Licence`, `github.com`, `▶`/`↺`.
- **Tests**: `RenoDXCommander.Tests/LocalizationTests.cs` — `dotnet test --filter Localization`.

## Debugging approach

- **Logs first.** When asked to check logs: copy them into the workspace, find the newest session log, and search it for the thing the user is reporting before reading any code:
  ```powershell
  Copy-Item "$env:LOCALAPPDATA\RHI\Logs\*" "g:\RDXC\Logs\" -Force
  Get-ChildItem "g:\RDXC\Logs\session_*.txt" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  ```
  A 404, permission error or "Failed"/"Skipping" line is often the whole answer. Crash reports (`crash_*.txt`) include a stack trace plus recent activity. `Logs/<username>/` folders are logs sent by users.
- Don't assume the root cause; gather evidence. **Ask clarifying questions before extensive debugging changes.** One change at a time; ask the user to test and share logs.
- UI freeze investigation: see the "UI Freeze Investigation" section of `rhi-knowledge.md` before touching locks, layouts, panel code or NVAPI reads.

## Common pitfalls

More in `.claude/rules/` and `rhi-knowledge.md`.

- GitHub API limit 60 req/hour: minimize calls, use ETag caching. `nightly.link` HEAD is unreliable; compare Content-Length.
- Vulkan games share global ReShade state: per-game overrides must propagate to all Vulkan games.
- `[RelayCommand]` doesn't support optional parameters.
- WinUI 3: an element can have only one parent (`COMException: No installed components were detected`); no nested ContentDialogs; `Button` has no `ToolTip` property (use `ToolTipService.SetToolTip`); use Win32 `GetOpenFileName`, not `FileOpenPicker`; use `Opacity` instead of `Visibility.Collapsed` to avoid layout jumps in fixed dialogs.
- To disable a row, dim only the install button and cog, never the row Grid (keeps Info clickable).
- `RequestReselect` is a no-op if the game is already selected: call `PopulateDetailPanel(card)` directly.
- `FindRecord()` returns a fresh object, not `card.*Record`: update both.
- On fresh installs `card.InstalledRecord` is null during post-install; use the local `record`.
- In static helpers, get hwnd from `xamlRoot`, never `GetRequiredService<MainWindow>()` (creates a new window).
- `InternalsVisibleTo RenoDXCommander.Tests` — tests can access `internal` (e.g. `ResolveAutoReShadeFilename`).
- `app.manifest` is `asInvoker` (admin via Task Scheduler, not manifest). `NoWarn NU1902` is suppressed.
- `GameDetectionService` must reuse `MaxScanDepth=4` / `_engineCache`.
- `.gitignore` excludes `bin/obj`, `.vs`, `publish.bat` (local path), `tools/*.md`.

## Delegating to sub-agents

Delegate file splits, multi-file refactors (4+ files), and features touching model + service + UI + persistence. Don't delegate single-file edits, manifest changes or small bug fixes. Sub-agent prompts must include the goal, file paths, build command, these rules, and relevant code snippets.

## Spec & Workflow

- Read `.trellis/spec/app/index.md` and the relevant guide before coding: `directory-structure.md`, `service-patterns.md`, `viewmodel-patterns.md`, `error-handling.md`, `logging-guidelines.md`, `quality-guidelines.md`, `i18n.md` (mandatory before adding/changing any user-visible text).
- Trellis phases: `workflow.md` — Plan (`prd.md`/`design.md`/`implement.md`) → Execute (only after `task.py start` → `in_progress`) → Finish (verify → spec update → commit).
- Before-dev: `trellis-before-dev`; after-edit check: `trellis-check` (available under `.zcode/skills/`, `.agents/skills/`, `.opencode/skills/`).
- No hard-coded user-visible strings in XAML (`Text="..."`) or C# (dialog titles/status text) — always use Loc keys. Adding a key requires all five language catalogs, even when the CI coverage threshold would still pass.
