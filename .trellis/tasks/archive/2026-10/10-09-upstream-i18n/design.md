# Design
使用 git merge 保留双边历史。冲突按上游行为及现有 Loc 绑定合并。新增界面字符串接入现有 ILocalizationService，资源覆盖五种语言。保留现有手动触发配置和 NuGet 缓存；parallel 已由官方文档确认支持。通过当前分支 workflow_dispatch 验证；失败读取日志并定点修复。
