[center][size=6][b]RHI — ReShade HDR Installer[/b][/size]

[i]Automatic HDR modding, DLSS management, and NVIDIA driver control for your entire PC game library.[/i][/center]

RHI scans all your games from Steam, Epic, GOG, Xbox Game Pass, EA, Ubisoft, Battle.net, Rockstar, and itch.io — then handles everything in one click.

[size=5][b]HDR for Every Game[/b][/size]

[b]Bespoke mods (RenoDX)[/b] — hundreds of game-specific HDR mods matched and installed automatically. If your game has one, RHI finds it and keeps it updated.

[b]Unreal Engine games[/b] — every UE game gets HDR via the UE-Extended addon. RHI writes the correct Engine.ini settings and reshade.ini configuration automatically. You don't touch any ini files.

[b]Luma Framework[/b] — every DX11 Unreal Engine game in your library shows a Luma option automatically. Game-specific Engine.ini keys, launch arguments, and TAA settings applied from the wiki on install without any manual steps.

[b]DX9 / older games[/b] — DXVK + Lilium HDR brings scRGB HDR output to games with no native HDR path.

[b]Everything else[/b] — RenoFX HDR Toolkit converts SDR to HDR for any remaining game.

[line]

[size=5][b]Replaces the ReShade Installer[/b][/size]

RHI handles ReShade end-to-end. No manual downloads, no DLL naming guesswork, no ini editing.

[list]
[*]Detects each game's graphics API automatically and names the DLL correctly (dxgi.dll, d3d9.dll, opengl32.dll)
[*]Latest stable and nightly builds always ready — staged on app launch
[*]Per-game build channels: Stable, Nightly, Custom, Legacy, or No Addons — mix freely across your library
[*]Vulkan games handled via global implicit layer — one-click install
[*]Foreign DLL detection: warns before overwriting DXVK, Special K, or ENB
[*]Manages reshade.ini automatically — hotkeys, screenshot path, peak brightness, DLSS paths all configured
[/list]

[line]

[size=5][b]DLSS 5 and Neural Rendering[/b][/size]

[b]DLSS5 Feeder[/b] — one click deploys the full neural rendering pipeline: DLSS5 consumer addon, nvngx_dlssnr.dll, pre-configured ReShade preset, and required shader packs. Automatically selects the right method for your game (DX11, DX12, 32-bit).

[b]NR Cost Scaler[/b] — runs neural rendering at 75% cost by default. Toggle before installing a method and it deploys alongside it automatically.

[b]ShortFuse DLSS Tool[/b] — alternative neural rendering implementation for DX12 games.

[line]

[size=5][b]DLSS and Streamline Manager[/b][/size]

Every NVIDIA game gets a full DLSS management panel.

[list]
[*]Swap SR, RR, FG, and Streamline versions per game — any version from 2.x to current
[*]Original DLLs backed up automatically and restorable any time
[*]Presets (J/K/L/M for SR, D/E for RR, A/B for FG) written directly to NVIDIA driver profiles — no Profile Inspector needed
[*]Render scale override — force any render resolution from 33% to 100% per game
[*]NVIDIA Override — enable the driver's "Latest DLL" injection per component, directly from the version dropdown
[*]Batch Deploy — push versions, presets, and render scales to your entire library at once
[*]Auto-update for SR, RR, and FG — opt-in per game
[/list]

[line]

[size=5][b]Multi Frame Generation[/b][/size]

[b]RTX 40 MFG Unlock[/b] — enables MFG multipliers beyond 2x (up to 6x) on RTX 40 Series via Ultimate ASI Loader. One-click install from the Extras section.

[b]RTX 50 Series MFG[/b] — configure Fixed or Dynamic mode, frame count (2x–6x), and dynamic target frame rate from the DLSS panel.

[line]

[size=5][b]OptiScaler[/b][/size]

[list]
[*]FSR4 / FSR3 / XeSS on DLSS-only games. Frame generation on AMD and Intel GPUs.
[*]Stable and Nightly channels selectable per game
[*]Pre-configured per-GPU INI templates (NVIDIA, AMD+DLSS, AMD no-DLSS) — user-editable, never overwritten
[*]Streamline + DLSS Enabler deploy per game for Nightly FG setups
[*]Full frame generation settings in the cog: FG Input, Output, Nvngx Replacement, HUD Fix
[*]OptiPatcher auto-deployed and silently auto-updated
[/list]

[line]

[size=5][b]Frame Limiters[/b][/size]

[b]ReLimiter[/b] — precision frame pacing with predictive sleep, phase-locked timing, VRR awareness, and DLSS FG adaptive pacing. The most accurate per-game frame limiter available.

[b]Display Commander[/b] — alternative limiter with support for both 32-bit and 64-bit games.

Both install as ReShade addons and update automatically.

[line]

[size=5][b]NVIDIA Driver Profile Management[/b][/size]

[i]Replaces NVIDIA App and NVIDIA Profile Inspector for per-game driver settings.[/i]

RHI writes driver settings directly via NvAPI — no external tools needed.

[b]Per game:[/b]
[list]
[*]VSync Mode and Tear Control
[*]Low Latency (Off / On / Ultra)
[*]Smooth Motion (RTX 40+) — Enable, Allowed APIs, Flip Pacing
[*]Power Management Mode
[*]G-Sync per-game toggle
[*]ReBAR — Off / On / Auto, Mode, Size Limit
[*]RTX HDR — Enable with Peak Brightness, Contrast, Saturation, Middle Grey, Debanding
[*]DLSS Presets and Render Scale Override
[/list]

[b]Global (all games):[/b]
[list]
[*]Shader Cache, G-Sync Mode, Preferred Refresh Rate, Global ReBAR, FPS Limit
[*]Digital Vibrance per display — restored automatically on every app startup
[/list]

[b]Export / Import[/b] — back up all per-game NVIDIA settings to a file. Restore everything after a driver reinstall in one click.

[line]

[size=5][b]HDR Auto-Toggle[/b][/size]

Enables Windows HDR when a game launches and disables it when the game closes. Works with Steam, Epic, Xbox Game Pass, and direct exe launches. Override per game with the HDR button next to Launch.

[line]

[size=5][b]46 Shader Packs[/b][/size]

Every shader pack you'd want, all managed from one dialog.

[b]Essential:[/b] Lilium HDR Shaders — required for HDR tone mapping.

[b]Recommended:[/b] PumboAutoHDR, crosire reshade-shaders, MaxG2D Simple HDR, clshortfuse shaders, RenoFX HDR Toolkit, smolbbsoop shaders.

[b]Extra:[/b] iMMERSE, qUINT, OtisFX, SweetFX, Prod80, CRT-Royale, VRToolkit, ZenteonFX, QD-OLED APL Fixer, LumaBoost, Glamarye Fast Effects, and 28 more.

Select packs globally or per game. Pick individual shader files within any pack. Save selections as named profiles and export them as a zip to share directly into Discord.

[line]

[size=5][b]Update All[/b][/size]

One button updates ReShade, RenoDX, ReLimiter, Display Commander, OptiScaler, and RE Framework across your entire library. Background check runs every 4 hours — works silently from the system tray.

[line]

[size=5][b]Manifest-Driven[/b][/size]

Game support, Engine.ini fixes, mod links, and feature flags are delivered via a server-side manifest updated on GitHub. New game support and fixes reach all users instantly without waiting for an app update.

[line]

[size=5][b]Requirements[/b][/size]

[list]
[*]Windows 10 / 11 (64-bit)
[*][url=https://dotnet.microsoft.com/download/dotnet/8.0].NET 8 Desktop Runtime[/url]
[*]NVIDIA GPU recommended for driver management features (ReShade and RenoDX work on all GPUs)
[/list]

[b]Supported stores:[/b] Steam · Epic · GOG · Xbox Game Pass · EA App · Ubisoft Connect · Battle.net · Rockstar · itch.io

[line]

[i]RHI is free. All third-party components — ReShade, RenoDX, Luma, OptiScaler, DXVK, and others — are the work of their respective authors.[/i]
