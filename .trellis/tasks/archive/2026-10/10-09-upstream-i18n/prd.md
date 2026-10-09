# 合并上游并继续翻译

## Goal
合并 upstream/main 最新代码到 feat/i18n，保留现有本地化并翻译新增界面内容。

## Requirements
- 当前干净分支 feat/i18n；上游目标 60559eb。
- 补齐新增用户可见字符串，五种语言键保持一致；保留格式占位符与技术名称。
- 提交并推送 origin/feat/i18n，以 GitHub Windows CI 验证构建及测试。
- 本机不安装或运行任何 .NET 内容。
- CI 仅保留 workflow_dispatch；用户明确禁止自动触发。
- 所有改动和本地静态审查完成后，统一触发 CI；不以 CI 探测配置是否可用。

## Acceptance
- upstream/main 是最终 HEAD 的祖先，无合并冲突标记。
- i18n strict、键一致与占位符检查通过。
- GitHub CI 在最终提交构建、测试成功。

## Out of scope
发布、部署和 Windows GUI 验收；不改上游业务行为。
