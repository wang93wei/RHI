# RHI App Development Guidelines

> C# WPF (WinUI 3, .NET 8, `net8.0-windows10.0.19041.0`) 桌面应用 — 单一可执行文件 `RHI.exe`。

---

## Overview

RHI (RenoDX HDR Injector) 是管理 PC 游戏 HDR 模组的桌面工具：自动检测 8 个商店的游戏、安装/更新 10 个组件（ReShade、RenoDX、DXVK 等）、管理着色器/插件、DLSS/Streamline、NVIDIA Profile 等。项目无前后端分离，只有一个主工程 `RenoDXCommander` + 测试工程 `RenoDXCommander.Tests` + 辅助进程 `RHI.DropHelper`。

## Guidelines Index

| Guide | Description |
|-------|-------------|
| [Directory Structure](./directory-structure.md) | 工程、文件夹、文件命名与组织规则 |
| [Service Patterns](./service-patterns.md) | Service 拆分、DI、partial class、接口约定 |
| [ViewModel Patterns](./viewmodel-patterns.md) | MVVM (CommunityToolkit.Mvvm)、ObservableProperty、partial ViewModel |
| [Error Handling](./error-handling.md) | 异常捕获、CrashReporter、静默失败边界 |
| [Logging Guidelines](./logging-guidelines.md) | CrashReporter.Log / session 日志 / Verbose 开关 |
| [Quality Guidelines](./quality-guidelines.md) | 编码规范、禁止模式、测试要求 |

## How to Fill These Guidelines

每条规则必须能在代码库中找到证据：

- 引用真实文件路径 + 符号名
- 说明何时适用、如何验证
- 列出禁止模式及历史坑

语言：所有文档 **英文**。
