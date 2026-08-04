# 贡献指南

感谢你愿意改进 WpfMarkdownViewer。

## 开始之前

- Bug 和功能建议请先提交 issue，说明使用场景、预期行为和最小复现。
- 安全问题不要公开提交 issue，请按 `SECURITY.md` 私下报告。
- 大范围 API 或架构改动请先讨论，避免实现方向与项目边界不一致。

## 本地开发

需要 Windows 和 .NET 10 SDK。

```powershell
dotnet restore WpfMarkdownViewer.slnx
dotnet build WpfMarkdownViewer.slnx --configuration Release --no-restore
dotnet test tests/WpfMarkdownViewer.Tests/WpfMarkdownViewer.Tests.csproj --configuration Release --no-build
```

涉及渲染或交互的修改，请同时运行 `samples/WpfMarkdownViewer.Demo`，验证完整 Markdown、
流式输出、浅色/深色主题、选择复制和窗口尺寸变化。

## 提交要求

- 保持改动聚焦，不进行无关格式化或重构。
- 新行为应有相应测试；修复 Bug 时优先添加能复现问题的回归测试。
- 公共 API 变更应同步更新 README 和相关 ADR。
- 新增依赖时需说明必要性，并更新 `THIRD-PARTY-NOTICES.md`。

提交贡献即表示你有权按本仓库的 MIT License 授权该贡献。
