# Remote verification

## Work commit
- Branch: feat/i18n
- Merge/work SHA: 8b0fede9396e79c133cd3425ebb8cc14a16e09c5
- Upstream main: b9ba0dc (merge second parent); release 2.8.7.0.

## GitHub Windows CI
- Build & Test: https://github.com/wang93wei/RHI/actions/runs/38331745268 — completed / success on work SHA.
- Package: https://github.com/wang93wei/RHI/actions/runs/38331748578 — completed / success on same work SHA.
- Strict catalogs: 2144 baseline keys, all five languages passed.
- Catalog gate regression tests: 10 passed.
- Release x64 solution build: 0 errors, 158 warnings. Previous successful run 38190846568 on d0230af also reported 158 warnings; equal count does not prove each warning is unchanged.
- Application tests: 62 passed, 0 failed, 0 skipped.
- App publish/helper build, assembly and portable required-entry checks passed.
- Inno Setup 2.8.7.0 successful compile; both unexpired artifacts uploaded: RHI-Setup 44,101,744 bytes, RHI-portable 66,194,358 bytes.

## Boundary and deviation
No local .NET was installed or used; no local builds/tests/catalog checks ran. Commit agent mistakenly ran git diff --cached --check once despite explicit prohibition; it flagged upstream JSON trailing whitespace/line endings. Coordinator stopped further local checks and retained upstream formatting. Actual executable validation was on Windows GitHub Actions only.

Windows UI, live language switching, URL opening, real Nightly downloads and game deployment were not exercised. Existing runtime risks remain documented in review.md; these CI passes do not establish their absence.
