# NuGet 发布流程

仓库通过 `.github/workflows/publish-nuget.yml` 自动发布 `src` 下的六个 NuGet 包。

## 首次配置

1. 在 NuGet.org 创建或确认 `WpfMarkdownViewer`、`WpfMarkdownViewer.Highlighting`、
   `WpfMarkdownViewer.Math`、`WpfMarkdownViewer.Svg`、`WpfMarkdownViewer.Mermaid` 和
   `WpfMarkdownViewer.All` 的包所有权。
2. 创建仅允许推送上述包的 NuGet API Key。
3. 在 GitHub 仓库 **Settings → Secrets and variables → Actions** 中创建 repository secret：
   `NUGET_API_KEY`。
4. 建议在 NuGet.org 为包 ID 设置前缀保留，并至少配置两个组织维护者。

API Key 不应写入项目文件、workflow 参数、日志或 Git 历史。请按 NuGet.org 的有效期策略定期轮换。

## 发布版本

发布由符合 SemVer 的标签触发：

```powershell
git tag v0.1.0
git push origin v0.1.0
```

预发布标签也受支持，例如 `v0.2.0-preview.1`。workflow 会依次执行还原、Release 构建、测试、
打包、上传构建产物和推送 NuGet.org。标签中的 `v` 不会进入包版本。

发布前应确认：

- `main` 的 CI 已通过；
- README、变更说明和第三方声明已更新；
- 包版本符合语义化版本规则；
- 标签指向准备发布的提交。

NuGet.org 不允许覆盖已发布版本。若发布内容有误，请发布新版本，不要移动或重用标签。
