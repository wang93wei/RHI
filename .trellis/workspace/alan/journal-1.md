# Journal - alan (Part 1)

> AI development session journal
> Started: 2026-09-01

---



## Session 1: i18n 全量实时切换落地

**Date**: 2026-09-01
**Task**: i18n 全量实时切换落地
**Branch**: `feat/i18n`

### Summary

JSON+ILocalizationService 五语言全量 i18n：XAML 绑定清零、C# 弹窗/VM 状态迁移、实时切换与 settings.json 持久化；CI Build & Test workflow_dispatch 通过（33481445052）。

### Git Commits

| Hash | Message |
|------|---------|
| `cd9e9e2` | (see git log) |
| `3338a6e` | (see git log) |
| `ab1ffeb` | (see git log) |
| `d96de5b` | (see git log) |
| `882a92f` | (see git log) |
| `822deba` | (see git log) |
| `18117cf` | (see git log) |
| `f419e78` | (see git log) |

### Status

[OK] **Completed**


## Session 2: 补齐下拉选项与残留字段的 i18n 本地化

**Date**: 2026-09-02
**Task**: 补齐下拉选项与残留字段的 i18n 本地化
**Branch**: `feat/i18n`

### Summary

全面清查并补齐 UI 硬编码英文：新增 LocOpt helper（Option.<英文原文> 键、缺键回退英文），驱动设置/全局设置/DXVK/ReShade 渠道/着色器模式/位深/API 等全部下拉接入翻译；显示文本与逻辑值双轨化，修复 6 处依赖英文文本的 SelectionChanged 字符串比较（Global/On/Custom...）；接入字典已存在但漏接的 15 键，补 31 处硬编码 Tooltip/Header；5 语言字典新增 en+191/其余+116 键，覆盖率 94.5%，check-i18n 通过；CI build/test 验证绿（中途修复 LocOpt 缺 DI using 与 Skeleton 静态方法实例 Loc 两处编译错误）。遗留：OptiScaler nightly cog 弹窗耦合下拉未本地化（显示文本兼作 INI 映射键），已记入 README R3.4，建议单独任务处理。

### Git Commits

| Hash | Message |
|------|---------|
| `fa02c43` | (see git log) |
| `17fe6ef` | (see git log) |

### Status

[OK] **Completed**


## Session 3: 合并上游 v2.8.0 并继续汉化（组件更新窗口 / 驱动设置面板）
<!-- trellis-session: v=2 fp=b4438e59b377c4b8 -->

**Date**: 2026-09-30
**Task**: 合并上游 v2.8.0 并继续汉化（组件更新窗口 / 驱动设置面板）
**Branch**: `feat/i18n`

### Summary

将 upstream RankFTW/RHI main（2847d0c，v2.8.0 Beta）合并进 feat/i18n，解决 3 处冲突（MainWindow.xaml Updates 按钮、NVIDIA 面板拆分后的 DLSS/Streamline 标题、驱动设置管理员提示），随后继续汉化：新增的组件更新窗口全文案接入 Loc，驱动设置折叠摘要本地化，清理残留硬编码；五语言各 +31 键（1904 键，100% 覆盖）。

### Main Changes

- merge: 合并 22 个上游提交（NVIDIA 面板拆分为 DLSS/Streamline 与 Driver Settings、组件更新历史窗口）
- feat(i18n): UpdateLogWindow 全量 Loc 化（标题、计数单复数、空状态、今天/昨天/日期格式随语言、已更新标记、分类标签）
- feat(i18n): 驱动设置与 DLSS/Streamline 拆分标题、折叠摘要（VSync/Smooth/ReBAR/NV Override）接入 Loc/LocOpt
- fix(i18n): 清零残留硬编码（HDR 状态提示、隐藏计数、Luma 开关、Show/Hide、关于页标语、无更新弹窗、自定义着色器提示、Loading 初始状态）
- docs: 五语言字典各 +31 键，更新 Languages README、RHI_PatchNotes、i18n 规格键数

### Git Commits

| Hash | Message |
|------|---------|
| `f273ae2` | Merge remote-tracking branch 'upstream/main' into feat/i18n |
| `7527a98` | feat(i18n): 本地化上游 v2.8.0 新增界面并补齐残留硬编码 |

### Testing

- [OK] python tools/check-i18n-coverage.py --strict → 五语言 1904/1904、无大小写重复键、XAML 硬编码 0
- [OK] 静态校验脚本：1583 个代码/XAML 引用键在五语言字典中零缺失、零大小写重复（脚本为本次临时脚手架，未入库）
- [OK] CI Build & Test run 36715629274（workflow_dispatch @ feat/i18n）：Check i18n catalogs / Build Release x64 / Run tests 全部 success
- [OK] 本机无 .NET SDK（仅 runtime），无法本地 dotnet build/test

### Status

[OK] **Completed**

### Next Steps

- CI 结果若暴露 XAML 绑定或编译问题，按日志修正
- R3.4 有意保留英文项（OptiScaler nightly cog 耦合下拉等）需先解耦显示与 INI 映射再汉化


## Session 4: 继续同步上游 12 个提交（DXVK / OptiScaler 修复）并复核汉化
<!-- trellis-session: v=2 fp=48af52c1bbd14692 -->

**Date**: 2026-09-30
**Task**: 继续同步上游 12 个提交（DXVK / OptiScaler 修复）并复核汉化
**Branch**: `feat/i18n`

### Summary

合并 upstream/main 2847d0c..1199800（12 个提交：DXVK DX11 安装/卸载状态修复、OptiScaler plugins 目录保护、更新日志版本捕获、补丁说明），1 处冲突（MainViewModel.Dxvk.cs）解决为保留 Loc 文案并采纳上游 DxvkEnabled=true。本批上游未新增用户可见文案，五语言字典无需变更；另做了一轮残留英文审计。

### Main Changes

- merge: 合并 12 个上游提交（DXVK/OptiScaler/更新日志修复 + 补丁说明）
- fix(merge): 冲突处保留 Status.DxvkInstalled 的 Loc 调用，同时采纳上游 card.DxvkEnabled=true（DX11 RequiresVulkanInstall 正确性）
- audit(i18n): 上游新增行零用户可见文案；39 个『未翻译但被引用』键复核后全部属 R3.4（品牌/技术名/格式串，API Key 与既有 zh-CN 行文风格一致）
- audit(i18n): 确认 ExternalLabel（Download from Nexus Mods / Discord）为清单数据且兼作 Redownload 逻辑，维持 R3.4 不汉化

### Git Commits

| Hash | Message |
|------|---------|
| `42301ae` | Merge remote-tracking branch 'upstream/main' into feat/i18n |

### Testing

- [OK] CI Build & Test run 36728982377 @ 42301ae：i18n catalogs 1904/1904 五语言、Build succeeded、62/62 测试通过
- [OK] python tools/check-i18n-coverage.py --strict → exit 0
- [OK] 10 个合并文件的括号/分隔符配平检查 OK；git rev-list HEAD..upstream/main = 0

### Status

[OK] **Completed**

### Next Steps

- 上游后续若新增界面文案，继续抽取 Loc 键并五语言同步
- 如需汉化补丁说明正文或 ExternalLabel，需先解耦（多语言 md 资源 / 显示文本与逻辑值分离）


## Session 5: 同步上游 ReBAR 摘要修复并保持汉化
<!-- trellis-session: v=2 fp=80a010a948d4f66b -->

**Date**: 2026-09-30
**Task**: 同步上游 ReBAR 摘要修复并保持汉化
**Branch**: `feat/i18n`

### Summary

合并 upstream 2dea5a3（Driver Settings 折叠摘要的 ReBAR 状态修正：0=Off、1=Auto 省略、2=On）。冲突位于已本地化的摘要代码，解决为同时保留 LocOpt 本地化与上游新逻辑；复用既有 Option.On/Option.Off 与 Overrides.Summary.* 键，五语言字典维持 1904 键不变。

### Main Changes

- merge: 合并上游 2dea5a3（ReBAR 摘要状态修正）
- fix(merge): 冲突解决保留 LocOpt.T 本地化，并采纳上游 0=Off / 1=Auto(省略) / 2=On 与 rebarName != null 判断
- i18n: 复用既有键，无需新增/改动字典项

### Git Commits

| Hash | Message |
|------|---------|
| `5475a29` | Merge remote-tracking branch 'upstream/main' into feat/i18n |

### Testing

- [OK] CI Build & Test run 36729863452 @ 5475a29：i18n catalogs 1904/1904、Build succeeded、62/62 测试通过
- [OK] python tools/check-i18n-coverage.py --strict → exit 0；改动文件分隔符配平 OK；HEAD..upstream/main = 0

### Status

[OK] **Completed**

### Next Steps

- 上游继续更新时按同法合并：冲突多集中在已本地化文案行，保持 Loc/LocOpt 优先并采纳上游新逻辑


## Session 6: 合并上游 v2.8.1–v2.8.6 Beta 2 并继续汉化
<!-- trellis-session: v=2 fp=6e3f0a91c7b42d58 -->

**Date**: 2026-10-06
**Task**: 合并上游 v2.8.1–v2.8.6 Beta 2 并继续汉化
**Branch**: `feat/i18n`

### Summary

将 upstream RankFTW/RHI main（1eb9c20，v2.8.6 Beta 2）合并进 feat/i18n，跨越 135 个上游提交（上次合并点为 2dea5a3）。解决 12 个文件 29 处冲突后，继续汉化上游新增界面：五语言各 +73 键（1904 → 1977，100% 覆盖）。首次 CI 运行暴露了上游 csproj 的 ClrMD 引用缺陷（MSB3030），修复后 Build & Test 与 Package 两个工作流均通过。

### Main Changes

- merge: 合并 135 个上游提交（ClrMD 卡死诊断、GitHub Device Flow OAuth、dgVoodoo2 Extras 行、Background Checks 设置、对话框/生命周期修复）
- fix(merge): 12 文件 29 处冲突一律「保留 Loc/LocOpt 文案 + 采纳上游新逻辑」
  - MainWindow.xaml：采纳上游 Grid 布局（Background Checks、Automatic Updates 对齐、Mod Data Source + GitHub API 卡片），文案改回 Loc 绑定
  - MainWindow.Events.Components.cs：保留 Loc 行标签，采纳上游行号 5/6/7，删除重复的 GetVulkanPresentMethod 二次取数
  - DialogService.Update.cs / MainWindow.Events.cs / MainViewModel.Install.Luma.cs：保留 Loc 文案，采纳上游 progressSession.Completion / DisposeAsync
  - UpdateLogWindow.cs：采纳上游 FormatVersion（GhRelease tag_name / ETag / Last-Modified），返回 null 以复用本地化的 "Updated"
  - ControlUePostInstallService.cs：保留 Loc.ControlUe.Strategy/AlsoList，把上游新增的 RT/SSR 提示并入字典
  - RHI Setup.iss：采纳上游相对路径默认值，但保留 /D 可覆盖，package.yml 不受影响
- feat(i18n): 上游新增界面全量接入 Loc（五语言各 +73 键）
  - 设置页：Background Checks + On/Minimal 提示、Mod Data Source、GitHub API 卡片、Donate 按钮
  - GitHub OAuth：连接/状态/设备码文案（SettingsHandler + GitHubAuthService）
  - Donate 对话框：标题、空状态、说明、Ko-fi 警告
  - OptiScaler nightly：FG Enabled、Force Reflex、Use Games Reflex Markers
  - Control UE 安装对话框：OptiScaler FG / Using HDR 两行
  - Extras：dgVoodoo2 行（安装/重部署/移除/齿轮 + 设置对话框）
  - 游戏覆盖摘要标签、作者徽章（RenoDX/Luma）、卡死诊断卡片
- refactor(i18n): 解耦显示与逻辑，使新下拉框可翻译 —— FG Enabled / Force Reflex / Use Games Reflex Markers 改为按索引选择；作者徽章前缀剥离同时兼容半角 `:` 与全角 `：`
- fix(build): 上游 csproj 用硬编码 `$(UserProfile)` 缓存路径引用 ClrMD，却从未下载该包，干净检出必失败（MSB3030）；改用 `PackageDownload` 在 restore 阶段拉包且不进依赖图（PackageReference 会让 WinAppSDK XAML 编译器在 publish 时崩溃）

### Git Commits

| Hash | Message |
|------|---------|
| `e7b1b68` | Merge upstream/main (v2.8.1–v2.8.6 Beta 2) into feat/i18n and continue localization |
| `a2488e5` | fix(build): restore ClrMD package so freeze-diagnostics DLL resolves on clean checkout |

### Testing

- [OK] python tools/check-i18n-coverage.py --strict → 五语言 1977/1977、无大小写重复键、XAML 硬编码 0
- [OK] 静态校验：1693 个代码/XAML 引用键在五语言字典中零缺失（含 LocOpt.T → Option.* 映射）；JSON 全部可解析；`{0}` 占位符五语言数量一致
- [OK] 冲突标记清零、括号/引号配平检查、XAML x:Name 无重复
- [OK] CI Build & Test run 37479884486 @ a2488e5：i18n catalogs 1977/1977、Build succeeded、62/62 测试通过
- [OK] CI Package run 37479900049 @ a2488e5：Installer 2.8.5.0 + portable zip 产出成功，Microsoft.Diagnostics.Runtime.dll 已进入发布目录
- [FAIL→FIX] 首次运行 37478745852 / 37478782627 均因 MSB3030 失败（上游缺陷，非汉化引入）；上游仓库无 CI 工作流，故该缺陷此前未被发现
- [OK] 本机无 .NET SDK（仅 runtime），无法本地 dotnet build/test

### Status

[OK] **Completed**

### Next Steps

- 上游继续更新时按同法合并：冲突多集中在已本地化文案行，保持 Loc/LocOpt 优先并采纳上游新逻辑
- OptiScaler nightly cog 剩余耦合下拉（upscaler/FG input/FG output/HUD fix/combined/DMV/flip）仍需先解耦显示与 INI 映射再汉化（见 `Assets/Languages/README.md` R3.4）
- FAQ 正文、补丁说明正文、`*ActionMessage` 进度文案仍为有意保留的英文
