# Directory Structure

> How code is organized in RHI (single-repo, C# WPF).

---

## Solution Layout

```
RHI.sln
├── RenoDXCommander/          # 主应用 WinExe (net8.0-windows10.0.19041.0, WinUI3)
│   ├── App.xaml / App.xaml.cs          # DI 容器、单例启动、全局 HttpClient
│   ├── MainWindow.xaml / .xaml.cs      # 主窗口骨架
│   ├── MainWindow.*.cs                 # 5 个 partial：Events / Install / Settings / Skeleton / UISync
│   ├── Services/            # ~125 个文件，核心业务逻辑
│   ├── ViewModels/          # MainViewModel (+13 partial) / GameCardViewModel (+10 partial) / FilterViewModel / SettingsViewModel
│   ├── Models/              # 纯 DTO / 记录类型
│   ├── Collections/         # BatchObservableCollection.cs
│   ├── Controls/            # WrapPanel.cs (自定义布局)
│   ├── Converters/          # XAML 值转换器
│   ├── Themes/              # DarkTheme.xaml
│   ├── Assets/              # 静态资源
│   ├── *.cs                 # 构建器/处理器：DetailPanelBuilder, DragDropHandler, UIFactory, HotkeyManager 等
│   ├── ReShade.ini / ReShade.Vulkan.ini / dxvk.conf / relimiter.ini ... # 打包 Content
│   └── 7z.exe / 7z.dll      # 解压依赖
├── RenoDXCommander.Tests/   # xUnit 测试 (CoreLogicTests.cs)
├── RHI.DropHelper/          # 辅助进程 (拖拽/权限提升)
├── docs/                    # 文档
└── .trellis/                # Trellis 工作流与规范
```

参考：`RenoDXCommander/RenoDXCommander.csproj:1-40` 定义 TargetFramework、UseWinUI、AllowUnsafeBlocks、AssemblyName=RHI。

## Module Organization

### Services/ — 按能力拆分，每个能力一个接口 + 一个实现 + N 个 partial

- 接口命名 `I{Name}Service`，实现 `NameService`，均在 `RenoDXCommander.Services` 命名空间。
- 大服务用 `partial class` 按职责拆文件：`AuxInstallService` (5 文件)、`DlssPresetService` (6 文件)、`DxvkService` (4 文件)、`GameDetectionService` (4 文件)、`MainViewModel` (13 文件)、`GameCardViewModel` (11 文件)。
- 命名后缀反映职责：`*.Detection.cs` / `*.Install.cs` / `*.Staging.cs` / `*.ProfileMatching.cs` / `*.Swap.cs`。

例子：
- `RenoDXCommander/Services/DxvkService.cs` + `DxvkService.Staging.cs` + `DxvkService.Install.cs` + `DxvkService.Tracking.cs`
- `RenoDXCommander/Services/AuxInstallService.cs` + `AuxInstallService.Ini.cs` + `AuxInstallService.Install.cs` + `AuxInstallService.DllIdentification.cs` + `AuxInstallService.GacSymlink.cs`

### ViewModels/ — MVVM，每 VM 一个主文件 + 能力 partial

- `MainViewModel` (总控，~14 个 partial)、`GameCardViewModel` (单游戏卡片，~11 个 partial)、`FilterViewModel`、`SettingsViewModel`。
- partial 后缀即能力：`.DlssStreamline.cs` / `.Dxvk.cs` / `.Luma.cs` / `.ReShade.cs` 等。
- 所有 VM 继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`。

### Models/ — 纯数据，无逻辑

- 文件即类型：`DetectedGame.cs`、`GameMod.cs`、`RemoteManifest.cs`、`SavedGameLibrary.cs`、`InstalledModRecord.cs` 等 (共 30+ 文件)。
- 大模型如 `RemoteManifest.cs` (27KB) 集中远程配置；`SavedGameLibrary.cs` 为本地持久化根。

### 根目录 *.cs — UI 装配与横切

- `DetailPanelBuilder*.cs` (6 文件) 构建详情面板；`DragDropHandler*.cs` (5 文件) 处理拖拽；`DialogService*.cs`、`HotkeyManager.cs`、`UIFactory.cs`、`WindowStateManager.cs` 等。

## Naming Conventions

| 类型 | 规则 | 例子 |
|------|------|------|
| Service 接口 | `I{Pascal}Service` | `IGameDetectionService`, `IDxvkService` |
| Service 实现 | `{Pascal}Service` | `GameDetectionService`, `DxvkService` |
| Service partial | `{Service}.{Capability}.cs` | `DlssStreamlineService.Swap.cs` |
| VM partial | `{VM}.{Capability}.cs` | `GameCardViewModel.Dxvk.cs` |
| Model | 单数名词 Pascal | `DetectedGame`, `SavedGameLibrary` |
| 常量文件名 | `{Name}Constants.cs` | `DllOverrideConstants.cs` |
| XAML | 同名 .xaml + .xaml.cs | `MainWindow.xaml` |

## Examples

- **新增一个组件服务**：参考 `DofFixService.cs` (单文件小服务) 或 `DxvkService` (partial 大服务) 的接口/注册/partial 拆分方式。
- **新增 VM 能力**：参考 `GameCardViewModel.DofFix.cs` / `MainViewModel.Dxvk.cs` 的 partial 扩展方式。
- **新增模型**：参考 `Models/DxvkVariant.cs` (enum) / `Models/DxvkInstalledRecord.cs` 的简洁 DTO 风格。
