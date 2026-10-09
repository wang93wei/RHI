# Execution
1. 合并 upstream/main 并解决冲突。
2. 审查上游新增 XAML/C# 用户文本，补齐五种语言。
3. 执行 python3 tools/check-i18n-coverage.py --strict 及资源占位符检查，独立审查。
4. 提交并推送当前分支，执行 GitHub CI，修复失败直到通过。
5. 归档任务并报告验证边界。

本机禁止 .NET 安装及构建。合并失败可在提交前 git merge --abort 恢复。

## CI correction
First manual run 37886997164 failed at restore: NU1605, app WindowsAppSDK 2.5.1 versus test 1.6.250108002. Synchronize direct test SDK reference, preserve ExcludeAssets=buildTransitive. Other solution projects have no competing WindowsAppSDK reference. Auxiliary ManifestEditor is independent and is outside solution scope.

## Result
- Upstream main 60559eb merged via 5d4ae41; 74 new localization keys, five catalogs have 2051 keys.
- Static i18n, placeholder parity, references and XML validation passed.
- CI 37886997164 failed due to test SDK downgrade; corrected by c72e5b2.
- CI 37887260110 on c72e5b275c10287af96f5a6100bea33bfabc9437 succeeded: Release x64 build (146 warnings, 0 errors), 62 tests passed, 0 failed/skipped.
- Only workflow_dispatch retained. No local .NET installation or execution. Windows GUI and packaging were not verified.
