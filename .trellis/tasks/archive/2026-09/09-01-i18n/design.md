# Design: i18n 多语言全量实时切换

## Architecture

```
Assets/Languages/
  en-US.json   ← 权威全量，CI 基准
  zh-CN.json
  zh-TW.json
  ja-JP.json
  ko-KR.json
  README.md

RenoDXCommander/
  Services/
    ILocalizationService.cs
    LocalizationService.cs          // 单例，ObservableObject，INotifyPropertyChanged
    LocalizationService.Fallback.cs // 回退/格式化/缺key 统计
  Converters/
    LocConverter.cs                 // 可选：IValueConverter 桥（若不用 MarkupExtension）
  Markup/
    LocExtension.cs                 // x:Bind / Binding 友好：{loc:Loc Key=Settings.Title}
  ViewModels/
    SettingsViewModel.cs            // 新增 Language 属性 + OnLanguageChanged
  Assets/Languages/README.md
```

核心：单例 `LocalizationService` 持有 `CurrentLanguage`、`FallbackLanguage="en-US"`、`Dictionary<string, Dictionary<string,string>> _catalogs`，加载 JSON，暴露 `string GetString(string key, params object[] args)` 与 `string this[string key]` 索引器，并通过 `INotifyPropertyChanged` / `LanguageChanged` 事件驱动全 UI 刷新。

## Data Flow

1. 启动：`App.xaml.cs` 构造 `LocalizationService`（最早注册），`LoadCatalogs()` 同步读取 `Assets/Languages/*.json`（打包路径 + 開發路徑回退），`ResolveSystemLanguage()` 将 `CultureInfo.CurrentUICulture.Name` 映射到支持集合，否则 `en-US`。
2. 读取 `settings.json` → `SettingsViewModel.Language`（默认 `"System"`），`LocalizationService.ApplyLanguage(settings.Language)`。
3. XAML 侧：`Text="{Binding [MainWindow.Title], Source={StaticResource Loc}}"` 或 `Text="{loc:Loc Settings.Title}"`；`Source` 指向 `LocalizationService` 单例，Key 缺失时 Binding 回退 key 本身。
4. C# 侧：`_loc.GetString("Dialog.Overwrite.Title", card.InstallPath)`；所有 `ContentDialog.Title/Content/PrimaryButtonText` 构造时即取译文。
5. 切换：设置页 `ComboBox SelectionChanged` → `SettingsViewModel.Language = val` → `LocalizationService.CurrentLanguage = mapped` → `OnPropertyChanged("")`（全量刷新）或 `LanguageChanged` 事件 → 已绑定控件自动重算。
6. 持久化：`SettingsViewModel.SaveSettingsToDict` 写入 `Language`；`LoadSettingsFromDict` 回读。

## Contracts

### ILocalizationService
```csharp
public interface ILocalizationService : INotifyPropertyChanged
{
    string CurrentLanguage { get; set; } // "en-US" | "zh-CN" | ...
    string FallbackLanguage { get; }     // "en-US"
    IReadOnlySet<string> SupportedLanguages { get; }
    string GetString(string key, params object[] args);
    string this[string key] { get; }     // 索引器等价 GetString(key)
    event EventHandler<string>? LanguageChanged; // newLanguage
    double Coverage(string lang);        // 已翻译 key / en-US 总数
    string ResolveSystemLanguage();      // CultureInfo → 支持语言
}
```

### JSON 结构
扁平键值，点分命名：
```json
{
  "App.Title": "RHI",
  "App.Subtitle": "Simplified PC Gaming",
  "Nav.Games": "Games",
  "Settings.Title": "Settings",
  "Settings.Language.Title": "Language",
  "Settings.Language.System": "Follow System",
  "Dialog.Overwrite.Title": "⚠ Unknown {0} Detected",
  "Filter.Placeholder": "Filter games..."
}
```
约定：Tooltip 用 `.Tooltip` 后缀，Placeholder 用 `.Placeholder`，Button 用 `.Button`。

### SettingsViewModel 新增
```csharp
[ObservableProperty] private string _language = "System"; // System | en-US | ...
partial void OnLanguageChanged(string value) {
  if (IsLoadingSettings) return;
  App.Services.GetRequiredService<ILocalizationService>().CurrentLanguage =
      value == "System" ? _loc.ResolveSystemLanguage() : value;
  SaveSettingsPublic();
}
```

## 技术选型与 Trade-offs

| 方案 | 优点 | 缺点 | 结论 |
|------|------|------|------|
| **JSON + ILocalizationService (推荐)** | 打包简单（Content Copy）、无需 WinUI 资源编译、可在 macOS 构建、CI 易校验覆盖率、参数化灵活、热重载友好 | 需自实现 Binding 刷新（`OnPropertyChanged("")`） | ✅ 采用 |
| `.resw` + `x:Uid` + `ResourceLoader` | 系统原生、自动跟随系统语言、工具链成熟 | 需 `PRI` 生成、单文件发布路径复杂、实时切换需重启或手动 `ResourceContext.QualifierValues` 刷新、参数化弱、macOS 无法编译 | ❌ |
| `.resx` + 强类型 | VS 设计器支持、强类型 | 同 resw，且 WinUI 不原生支持 resx 动态切换 | ❌ |

实时刷新实现：`LocalizationService` 继承 `ObservableObject`，`CurrentLanguage` setter 内 `OnPropertyChanged("")`（空串表示全属性变更），所有 `{Binding [Key], Source={StaticResource Loc}}` 自动重算；对 `x:Bind`（编译期绑定）不支持动态刷新，故 XAML 中可译文本统一用 `{Binding}` 而非 `{x:Bind}`，或保留 `x:Bind` 给非文案（图标/数字）。

## Compatibility & Migration

- `settings.json` 无 `Language` 键：视为 `"System"`，`LoadSettingsFromDict` 默认赋值，不写回直到用户首次切换（避免无意义 diff）。
- 旧版本回退：新 `settings.json` 带 `Language` 在旧版本被忽略（未知 key 保留但不使用），不影响启动。
- 单文件发布：`RenoDXCommander.csproj` 新增 `<Content Include="Assets\Languages\*.json" CopyToOutputDirectory="PreserveNewest" ExcludeFromSingleFile="true" />`，`publish.bat` 同步复制（仿 `ReShade.ini` 模式）。
- `en-US.json` 为全量基准，CI 脚本 `tools/check-i18n-coverage.ps1` 校验其它语言缺 key 仅警告不阻断发布（允许机器翻译占位）。

## Risks & Mitigations

- **R1 漏译**：`grep` 兜底扫描硬编码文案；`LocalizationService.GetString` 对缺 key 返回 key 并 `CrashReporter.Log`，便于灰度发现。
- **R2 x:Bind 刷新失效**：规范要求可译文本用 `Binding`，CR 阶段 `grep` `x:Bind.*Text="` 人工复核。
- **R3 性能**：JSON 全量约 500 key × 5 语言 ≈ 15KB，内存常驻可忽略；启动同步读取，失败回退英文，不阻塞 UI。
- **R4 翻译质量**：关键路径（安装/错误弹窗）人工校对，其余机器翻译占位 + `README` 引导社区 PR。

## Operational

- 回滚：删除 `Assets/Languages` 引用并将 `GetString` 退化为 `return key` 即可回退英文；`settings.json` 中 `Language` 键保留无害。
- 观测：`CrashReporter.Log("[Localization] Missing key: {key} lang={lang}")`；设置页底部显示覆盖率 `Coverage(lang).P0% translated`（调试用）。

## References

- `RenoDXCommander/App.xaml.cs:26-140` DI 注册位
- `RenoDXCommander/SettingsHandler.cs:32-306` 设置页初始化位
- `RenoDXCommander/MainWindow.xaml:39-1400` XAML 文案位
- `RenoDXCommander/DetailPanelBuilder*.cs` 动态文案位
- `RenoDXCommander/ViewModels/SettingsViewModel.cs:127-342` 持久化位
