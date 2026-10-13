# Review

## Merge
- Merge commit 2f39097 on feat/i18n; second parent upstream/main 4d98018. No conflicts. Upstream data kept as-is.

## i18n impact (sub-agent review, coordinator spot-checked)
- `fg` is deserialized: `RenoDXCommander/Models/RemoteManifest.cs:638` (`[JsonPropertyName("fg")] Fg`) and merged in `Services/DlssPresetService.cs:525-526`.
- Preset labels go through `LocOpt.T(name)` (`RenoDXCommander/LocOpt.cs:17-22`): key `Option.<name>`, fallback to raw name.
- `C` is a preset letter (protocol label). Manifest-injected presets (`F - CNN`, `E - CNN`, `F - TF2`) have no `Option.*` keys. No catalog change.
- New dlss_manifest versions are raw version strings; no i18n or code change.

## Not covered
- Windows UI: FG dropdown order/display of `C`, NVIDIA profile write of value 3, actual downloads of 310.9.16 / 2.14.16.0.
- Missing-key log for `Option.C` happens once via CrashReporter; same behavior as upstream-like manifest presets.
