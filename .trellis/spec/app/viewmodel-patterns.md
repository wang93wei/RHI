# ViewModel Patterns

> MVVM with `CommunityToolkit.Mvvm` in `RenoDXCommander/ViewModels/` and UI assembly in `MainWindow.*.cs` / `DetailPanelBuilder*.cs`.

---

## Stack

- **MVVM 库**：`CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`)。
- **UI 框架**：WinUI 3 (`Microsoft.UI.Xaml`)，`TargetFramework net8.0-windows10.0.19041.0`。
- **集合**：`BatchObservableCollection<T>` (`Collections/BatchObservableCollection.cs`) 用于批量刷新避免频繁 `CollectionChanged`。

## ViewModel Structure

### Base

所有 VM 继承 `ObservableObject`：

```csharp
public partial class FilterViewModel : ObservableObject { ... }
public partial class GameCardViewModel : ObservableObject { ... }
public partial class MainViewModel : ObservableObject { ... }
```

参考：`ViewModels/FilterViewModel.cs:10`, `ViewModels/GameCardViewModel.cs:8`, `ViewModels/MainViewModel.cs:12`。

### Observable Properties

用 `[ObservableProperty]` 源生成器，字段 `_camelCase` 生成 `PascalCase` 属性 + `OnPropertyChanged`：

```csharp
[ObservableProperty] private string _searchQuery = "";
[ObservableProperty] private string _filterMode = "Detected";
[ObservableProperty] private bool _isInstalling = false;
[ObservableProperty] private GameStatus _status = GameStatus.NotInstalled;
```

参考：`ViewModels/FilterViewModel.cs:30-38`, `ViewModels/GameCardViewModel.cs:9-29`。

变更回调用 `partial void On{Property}Changed(T value)`：

```csharp
partial void OnSearchQueryChanged(string value) => ApplyFilter();
partial void OnShowHiddenChanged(bool value) => ApplyFilter();
```

参考：`ViewModels/FilterViewModel.cs:42-46`。

### Commands

用 `[RelayCommand]` 生成 `ICommand`，方法名即命令名：

- 在 `FilterViewModel` / `GameCardViewModel` 中搜索 `[RelayCommand]` 用法。
- 避免在 code-behind 直接订阅事件，优先 Command 绑定。

### Partial ViewModel Decomposition

`MainViewModel` (36k 行主文件 + 13 partial) 按生命周期/能力拆分：

| Partial | 职责 |
|---------|------|
| `MainViewModel.cs` | 字段、构造、转发属性、共享 helper |
| `.Init.cs` | 启动初始化序列 |
| `.CacheLoad.cs` | 缓存加载 |
| `.BackgroundScan.cs` | 后台扫描 |
| `.GameMatching.cs` | 游戏匹配 |
| `.BuildCards.cs` | 卡片构建 |
| `.Install.cs` / `.Install.Components.cs` / `.Install.Luma.cs` / `.Install.Nexus.cs` | 安装流程 |
| `.Update.cs` | 更新检查 |
| `.Settings.cs` | 设置 |
| `.Dxvk.cs` | DXVK 集成 |

`GameCardViewModel` (11 partial) 每组件一文件：`DisplayCommander` / `DlssStreamline` / `DofFix` / `Dxvk` / `Luma` / `OptiScaler` / `REFramework` / `RenoDX` / `ReShade` / `UltraLimiter`。

**Rule**: 新增组件能力时，新建 `GameCardViewModel.{Component}.cs` + `MainViewModel.Install.{Component}.cs`，不要塞入主文件。

### Settings & Filter

- `SettingsViewModel.cs`：全局设置，属性转发给 `MainViewModel` (`MainViewModel.cs:30-50` 如 `IsReShadeNightly` / `SkipUpdateCheck`)。
- `FilterViewModel.cs`：过滤与搜索，含 `ExclusiveFilters` (Detected/Favourites/Hidden/Installed) 与 `CombinableFilters` (Unreal/Unity/Other/RenoDX/Luma)，通过 `Action` 回调 (`FilterModeChanged` / `PreFilterAction`) 与 `MainViewModel` 协作。

### MainWindow Code-Behind

`MainWindow` 仅做骨架与事件转发，逻辑在 VM：

- `MainWindow.xaml` 定义布局，`MainWindow.xaml.cs` 极薄。
- 事件分 5 个 partial：`Events` / `Events.Components` / `Events.Install` / `Events.Settings` / `FaqBuilder` / `Skeleton` / `UISync`。
- `DetailPanelBuilder*.cs` (6 文件) 用纯 C# 构建详情面板，非 XAML。

## UI Thread & Async

- VM 中耗时操作一律 `async Task`，通过 `IsInstalling` / `InstallProgress` / `ActionMessage` 反馈进度。
- 集合批量更新用 `BatchObservableCollection`，避免逐项通知卡顿。

## Anti-Patterns

- **不要** 在 VM 中直接操作 `File`/`Registry`/`Process`，注入 Service。
- **不要** 在 VM 构造函数中启动异步任务，用 `InitializeAsync`。
- **不要** 手写 `INotifyPropertyChanged`，用 `[ObservableProperty]`。
- **不要** 在 XAML code-behind 写业务逻辑，转发到 VM Command。
- **不要** 跨 VM 直接互相引用，通过 `MainViewModel` 协调或 DI 注入。
