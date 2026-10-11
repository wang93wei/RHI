# Integration evidence

- Baseline: a54a62d. Upstream main: e9eef5daaac32b168f16baa4c8407ba6a5ac40ca.
- Merge commit: 27d6a4429541c699a8191c27dfbac8a8db2ce80e, parents a54a62d and e9eef5d. Pushed to origin/feat/i18n.
- Five parallel localization groups and one catalog consolidator: each catalog received 82 new keys and 3 updated existing values.
- AGENTS.md and i18n spec updated. Source-only review found no concrete merge error.
- No local build/test/lint or .NET installation. The first commit worker ran Git whitespace checks and stopped on inherited CRLF; the replacement honored the no-local-validation instruction and preserved upstream line endings.

## Remote validation

- Build & Test: https://github.com/wang93wei/RHI/actions/runs/38120066437 (success).
- Package: https://github.com/wang93wei/RHI/actions/runs/38120069702 (success).
- Both runs target 27d6a4429541c699a8191c27dfbac8a8db2ce80e.

## Limits

Windows UI, live switching, installer runtime download, registry operations and actual game integration require separate runtime evidence.

## Completed CI evidence

- Build & Test: 62 passed, 0 failed, 0 skipped; build 0 errors and 158 warnings.
- Catalog baseline: 2133 keys; zh-CN/zh-TW/ja-JP/ko-KR each 2133/2133 (100%); no hard-coded MainWindow.xaml strings reported. This checker does not prove placeholder parity or C# coverage.
- Package: publish, drop-helper build, installer compile, portable ZIP and uploads succeeded. Inno log lists all five language JSONs.
- RHI-Setup artifact ID 11694816800, 44,069,715 bytes. RHI-portable artifact ID 11694981270, 66,021,789 bytes. Both target the tested merge SHA and are not expired at inspection.
- No CI failure required a repair or rerun. Prior successful remote Build & Test run 37887260110 reported 146 warnings; current run reports 158. The increase is in CS8604 / CS8601 nullable-reference warnings. No warning cleanup was included; CI did not fail.
