# Quality Guidelines

> Code standards for RHI (C# / WinUI 3 / .NET 8).

---

## Language & Project Settings

- `TargetFramework net8.0-windows10.0.19041.0`, `UseWinUI true`, `Nullable enable`, `ImplicitUsings enable`, `AllowUnsafeBlocks true` (`RenoDXCommander.csproj:4-18`)。
- `AssemblyName RHI`, `ApplicationManifest app.manifest`, `NoWarn NU1902`。
- `InternalsVisibleTo RenoDXCommander.Tests` 允许测试访问 internal。

## Code Style

- **可空**：`Nullable` 启用，所有可空引用类型显式 `?`，如 `string?`, `RemoteManifest?`。
- **隐式 using**：启用，无需 `using System` 等常见命名空间。
- **并发**：`ConcurrentDictionary` 用于缓存 (`GameDetectionService.cs:18`)，`MaxScanDepth=4` 限制扫描深度。
- **分部类**：大类必须拆 partial，见 `service-patterns.md` / `viewmodel-patterns.md`。
- **常量**：服务内 `private const` 集中顶部，如 `MaxScanDepth` / `MaxLogFiles` / `MaxBreadcrumbs`。

## Forbidden Patterns

| 禁止 | 原因 | 替代 |
|------|------|------|
| `async void` (非事件) | 异常无法捕获 | `async Task` |
| `new HttpClient()` | 破坏连接复用 | 注入单例 `HttpClient` |
| 在 Service 中弹 UI | 分层破坏 | `DialogService` / VM 通知 |
| 在构造函数中 IO/网络 | 启动阻塞 | `InitializeAsync` |
| 手写 `INotifyPropertyChanged` | 冗余 | `[ObservableProperty]` |
| `File.*` 不包 `try/catch` | 单文件失败崩全流程 | `try/catch` + `CrashReporter.Log` + `continue` |
| 直接 `Registry`/`Process` 不判空 | 空路径崩溃 | `string.IsNullOrEmpty` 守卫 |

## Testing

- **框架**：xUnit (`RenoDXCommander.Tests/RenoDXCommander.Tests.csproj`)。
- **策略**：仅测纯逻辑，无文件/网络/DI (`CoreLogicTests.cs:7` 注释)。
- **例子**：`ResolveAutoReShadeFilename` (7 个用例覆盖 DX8/DX9/DX11/DX12/OpenGL/混合/空集)、`ViewLayout` 循环。
- **新增测试**：优先为纯函数/分支逻辑添加用例；涉 IO 的逻辑应抽纯函数后再测。

## Verification

```bash
dotnet build RenoDXCommander.sln -c Release
dotnet test RenoDXCommander.Tests/RenoDXCommander.Tests.csproj
```

- 提交前确保 `build` 无警告（除 `NU1902` 已屏蔽）、`test` 全绿。
- XAML 修改后需启动验证主题与布局（`Themes/DarkTheme.xaml`）。

## Common Mistakes

- 忘记在 `App.xaml.cs` 注册新 Service → 运行时 `GetRequiredService` 抛异常。
- 新增 `RemoteManifest` 字段未加 `[JsonPropertyName]` → 反序列化静默丢失。
- `SavedGameLibrary` 新增集合未加 `StringComparer.OrdinalIgnoreCase` → 大小写不一致导致重复。
- `GameDetectionService` 新增商店检测未复用 `MaxScanDepth` / `_engineCache` → 性能回退。
- 在 `catch` 中吞异常不 `Log` → 崩溃报告无线索。
