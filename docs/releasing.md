# NuGet 发布流程

仓库通过 `.github/workflows/publish-nuget.yml` 自动发布 `src` 下的六个 NuGet 包。

## 首次配置

仓库使用 NuGet.org Trusted Publishing，通过 GitHub Actions OIDC 换取短期 API Key，不保存长期
NuGet API Key。

1. 在 GitHub 仓库创建 `nuget` Environment，并将部署标签限制为 `v*.*.*`。
2. 在该 Environment 中创建 Secret `NUGET_USER`，值为 NuGet.org profile name（不是邮箱）。
3. 在 NuGet.org 的 **Trusted Publishing** 中创建 GitHub Policy：
   - Repository owner：`QuickerOrg`
   - Repository：`WpfMarkdownViewer`
   - Workflow file：`publish-nuget.yml`
   - Environment：`nuget`
4. 在 NuGet.org 创建或确认 `WpfMarkdownViewer`、`WpfMarkdownViewer.Highlighting`、
   `WpfMarkdownViewer.Math`、`WpfMarkdownViewer.Svg`、`WpfMarkdownViewer.Mermaid` 和
   `WpfMarkdownViewer.All` 的包所有权。
5. 建议为包 ID 设置前缀保留，并至少配置两个组织维护者。

workflow 需要 `id-token: write` 权限，并使用 `NuGet/login@v1` 在推送前取得约一小时有效的临时
API Key。不要在仓库中创建或保存长期 `NUGET_API_KEY`。

私有 GitHub 仓库首次建立 Policy 时，NuGet.org 可能提供 7 天临时激活窗口。应在窗口内完成一次
成功发布，让 NuGet.org 根据 OIDC 中的仓库和所有者 ID 永久绑定 Policy；窗口过期后可在 NuGet.org
重新启动。

## 发布版本

发布由符合 SemVer 的标签触发：

```powershell
git tag v0.1.0
git push origin v0.1.0
```

预发布标签也受支持，例如 `v0.2.0-preview.1`。workflow 会依次执行还原、Release 构建、测试、
打包、上传构建产物、通过 OIDC 登录和推送 NuGet.org。标签中的 `v` 不会进入包版本。

发布前应确认：

- `main` 的 CI 已通过；
- README、变更说明和第三方声明已更新；
- 包版本符合语义化版本规则；
- 标签指向准备发布的提交。

NuGet.org 不允许覆盖已发布版本。若发布内容有误，请发布新版本，不要移动或重用标签。
