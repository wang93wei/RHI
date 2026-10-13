# Remote verification

## Work commit
- Branch: feat/i18n
- Merge/work SHA: 2f390975f223ab394d718d3d28c1ddd1b3f46a97
- Upstream main: 4d98018 (merge second parent); data-only (manifest.json FG preset C, dlss_manifest.json 310.9.16 / Streamline 2.14.16.0).

## GitHub Windows CI
- Build & Test: https://github.com/wang93wei/RHI/actions/runs/38406041887 — completed / success on work SHA.
- Package: https://github.com/wang93wei/RHI/actions/runs/38406047599 — completed / success on same work SHA.
- Release x64 solution build: 0 errors, 158 warnings (same count as previous run 38331745268).
- Application tests: 62 passed, 0 failed, 0 skipped.
- Artifacts uploaded, not expired: RHI-Setup 44,096,871 bytes, RHI-portable 66,194,427 bytes.

## Boundary
No local .NET install, build, tests or catalog checks. No catalog or code change (see review.md). No CI failure, so no remote fix was needed.
Windows UI (FG preset C in dropdowns, NVIDIA profile write of value 3) and real downloads of new DLSS/Streamline versions were not exercised.
