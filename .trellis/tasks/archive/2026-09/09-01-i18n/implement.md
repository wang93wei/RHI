# Implement: i18n 多语言全量实时切换

## Checklist (ordered)

### Phase 0 — 基础设施
- [ ] 0.1 新建 `RenoDXCommander/Services/ILocalizationService.cs` + `LocalizationService.cs` (+ `.Fallback.cs` partial 可选)
  - 单例 `ObservableObject`，`SupportedLanguages = ["en-US","zh-CN","zh-TW","ja-JP","ko-KR"]`，`FallbackLanguage="en-US"`
  - `LoadCatalogs()`：从 `AppContext.BaseDirectory/Assets/Languages` 与 `Assets/Languages` 双路径加载，若打包路径缺失回退开发路径；JSON 解析 `try/catch` + `CrashReporter.Log`，坏文件回退空字典
  - `ResolveSystemLanguage()`：`CultureInfo.CurrentUICulture.Name` → 精确匹配 → 前缀匹配（`zh`→`zh-CN`，`en`→`en-US`）→ `en-US`
  - `GetString(key, args)`：三级回退 + `string.Format`（缺参捕获 `FormatException` → 返回模板串 + Log）
  - 索引器 `this[key]` + `LanguageChanged` 事件
- [ ] 0.2 新建 `RenoDXCommander/Markup/LocExtension.cs`（可选）或 `Converters/LocConverter.cs` 作为过渡
  - 优先 `Binding` 方案：`App.xaml` Resources 注入 `LocalizationService` 单例 `x:Key="Loc"`，XAML 用 `Text="{Binding [Settings.Title], Source={StaticResource Loc}}"`
  - 若需 `LocExtension.MarkupExtension`，实现 `ProvideValue` 返回 `Binding`
- [ ] 0.3 `App.xaml.cs` DI 注册：`services.AddSingleton<ILocalizationService, LocalizationService>()`（置于 `HttpClient` 之后，`SettingsViewModel` 之前），并 `AddSingleton<LocConverter>` 如需
- [ ] 0.4 `App.xaml` 全局资源：`<loc:LocalizationService x:Key="Loc" />` 或通过 `App.Services` 暴露
- [ ] 0.5 资源文件：`Assets/Languages/en-US.json`（全量，≥500 key 先以英文占位）+ `zh-CN/ja-JP/ko-KR/zh-TW.json`（复制 en-US 占位，用于覆盖率基线）+ `Assets/Languages/README.md`

### Phase 1 — 持久化与设置页
- [ ] 1.1 `SettingsViewModel` 新增 `Language` 属性（`[ObservableProperty] string _language = "System"`），`LoadSettingsFromDict`/`SaveSettingsToDict` 读写 `Language`，`OnLanguageChanged` 同步到 `LocalizationService`
- [ ] 1.2 `MainWindow.xaml` 设置页新增语言卡片（仿 `DxvkVariantCombo` 模式）：`ComboBox x:Name="LanguageCombo"` 6 项（Follow System / English / 简体中文 / 繁體中文 / 日本語 / 한국어），`SettingsHandler.InitLanguageCombo` + `LanguageCombo_SelectionChanged`
- [ ] 1.3 `SettingsHandler.cs` 实现 `InitLanguageCombo` 与 `LanguageCombo_SelectionChanged`，含 `_languageComboInit` guard，与 `DxvkVariantCombo` 同模式
- [ ] 1.4 启动链路：`App.xaml.cs:OnLaunched` 在 `LoadSettingsFile` 后 `localization.ApplyLanguage(settings.Language)`，确保 `MainWindow` 构造前语言就绪

### Phase 2 — XAML 全量抽取
- [ ] 2.1 `MainWindow.xaml` 标题/导航/工具栏：`RHI / Simplified PC Gaming / Global Shaders / ReShade Addons / Wiki / Support` 等 `MenuFlyoutItem Text` → `Binding`
- [ ] 2.2 过滤与统计：`Filter games...` Placeholder、`0 shown / 0 installed`、`Hidden` → key
- [ ] 2.3 详情面板：`Components / HDR Mods / Recommended / Frame limiters / Optional` 分区标题、`RenoDX / Luma / DOF Fix / ReLimiter / Display Commander / OptiScaler / DXVK` 等 `Detail*Label`、`Launch / HDR / RES / 32-bit / 64-bit / Favourite / Config / Browse` 按钮
- [ ] 2.4 设置页：`Settings / Component Updates / ReShade & Display / Screenshots & Hotkeys / HDR & Peak Brightness` 等段标题与描述、`Apply / Check For Updates / Automatic Updates` 等
- [ ] 2.5 `SetupWindow.xaml`（如有可见文案）与 `Themes/DarkTheme.xaml` 注释文案
- [ ] 2.6 验证：`grep -rn 'Text="[^"]*[A-Za-z]' MainWindow.xaml` 硬编码清零（白名单 `RHI`）

### Phase 3 — C# 动态文案抽取
- [ ] 3.1 `DetailPanelBuilder*.cs`：`Build*` 中所有 `TextBlock.Text = "..."`、`ToolTip`、`Button.Content` 改 `GetString`
- [ ] 3.2 `DialogService*.cs`：所有 `ContentDialog.Title/Content/PrimaryButtonText/CloseButtonText` 抽取，含 `Unknown dxgi.dll Detected` / `Purge Staging Files` 等 20+ 弹窗
- [ ] 3.3 `ShaderPopupHelper` / `DragDropHandler*` / `MassDeployHandler` / `InstallEventHandler` 文案
- [ ] 3.4 `ViewModels`：`FilterViewModel` 过滤器名称、`GameCardViewModel.UI` 状态文案、`MainViewModel.*` 的 `ActionMessage`/`InstallProgress` 用户可见串
- [ ] 3.5 参数化校验：含 `{0}` 的 key 在调用侧传入正确参数，缺参不抛异常

### Phase 4 — Service 提示与错误文案
- [ ] 4.1 `Services/*` 中面向用户的异常/提示（如 `ReShadeExtractor` / `AutoUpdateService` 的用户提示）抽取；日志 `CrashReporter.Log` 保持英文
- [ ] 4.2 `FaqBuilder` / `Markdown` 文案（如需）抽取或保留英文（视范围）

### Phase 5 — 翻译与覆盖率
- [ ] 5.1 人工校对 `zh-CN.json` 关键路径（启动、安装、设置、错误弹窗 ≥100 key）
- [ ] 5.2 机器翻译占位 `ja-JP/ko-KR/zh-TW`（可先复制英文，CI 仅统计覆盖率）
- [ ] 5.3 新增 `tools/check-i18n-coverage.ps1`：比对 `en-US.json` 与其它语言，输出 `Coverage: zh-CN 87% ...`，缺 key 列表

### Phase 6 — 测试与发布
- [ ] 6.1 `RenoDXCommander.Tests/LocalizationTests.cs`：回退、参数化、System 映射、持久化 round-trip、坏 JSON 回退、Coverage 统计（≥10 用例）
- [ ] 6.2 手动验证（Windows）：切换 6 语言实时刷新、重启保持、Follow System 映射、缺 key 回退英文、ContentDialog 刷新
- [ ] 6.3 `RenoDXCommander.csproj` 新增 `Assets/Languages/*.json` Content，`publish.bat` 复制
- [ ] 6.4 `RHI_PatchNotes.md` 与 `Assets/Languages/README.md` 更新

## Validation Commands

```bash
# 本机（macOS，无 .NET）：仅验证 JSON / 覆盖率 / 硬编码扫描
python3 tools/check-i18n-coverage.py  # 或 powershell -File tools/check-i18n-coverage.ps1（如可用）
python3 -m json.tool RenoDXCommander/Assets/Languages/en-US.json > /dev/null && echo "en-US valid"
grep -rn 'Text="[^"]*[A-Za-z]' RenoDXCommander/MainWindow.xaml | grep -v 'RHI' | wc -l  # 期望 0
grep -rn 'Title\s*=\s*"' RenoDXCommander --include="*.cs" | grep -v 'GetString' | wc -l  # 期望趋近 0（日志除外）

# Windows 验证（仅通过 GitHub Actions 手动触发，workflow_dispatch）：
# - Build & Test:  dotnet build RenoDXCommander.sln -c Release -p:Platform=x64
#                  dotnet test RenoDXCommander.Tests/RenoDXCommander.Tests.csproj -c Release -p:Platform=x64 --no-build
# - Package:       dotnet publish RenoDXCommander/RenoDXCommander.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:Platform=x64 --self-contained false -o publish/RHI
# 触发方式：gh workflow run "Build & Test" --ref feat/i18n  与  gh workflow run "Package" --ref feat/i18n
# 永远不使用 push/pull_request 自动触发，仅 workflow_dispatch
```

## Risky Files / Rollback Points

- `App.xaml.cs`：DI 注册顺序，回滚点 commit `feat/i18n-phase0-infra`
- `ViewModels/SettingsViewModel.cs`：持久化键新增，回滚点 `feat/i18n-phase1-settings`
- `MainWindow.xaml`（3066 行）：大面积 `Text=` 替换，建议分 3 次提交（导航/过滤/详情/设置），每次 `grep` 验证
- `DetailPanelBuilder*.cs`（5309 行）与 `DialogService*.cs`：动态文案分散，需 `try/catch` 包裹 `GetString`，防止坏 key 崩溃主流程
- `SettingsHandler.cs`（1999 行）：新增 Combo 处理，注意 `_languageComboInit` guard 避免递归保存

## Review Gates

- Gate A：Phase 0+1 完成后，Windows  smoke：启动不崩溃、设置页可切换、重启保持
- Gate B：Phase 2+3 完成后，全量 `grep` 零残留 + 至少 10 个弹窗验证
- Gate C：Phase 6 完成后，`check-i18n-coverage.ps1` + `LocalizationTests` 全绿

