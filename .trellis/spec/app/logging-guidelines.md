# Logging Guidelines

> Structured logging via `CrashReporter` in RHI.

---

## System

RHI 无 Serilog/NLog/ILogger，采用自研 `CrashReporter` 静态类 + `ICrashReporter` 接口。

| 组件 | 路径 | 说明 |
|------|------|------|
| 静态核心 | `Services/CrashReporter.cs` | 环形缓冲 + 文件写入 + 全局异常注册 |
| 接口 | `Services/ICrashReporter.cs` | `Log` / `WriteCrashReport` / `VerboseLogging` |
| 适配器 | `Services/CrashReporterService.cs` | DI 注入用，转发到静态类 |
| 存储 | `%LocalAppData%\RHI\logs\` | `crash_*.txt` + `session_*.txt` 各最多 10 个 |

参考：`Services/CrashReporter.cs:14-50` (配置常量、路径、AppVersion)。

## Log Levels (Implicit)

无显式级别，通过消息前缀与 Verbose 开关区分：

| 场景 | 写法 | 例子 |
|------|------|------|
| 关键流程 | `CrashReporter.Log("[Service] Action")` | `[AddonPackService] Fetching Addons.ini...` |
| 警告 | `Log($"[Service] Warning: ...")` | `[AddonPackService] Parse warning: ...` |
| 错误 | `Log($"[Service.Method] Failed — {ex.Message}")` | `[AuxInstallService] Stale file migration failed — ...` |
| 调试 | `VerboseLogging=true` 时额外 `Log` | 仅 verbose 模式写入的详细上下文 |

参考：`Services/AddonFileWatcher.cs:66-168`, `Services/AddonPackService.cs:192-278`。

## Format

```
[HH:mm:ss.fff] [Service] message
```

- 时间戳由 `CrashReporter` 自动添加 (`CrashReporter.cs:30` 附近)。
- 消息前缀 `[ServiceName]` 或 `[Service.Method]`，方括号包裹，便于过滤。
- Session 日志首行：`═══ RHI v{Version} — Session started {Date} ═══`。

## When to Log

- **必须 Log**：安装/卸载/更新开始与结束、文件删除/拷贝、下载开始、解析警告、捕获的异常。
- **建议 Log**：游戏扫描开始/结束、引擎检测结果、注册表读取失败。
- **Verbose 才 Log**：逐文件扫描细节、高频轮询。

## Verbose Switch

```csharp
public static bool VerboseLogging { get; set; } // CrashReporter.cs
public bool VerboseLogging { get => _verboseLogging; set { ... Log("Verbose logging enabled"); } }
```

由 `SettingsViewModel.VerboseLogging` 暴露到 UI (`MainViewModel.cs:45`)，用户可在设置中开关。Session 日志始终写入，不受开关影响。

## Breadcrumbs

- 内存环形缓冲 `MaxBreadcrumbs=300`，崩溃时随报告一起写入。
- 调用 `CrashReporter.Log()` 即同时入缓冲 + 写 session 文件 (`CrashReporter.cs:30-60`)。
- 崩溃报告含面包屑轨迹，便于复现「崩溃前发生了什么」。

## Anti-Patterns

- **不要** 直接 `File.AppendAllText` 写日志，统一走 `CrashReporter.Log`。
- **不要** 记录敏感信息（完整路径可，token/密钥不可）。
- **不要** 在高频循环中无条件 `Log`，用 `VerboseLogging` 守卫。
- **不要** 忘记在 `catch` 中 `Log`，否则崩溃报告无上下文。
