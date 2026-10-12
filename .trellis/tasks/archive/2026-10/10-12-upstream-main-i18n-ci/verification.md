# Integration evidence

- Baseline: 00885d39e5cb53491317e630c733bd0cab63956d, clean feat/i18n synchronized with origin.
- Upstream: 7bca1272fb864989648fbc73bf9245235d8bf1b8 (15 incoming commits).
- Integration: d0230afd2573fcc3f6be2c44010b5e9ea6e0a084, merge parents baseline/upstream; pushed to origin/feat/i18n.
- Source review: merge conflict retained localization; five catalogs synchronized; F5 progress translated; preset protocol compatibility retained; F5 update state/NR deployment/tracking and uninstall original-forwarder cleanup fixed.
- Build & Test: https://github.com/wang93wei/RHI/actions/runs/38190846568 (success).
- Package: https://github.com/wang93wei/RHI/actions/runs/38190846211 (success).
- User boundary: no local validation and no local .NET. No local compiler/tests/catalog validation or .NET installation occurred. Commit agent mistakenly ran git diff --cached --check (whitespace-only validation), reported upstream whitespace and made no whitespace edits; disclosed to user. Future commit agents must explicitly omit that skill check.
- Not established by CI: Windows UI layout/live language switching, real F5 downloads, game updates/uninstall restoration, installer execution.

## Remote results

- Both workflows succeeded on d0230afd2573fcc3f6be2c44010b5e9ea6e0a084 on 2026-10-12.
- Build: 158 warnings, 0 errors; 62 application tests passed, 0 failed/skipped; 10 Python regression tests passed.
- Warning baseline: previous successful run 38121017208 on 00885d3 also had 158 warnings. Comparing unique warning messages found the same 27 messages, with none added or removed.
- Catalogs: 2140 keys in each of five catalogs, 100% key coverage; strict parity/value/placeholder gates succeeded. Unchanged brands and protocol text remain untranslated report-only.
- Portable ZIP content assertions passed for three F5 INIs and five language catalogs.
- Inno Setup successful compile log explicitly lists all three F5 INIs and five language catalogs as compressed inputs.
- RHI-Setup artifact: 44,095,076 bytes; RHI-portable artifact: 66,191,836 bytes; both not expired at retrieval.
- No CI repair iteration was required; source-review fixes were included before the first dispatch.
