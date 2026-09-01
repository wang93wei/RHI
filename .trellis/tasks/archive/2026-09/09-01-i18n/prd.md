# i18n 多语言全量实时切换

## Goal

为 RHI (WinUI 3 / .NET 8 单文件应用) 增加全量国际化能力，首批支持 5 语言（简中/英/日/韩 + 英语变体），XAML 文案、设置页、详情面板、弹窗、Service 提示文案全部可翻译，支持跟随系统语言与设置页实时切换（无重启），为后续 Crowdin/社区翻译留扩展点。

用户价值：非英语用户可用母语完成 HDR 模组安装流程，降低上手成本；后续新增语言仅需新增资源文件，无需代码改动。

## Background

- 现状：`MainWindow.xaml`（3066 行，~250+ `Text=` + 110 `Content=`，361+ 绑定文案）、`DetailPanelBuilder`（5309 行）、`DialogService`、各 `*.Events.*.cs` / `ShaderPopupHelper` / `SettingsHandler` 等共 ~357 个 `ContentDialog` 调用、~382 条详情面板字符串、~144 条 `ActionMessage` 均为硬编码英文；无任何 `resx/resw/CultureInfo` 基础设施；`App.xaml.cs` 未设置 `CultureInfo`；`SettingsViewModel` 通过 `settings.json` 持久化但无语言键。
- 约束：WinUI 3 `net8.0-windows10.0.19041.0`，macOS 本地无法编译需 Windows CI 验证；单文件发布需 `ExcludeFromSingleFile=false` 资源可被打包；需兼容已有 `settings.json` 老用户（缺 key 即跟随系统）。

## Requirements

### R1 语言集合与回退
- R1.1 首批 5 语言：`en-US`（默认/fallback，覆盖美/英）、`zh-CN`（简体中文）、`zh-TW`（繁體中文，覆盖“中”第二形态）、`ja-JP`（日本語）、`ko-KR`（한국어）。如用户坚持“美/英”分开，则 `en-GB` 仅作为 `en-US` 的 alias/覆盖层（复用同一文件，差异 key 单独覆盖）。
- R1.2 语言标识统一小写带横杠（`en-us` 归一化），资源缺失时按 `当前语言 → en-US → key 本身` 三级回退，不抛异常。
- R1.3 `settings.json` 新增 `Language` 键：`"System"` | `"en-US"` | `"zh-CN"` | `"zh-TW"` | `"ja-JP"` | `"ko-KR"`，默认 `"System"`（跟随 OS，`CultureInfo.CurrentUICulture` 映射到最接近支持语言，否则 `en-US`）。

### R2 实时切换与跟随系统
- R2.1 设置页提供语言下拉（6 选项含 `Follow System`），切换后 200ms 内全 UI 刷新，无需重启；已打开的 `ContentDialog` 下次打开即生效。
- R2.2 `Follow System` 时监听 `Window`/`App` 语言变化（或轮询 `CurrentUICulture`），系统语言变更后自动重刷 UI。
- R2.3 语言偏好持久化到 `SettingsViewModel`，重启后恢复；迁移老用户 `settings.json` 无 `Language` 时视为 `System`。

### R3 全量文案覆盖
- R3.1 XAML：`MainWindow.xaml`、`SetupWindow.xaml`、所有 `Themes/DarkTheme.xaml` 可见文案、`Converters` 文案抽取。
- R3.2 Code-behind：`DetailPanelBuilder*.cs`、`DialogService*.cs`、`ShaderPopupHelper`、`DragDropHandler*`、`InstallEventHandler`、`SettingsHandler`、`MassDeployHandler` 等所有 `ContentDialog` / `InfoBar` 文案。
- R3.3 ViewModel/Service：`MainViewModel.*`、`GameCardViewModel.*`、`FilterViewModel`、各 `*Service` 中暴露给 UI 的 `ActionMessage`/`InstallProgress`/`StatusText`/`ErrorMessage`。
- R3.4 例外不译：日志 `CrashReporter.Log`、文件名/注册表路径、版本号、URL、Shader/Addon ID 等技术标识。

### R4 技术方案（推荐）
- R4.1 采用 **JSON + `ILocalizationService`** （非 `.resw`），理由见 `design.md` Trade-offs：打包友好、热重载易、跨平台构建无需 WinUI 资源工具链、支持参数化与复数、便于未来接入 Crowdin。
- R4.2 资源置于 `RenoDXCommander/Assets/Languages/{lang}.json`（扁平 key，`Section.Key` 命名，如 `Settings.Language.Title`），`en-US.json` 为权威全量，CI 校验其它语言 key 覆盖率。
- R4.3 Key 命名规范：`{Area}.{Component}.{Key}` + `{Area}.{Component}.{Key}.Tooltip` / `.Placeholder` 后缀，避免 `Null`/`None` 歧义。
- R4.4 参数化：`{0}` / `{name}` 占位，`ILocalizationService.GetString(key, args)` 内部 `string.Format`，缺参不抛异常仅 `CrashReporter.Log`。

### R5 兼容与非目标
- R5.1 兼容旧 `settings.json`，新增键缺省不影响启动。
- R5.2 单测可在 Windows Runner 验证；macOS 本地可跑 `RenoDXCommander.Tests` 对 `LocalizationService` 纯逻辑测试。
- R5.3 不支持按游戏/按卡片单独语言；不支持 RTL 布局；不做机器翻译，缺译回退英文。

## Acceptance Criteria

- [ ] AC1 语言资源：`Assets/Languages/en-US.json` 为全量基准（≥ 500 key，覆盖 R3 范围）；`zh-CN/ja-JP/ko-KR/zh-TW` 初始机器翻译占位 + 人工校对关键路径（启动、设置、安装按钮、错误弹窗），CI 打印覆盖率，允许非 `en-US` 缺 key 但运行时回退英文不崩溃。
- [ ] AC2 持久化与跟随：`settings.json` 含 `Language`，`SettingsViewModel.LoadSettingsFromDict/SaveSettingsToDict` 读写正确；默认 `System` 时 `CultureInfo.CurrentUICulture` 为 `zh-CN/ja-JP/ko-KR` 能自动选中对应语言，为 `fr-FR` 等不支持语言回退 `en-US`；切换后重启仍保持。
- [ ] AC3 实时切换：设置页下拉切换 6 选项，`MainWindow.xaml` 标题、导航、过滤器占位、`GameCountText`、设置页各段标题/描述、详情面板 `Components/HDR Mods` 等分区标题、至少 10 个 `ContentDialog` 标题/按钮在 200ms 内刷新；无重启、无闪退。
- [ ] AC4 全量覆盖：`grep -rn 'Text="[^"]*[a-zA-Z\u4e00-\u9fa5]' RenoDXCommander/MainWindow.xaml` 硬编码文案清零（除 `RHI` 品牌字）；`DialogService.Game.cs` / `DetailPanelBuilder.*` / `SettingsHandler.PurgeCachedFiles` 等至少 20 处弹窗标题/内容改为 `GetString`；`FilterViewModel` / `GameCardViewModel` 状态文案可翻译。
- [ ] AC5 回退与健壮性：`GetString("Missing.Key")` 返回 `Missing.Key`；格式化参数缺失不抛异常；单语言 JSON 损坏时 `CrashReporter.Log` 并回退 `en-US`，应用可启动；日志保留英文不被翻译。
- [ ] AC6 可测试：`RenoDXCommander.Tests` 新增 `LocalizationTests`（≥ 10 用例）：回退、参数化、System 映射、持久化 round-trip、缺 key 覆盖率统计；`dotnet test --filter Localization` 在 Windows runner 通过。
- [ ] AC7 文档与发布：`Assets/Languages/README.md` 说明新增语言步骤；`RHI_PatchNotes.md` 记录 i18n；`publish.bat` / `Directory.Build.props` 确保语言 JSON 随单文件发布复制到 `publish/RHI`。

## Out of Scope

- 机器翻译自动流水线、在线 Crowdin 同步（仅预留结构）
- RTL 语言（ar/he）、复数/性别复杂语法（仅 `{0}` 占位）
- 按游戏独立语言、字体回退（CJK 字体假设系统已安装）
- WinUI `x:Uid` / `.resw` 方案（已评估弃用，见 design.md）

## Open Questions

- 无阻塞问题。`en-GB` 是否独立由后续 PR 根据实际文案差异决定（默认 alias `en-US`）。

## Notes

- 参考规范：`.trellis/spec/app/directory-structure.md`、`service-patterns.md`、`viewmodel-patterns.md`；日志遵循 `logging-guidelines.md`。
- 实施前需 `design.md` + `implement.md` 评审通过。
- 环境约束：本机 macOS 无 .NET，仅本地验证 JSON/覆盖率/硬编码扫描；`dotnet build/test/publish` 仅通过 GitHub Actions `workflow_dispatch` 手动触发验证，编译与打包拆为独立 workflow，禁止 `push`/`pull_request` 自动触发。

