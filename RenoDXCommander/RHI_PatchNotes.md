## v2.6.0 — i18n Preview

### New

- 🌐 **Multi-language support (i18n)** — RHI now ships with 5 languages: English (en-US, fallback), Simplified Chinese (zh-CN), Traditional Chinese (zh-TW), Japanese (ja-JP) and Korean (ko-KR). `en-GB` is aliased to `en-US`.
- **Follow System + Real-time switching** — Default is `Follow System` (maps `CurrentUICulture` to the nearest supported language, fallback `en-US`). Changing language in Settings → Language applies within 200 ms without restart and persists to `settings.json`.
- **Full coverage** — All `MainWindow.xaml` navigation, filters, detail panels and settings cards, plus 20+ `ContentDialog`s (`DialogService`, `SettingsHandler`, `DetailPanelBuilder`) are now localized via `ILocalizationService` (`Assets/Languages/*.json`).
- **JSON resource packs** — Flat `Section.Key` JSON under `Assets/Languages/` with `en-US` as authoritative baseline (1170 keys). Missing keys fall back to `en-US` then to the key itself and are logged once via `CrashReporter`. Coverage script `tools/check-i18n-coverage.py` / `.ps1` prints per-language coverage.

### Changes

- `SettingsViewModel` now persists `Language` (`System` | `en-US` | `zh-CN` | `zh-TW` | `ja-JP` | `ko-KR`) via `LoadSettingsFromDict` / `SaveSettingsToDict`; old installs without the key default to `System`.
- `App.xaml.cs` registers `ILocalizationService` as singleton and exposes it as XAML resource `Loc` for `{Binding [Key], Source={StaticResource Loc}}` live updates.
- `RenoDXCommander.csproj` and `package.yml` now publish `Assets/Languages/*.json`; GitHub Actions split into `build.yml` (Build & Test) and `package.yml` (Package) — both `workflow_dispatch` only.
- `LocalizationTests` added (≥15 cases: fallback, formatting, coverage, System mapping, persistence).

---

## v2.5.3

### Bug Fixes

- Fixed `RenoDX DLSS5.addon64` continuing to deploy to game folders even after v2.5.2. The old name was still stored in per-game addon selections in settings — RHI now migrates these to `DLSS5 Tool` on load.

---

## v2.5.2

### Bug Fixes

- Fixed `RenoDX DLSS5.addon64` being deployed to game folders on every launch due to a stale file left over from renaming the addon to DLSS5 Tool. RHI now removes it automatically on startup and cleans it up from all affected game folders, including per-game addon selections that still referenced the old name.
- Fixed DLSS5 Tool and DLSS Tool (ShortFuse) being deployed as `.addon32` on 32-bit games, causing a ReShade load error. Both addons now always deploy as `.addon64`.

---

## v2.5.1

### Bug Fixes

- Fixed DLSS5 Tool addon not deploying to game folders after being selected. The internal package name change from "RenoDX DLSS5" to "DLSS5 Tool" was not reflected in all deploy paths.
- Fixed stale `RenoDX DLSS5.addon64` file from the pre-rename version being re-deployed to games on every startup. RHI now removes it automatically on launch.
- Fixed co-deployed DLSS and Streamline files not being cleaned up when switching away from DLSS Tool (ShortFuse). Files RHI placed are now fully restored or removed on deselect.
- Fixed mutual exclusivity between DLSS5 Tool and DLSS Tool (ShortFuse) — selecting one now greys out the other in the addon picker.

---

## v2.5.0

### New

- Added a search bar to the shader pack picker — filter by pack name or individual shader filename.
- **DLSS Tool (ShortFuse)** — ShortFuse's DLSS5 addon is now in the addon picker as a second option alongside DLSS5 Tool. Supports DX12, DX11 and DX9 with HDR scaling. On install, RHI automatically downloads and deploys the newest DLSS SR, RR, FG, NR and Streamline files to the game folder. Supports RTX 20-50 Series. Still WIP — fall back to DLSS5 Tool if you have issues.
- **Updated nvngx_dlssnr.dll** to ShortFuse's latest build, now supporting RTX 20, 30, 40 and 50 Series GPUs with identical performance to the original NVIDIA build on RTX 50 Series.

### Changes

- Moved the Neural Rendering column to the far right of the Nvidia Profile section, after Streamline.
- Renamed RenoDX DLSS5 addon to DLSS5 Tool. The current version is now shown next to the name in the addon picker.

---

## v2.4.9

### New

- **nvngx_dlssnr.dll 310.8.SF** — a modified Neural Rendering DLL by ShortFuse that extends support to RTX 20, 30, 40 and 50 Series GPUs. This is now the default version RHI deploys. Shown as `310.8.1` in Windows Explorer, `310.8.SF` in RHI.

### Changes

- The Neural Rendering Deploy DLL button now also deploys `nvngx_dlss.dll` to the game folder alongside `nvngx_dlssnr.dll`. Any existing `nvngx_dlss.dll` is backed up as `.original` first.
- Added an MOTD button to the status bar next to Patch Notes — click it to re-read the current message at any time.

### Manifest Updates

- Added Reshade Motion Estimation by JakobPCoder to the shader pack library — dense real-time optical flow motion estimation.

---

## v2.4.8

### Bug Fixes

- Fixed "How to use" link not appearing in the per-game addon picker.
- Fixed `renodx-dlss5.addon64` triggering an install prompt when double-clicked or drag-dropped. It is managed by RHI internally and should only be installed via the addon picker or placed in the Custom Addons folder.

### Manifest Updates

- Added DLSS5 DX11 Bridge and DLSS5 Feeder to the addon picker — both enable DLSS 5 Neural Rendering in D3D11 games. Additional setup steps are required; the How To Use button on each addon links to the repo for instructions.
- Added DLSS5 Feeder companion shader to the shader pack library.
- Fixed Metal Gear Solid 4 (Master Collection) showing as Unreal Engine — now correctly shows MGS4 Engine.

---

## v2.4.7

### Bug Fixes

- Fixed the Neural Rendering column not showing `nvngx_dlssnr.dll` as installed after deploying it. It now updates immediately without needing a Refresh.
- The Neural Rendering column now clearly shows "Custom" when a custom DLL is active.

---

## v2.4.6

### Bug Fixes

- Fixed RenoDX DLSS5 not auto-updating to games when a new version is released. The addon now deploys the updated file directly from its own staging folder and no longer creates a redundant copy in the addons folder.

### Manifest Updates

- Added CubeLUT3Ddith by aron7awol to the shader pack library — Cube 3D LUT shader with dithering to reduce banding.

---

## v2.4.5

### Bug Fixes

- Fixed RenoDX DLSS5 not deploying to game folders after the addons staging folder was deleted. The addon now deploys directly from its own staging location.

---

## v2.4.4

### New

- **RenoDX DLSS5 addon** — `renodx-dlss5.addon64` is now a first-class addon in the per-game addon picker, listed above RenoDX Upgrade. Enable it per game from the Addons combo → Select. RHI downloads it automatically, keeps it updated silently alongside other components, and deploys `nvngx_dlssnr.dll` to the game folder alongside it if not already present. For 50 Series GPUs only.
