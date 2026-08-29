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
# AGENTS.md — RHI

## What this repo is

RHI (formerly RenoDX Commander) is a Windows desktop app that manages HDR/mod components — ReShade, RenoDX, DLSS/Streamline swaps, OptiScaler, DXVK, Luma, ReLimiter, and more — across a user's PC game library (8 storefronts), with per-game overrides and NVIDIA driver-profile settings. UI framework is **WinUI 3** (`Microsoft.UI.Xaml`), not WPF. Root namespace is `RenoDXCommander`; output assembly name is `RHI`.

## Layout

- `RenoDXCommander/` — main WinUI 3 app (net8.0-windows10.0.19041). `Services/` (all install/update/detection logic, interface-first), `ViewModels/`, `Models/`, `Controls/`, `Themes/`.
- `RenoDXCommander.Tests/` — xUnit tests (`CoreLogicTests.cs`). Pure logic only: no filesystem, no network, no DI.
- `RHI.DropHelper/` — small WinForms helper providing non-elevated drag-drop while the main app runs elevated.
- `tools/RHI-ManifestEditor/` — separate solution for editing the remote manifest.
- `tools/` — utilities: `check_missing_includes.py`, `decode_game_report.py`, `upload-dlss-releases.ps1`, `RHI-Stats`.
- `manifest.json` (repo root) — the remote manifest (game overrides, feature flags, component URLs); shape lives in `RenoDXCommander/Models/RemoteManifest.cs`.
- `dlss_manifest.json` — DLSS/Streamline version catalog (`dlss`, `dlssd`, `dlssg`, `streamline`, `dlssnr`).
- `RHI Setup.iss` — Inno Setup installer script.
- `docs/` — feature/implementation docs (see "Read before editing" below).

## Build / test

- `dotnet build` — `Directory.Build.props` forces `Platform=x64` when unset/AnyCPU.
- `dotnet test RenoDXCommander.Tests/RenoDXCommander.Tests.csproj`
- Release publish: `publish.bat` (single-file win-x64, self-contained false, then copies content/template files next to the EXE). Note: its output path is hardcoded to the maintainer's machine (`C:\Users\Mark\OneDrive\...`) and it force-kills running RHI processes — don't run blindly.
- App version lives in `RenoDXCommander.csproj` (`AssemblyVersion`/`FileVersion`); user-facing release notes go in `RenoDXCommander/RHI_PatchNotes.md`.

**Environment gotcha (as of 2026-08):** this machine has only the .NET 8 *runtime* installed — no .NET SDK and no Visual Studio. `dotnet build` / `dotnet test` fail with "No SDKs were found" until the .NET 8 SDK is installed. Don't claim builds/tests pass without running them.

## Architecture & conventions

- **DI:** everything registers in `App.xaml.cs` into `App.Services` (static `IServiceProvider`, Microsoft.Extensions.DependencyInjection). All services are singletons and interface-first (`IFooService` → `FooService`); add new services there.
- **Partial-class splitting by feature:** large classes are spread across files like `MainViewModel.Install.cs`, `GameCardViewModel.DlssStreamline.cs`, `DetailPanelBuilder.Overrides.NvidiaProfile.cs`, `DlssPresetService.ReBar.cs`. Put new code in the matching partial file (or add a new one), not into the already-huge parent file.
- **UI:** much of the detail UI is built in C# via `UIFactory` and the `DetailPanelBuilder.*` partials, not XAML. `MainWindow.xaml` is the main shell.
- **Feature flags:** `Services/FeatureFlags.cs` gates in-progress features. A flag is on if `DevUnlockService.IsUnlocked` (file `%LocalAppData%\RHI\unlock.txt` exists) OR the manifest's `featureFlags` sets it true. Gate new hidden features this way.
- **Networking:** one shared `HttpClient` singleton from DI (User-Agent `RHI/2.0`); `GitHubETagCache` handles conditional GitHub requests. Use them; don't new up `HttpClient`.
- **Content/template files:** `ReShade.ini`, `ReShade.Vulkan.ini`, `OptiScaler*.ini`, `relimiter.ini`, `DisplayCommander.ini`, `dxvk.conf`, `7z.exe`/`7z.dll`, icons, `RHI_PatchNotes.md` etc. are `Content` items with `CopyToOutputDirectory` + `ExcludeFromSingleFile`. When adding such files, follow that pattern or deploys/publish will silently miss them.
- Nullable + implicit usings enabled; `AllowUnsafeBlocks` is on; tests reach internals via `InternalsVisibleTo("RenoDXCommander.Tests")`.
- The Tests csproj references `Microsoft.WindowsAppSDK` with `ExcludeAssets="buildTransitive"` deliberately — removing it drags in PRI-generation targets that require Visual Studio.

## Read before editing sensitive areas

- `docs/manifest-field-reference.md` — every manifest field, what it does, and where it's read. Read before touching `RemoteManifest.cs` or `manifest.json`.
- `docs/DETAILED_GUIDE.md` — full feature reference; check here for expected behavior before changing features.
- `docs/nexus-integration.md` — NexusMods GraphQL/NXM download flow.
- `RenoDXCommander/FEATURES.txt` — ReLimiter feature spec.
- `RenoDXCommander/RHI_PatchNotes.md` — recent release notes; update when shipping user-visible changes.

## Domain gotchas

- The app deliberately warns users it is **single-player only** — ReShade-with-addons can trigger anti-cheat. Preserve that warning in user-facing flows.
- NVIDIA profile writes (ReBAR, Low Latency, Smooth Motion, DLSS presets) require admin; the app uses Task-Scheduler-based persistent elevation. Don't move these code paths off the elevated path.
- "Foreign DLL protection": the app scans binary signatures to avoid clobbering DXVK/Special K/ENB DLLs. Respect it when writing deploy/overwrite logic.
