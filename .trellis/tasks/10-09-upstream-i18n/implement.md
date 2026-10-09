# Execution
1. 合并 upstream/main 并解决冲突。
2. 审查上游新增 XAML/C# 用户文本，补齐五种语言。
3. 执行 python3 tools/check-i18n-coverage.py --strict 及资源占位符检查，独立审查。
4. 提交并推送当前分支，执行 GitHub CI，修复失败直到通过。
5. 归档任务并报告验证边界。

本机禁止 .NET 安装及构建。合并失败可在提交前 git merge --abort 恢复。

## CI correction
First manual run 37886997164 failed at restore: NU1605, app WindowsAppSDK 2.5.1 versus test 1.6.250108002. Synchronize direct test SDK reference, preserve ExcludeAssets=buildTransitive. Other solution projects have no competing WindowsAppSDK reference. Auxiliary ManifestEditor is independent and is outside solution scope.
