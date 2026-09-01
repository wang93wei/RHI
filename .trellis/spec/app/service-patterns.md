# Service Patterns

> How business logic is structured in `RenoDXCommander/Services/`.

---

## Dependency Injection

- **容器**：`Microsoft.Extensions.DependencyInjection`，在 `RenoDXCommander/App.xaml.cs:26-140` 集中注册。
- **生命周期**：几乎全部 `AddSingleton`；唯二例外是 `MainWindow` 为 `AddTransient`，`HttpClient` 为自定义 `SocketsHttpHandler` 的 Singleton。
- **共享 HttpClient**：单例，`SocketsHttpHandler` 配置 `EnableMultipleHttp2Connections=true`、`MaxConnectionsPerServer=16`、`PooledConnectionLifetime=10min`，`User-Agent: RHI/2.0`，`Timeout=10min`。服务内用 `CancellationTokenSource` 做更细粒度超时。
- **循环依赖**：用 `Lazy<T>` 打破，如 `Lazy<IDxvkService>` / `Lazy<IDlssStreamlineService>` (`App.xaml.cs:87-95`)。
- **别名注册**：`IAuxFileService` 复用 `AuxInstallService` 实例 (`App.xaml.cs:77`)。

参考：`RenoDXCommander/App.xaml.cs:64-130` 完整注册表。

### Adding a New Service

1. 定义接口 `I{Name}Service` 于 `Services/I{Name}Service.cs`。
2. 实现 `NameService : I{Name}Service`，必要时用 `partial` 拆文件。
3. 在 `App.xaml.cs` 注册 `AddSingleton<INameService, NameService>()`。
4. 构造函数注入所需依赖（HttpClient、其他 Service、ICrashReporter）。

## Partial Class Decomposition

大服务按职责拆 partial，每个 partial 专注一类操作，文件名即职责：

| Service | Partials | 职责 |
|---------|----------|------|
| `AuxInstallService` | `.DllIdentification` / `.GacSymlink` / `.Ini` / `.Install` | DLL 识别、GAC 符号链接、INI 合并、安装流程 |
| `DlssPresetService` | `.DriverSettings` / `.Export` / `.ProfileMatching` / `.ReBar` / `.Reset` | 驱动设置、导出、配置匹配、ReBAR、重置 |
| `DlssStreamlineService` | `.Detection` / `.Swap` | 检测、版本切换 |
| `DxvkService` | `.Staging` / `.Install` / `.Tracking` | 暂存、安装、追踪 |
| `MainViewModel` | 13 partials (BackgroundScan / BuildCards / CacheLoad / Dxvk / GameMatching / Init / Install.* / Settings / Update) | 分层关注点 |
| `GameCardViewModel` | 11 partials (DisplayCommander / DlssStreamline / DofFix / Dxvk / Luma / ...) | 每组件一 partial |

**Rule**: 单文件超 ~500 行即考虑拆 partial；partial 后缀必须是能力名词，禁止 `Part1`/`Part2`。

## Interface Conventions

- 接口与实现一对一，接口名 `I` 前缀 + `Service` 后缀。
- 部分小服务无接口（如 `DofFixService`、`AutoUpdateService`、`DlssPresetService` 直接注册具体类）。
- 检测类服务暴露批量方法：`FindSteamGames()` / `FindGogGames()` / ... / `FindRockstarGames()` + `DetectEngineAndPath()` + `MatchGame()` (`IGameDetectionService.cs:8-25`)。
- 安装类服务暴露 `InstallAsync` / `UninstallAsync` / `UpdateAsync` 模式，返回 `Task`。

## Static Global State (Legacy Pattern)

少数服务用静态全局缓存（历史原因，非新代码推荐）：

- `AuxInstallService.GlobalPeakNits` / `GlobalPeakNitsEnabled` / `GlobalManifest` / `CustomReShadeSelectionResolver` (`AuxInstallService.cs:15-45`) — 由 `SettingsViewModel` / `MainViewModel.Init` 在启动时赋值。

新代码优先构造函数注入，避免新增静态全局。

## Config & Manifest

- 远程配置：`Models/RemoteManifest.cs` (JSON 反序列化，`[JsonPropertyName]` 标注)，通过 `IManifestService` 拉取，字段含 `wikiNameOverrides`、`blacklist`、`engineIniPathOverrides` 等。
- 本地持久化：`Models/SavedGameLibrary.cs` 为根，含 `Games` / `HiddenGames` / `FavouriteGames` / `EngineTypeCache` / `DxvkInstalledVersions` 等，键名多为 `GameName` 或 `GameName|Store` 复合键 (支持多商店同名游戏)。

## Anti-Patterns

- **不要** 在 Service 中直接操作 UI (`MessageBox`/`Window`)，应通过 ViewModel 或 `DialogService`。
- **不要** 在 Service 构造函数中做 IO/网络，应在 `InitializeAsync` 中。
- **不要** 新建 `HttpClient` 实例，注入共享单例。
- **不要** 用 `async void`（除事件处理器），一律 `async Task`。
