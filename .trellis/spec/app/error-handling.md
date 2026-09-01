# Error Handling

> How failures are handled in RHI — WinUI desktop app with file/registry/process/network operations.

---

## Philosophy

- **Crash 必须可追溯**：未处理异常由 `CrashReporter` 全局捕获，写入 `%LocalAppData%\RHI\logs\`。
- **IO 操作静默容错**：文件/注册表/进程操作多处 `try/catch` 后 `Log` 而非崩溃，尤其扫描与安装流程。
- **用户可见错误**：通过 `DialogService` / `ActionMessage` 展示，不抛未处理异常到 UI 线程。

## Global Crash Reporting

### CrashReporter (static + service)

- **静态类** `Services/CrashReporter.cs`：`Register(App)` 在 `App.xaml.cs:12` 最早调用，订阅 `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` / WinUI 异常。
- **接口** `ICrashReporter` (`Services/ICrashReporter.cs`)：`Log(string)` / `WriteCrashReport(source, ex, isTerminating, note)` / `VerboseLogging`。
- **实现** `CrashReporterService.cs` 适配静态类，供 DI 注入。
- **日志文件**：`%LocalAppData%\RHI\logs\` 下 `crash_*.txt` (最多 10 个) + `session_*.txt` (最多 10 个)，`MaxBreadcrumbs=300` 环形缓冲。
- **面包屑**：关键路径调用 `CrashReporter.Log("[Service] message")`，崩溃报告包含完整面包屑轨迹。

参考：`Services/CrashReporter.cs:14-80` (初始化、路径、环形缓冲)、`Services/ICrashReporter.cs`。

### Usage

```csharp
// 记录关键步骤
_crashReporter.Log($"[AddonFileWatcher] Watching '{_watchPath}'");
CrashReporter.Log("[AddonPackService] Fetching Addons.ini...");

// 捕获并记录
try { ... }
catch (Exception ex) { CrashReporter.Log($"[Service.Method] Failed — {ex.Message}"); }

// 启动时注册
CrashReporter.Register(this); // App.xaml.cs
```

参考：`Services/AddonFileWatcher.cs:66-87`, `Services/AddonPackService.cs:192-278`。

## Local Error Handling

### IO / Registry / Process — catch + log + continue

典型模式（扫描 8 个商店、文件安装）：

```csharp
try { File.Delete(path); CrashReporter.Log($"Removed '{name}'"); }
catch { } // 忽略单个文件失败，不中断整体流程

try { ... }
catch (Exception ex) { CrashReporter.Log($"[Service] Failed — {ex.Message}"); }
```

参考：`Services/AuxInstallService.cs:220-510` (多处 `catch (Exception ex)` 记录后继续)、`Services/GameDetectionService.cs` (注册表读取容错)。

### Elevated Operations

需提权的文件拷贝通过临时 exe + 进程退出码判断：

```csharp
catch (UnauthorizedAccessException) { // 尝试提权路径
}
throw new IOException($"Elevated copy exited with code {proc.ExitCode}");
```

参考：`Services/AuxInstallService.cs:522-537`。

### Verbose Logging

`CrashReporter.VerboseLogging` / `ICrashReporter.VerboseLogging` 控制详细日志，`SettingsViewModel.VerboseLogging` 转发 (`MainViewModel.cs:45`)。开启后 `Log` 包含更多上下文，默认关闭以减少磁盘写入。

## Validation

- **输入校验**：路径/文件名空检查 `string.IsNullOrEmpty(installPath)` 直接 `return null` (`GameDetectionService.cs:30`)。
- **引擎检测缓存**：`ConcurrentDictionary<string, (path, engine)>` 避免重复扫描，线程安全。
- **远端数据**：`RemoteManifest` 字段多为可空 (`Dictionary<...>?` / `List<string>?`)，消费处判空。

## Testing Error Paths

- 测试聚焦纯逻辑方法，无文件/网络依赖 (`RenoDXCommander.Tests/CoreLogicTests.cs:7` 注释：`Tests pure logic methods — no filesystem, no network, no DI container`)。
- 例子：`ResolveAutoReShadeFilename` 覆盖 DX9/DX11/OpenGL/空集合等分支；`ViewLayout` 循环逻辑。

## Anti-Patterns

- **不要** 在 `catch` 中 `throw;` 而不 `Log`，会丢失面包屑上下文。
- **不要** 用 `async void` 吞异常，一律 `async Task` 并在调用处 `try/catch`。
- **不要** 对用户输入直接 `File.Delete`/`Registry` 而不校验路径合法性。
- **不要** 在循环中对单项失败 `throw` 中断整体，应 `Log` 后 `continue`（扫描/批量安装场景）。
- **不要** 忽略 `UnauthorizedAccessException` 而不尝试提权或提示用户。
