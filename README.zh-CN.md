# WpfMarkdownViewer

[English](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/README.md) | [简体中文](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/vpre/WpfMarkdownViewer.All.svg?label=NuGet)](https://www.nuget.org/packages/WpfMarkdownViewer.All)

一个原生 **WPF** Markdown 组件，面向 AI 生成内容，提供接近 ChatGPT 的视觉效果和流畅的增量流式渲染——不使用浏览器、WebView2 或 JavaScript。

![WpfMarkdownViewer 渲染效果预览](https://raw.githubusercontent.com/QuickerOrg/WpfMarkdownViewer/main/docs/images/wpfmarkdownviewer-preview.png)

它被设计为 AI 功能（多模型提供商、插件输出、动作说明）的长期共享基础设施，而不是一次性查看器。渲染器使用 `TextFormatter` + `DrawingContext` 自绘；令牌流入时只重新渲染末尾正在生成的块，块完成后则收敛到 `Markdig` 的权威解析结果。

```text
AI 令牌流 → AppendDelta(...) → 自适应节流 → 流式块解析器
          → 只重绘 Active Block → 完成块 → 以 Markdig 为准
```

## 主要特性

- **为流式输出而生。** `AppendDelta` 线程安全；自适应计时器按离散节奏刷新，并且只重新渲染活动块。已完成块保持不可变并被复用。
- **原生自绘，性能优先。** 不使用 `FlowDocument`，也不会在每个令牌到达时重建整棵视觉树。块级、视口级和消息级虚拟化使长对话仍能保持响应。
- **ChatGPT 风格会话外壳。** 可选的 `ConversationView` 提供用户气泡、全宽助手消息、复制/重新生成操作栏和消息级虚拟化。
- **丰富内容。** 支持标题、强调、列表、任务列表、表格、引用、带 TextMate 高亮和复制按钮的围栏代码、位图与 SVG、LaTeX 数学公式以及 Mermaid 图表，全部原生渲染。
- **选择与复制。** 可跨块、跨消息拖动选择，拖到视口边缘时自动滚动，并能复制为尽可能忠实的纯文本 Markdown（保留代码围栏、管道表格和 Mermaid 源码）。
- **主题化。** 提供强类型 `MarkdownStyle`、协调一致的浅色/深色预设，并支持运行时切换。
- **轻量核心、按需插件。** 核心程序集只依赖 Markdig。语法高亮、LaTeX、SVG 和 Mermaid 位于独立插件中，可按需注册；缺少能力时会平稳降级。

## 运行要求

- Windows、.NET 10、WPF（`net10.0-windows`）。

## 从 NuGet 安装

安装包含全部能力的包：

```powershell
dotnet add package WpfMarkdownViewer.All --prerelease
```

也可以只选择应用需要的包：

| 包 | 内容 |
| --- | --- |
| [`WpfMarkdownViewer`](https://www.nuget.org/packages/WpfMarkdownViewer) | 核心渲染器 |
| [`WpfMarkdownViewer.Highlighting`](https://www.nuget.org/packages/WpfMarkdownViewer.Highlighting) | TextMate 语法高亮 |
| [`WpfMarkdownViewer.Math`](https://www.nuget.org/packages/WpfMarkdownViewer.Math) | LaTeX 数学公式 |
| [`WpfMarkdownViewer.Svg`](https://www.nuget.org/packages/WpfMarkdownViewer.Svg) | SVG 图片 |
| [`WpfMarkdownViewer.Mermaid`](https://www.nuget.org/packages/WpfMarkdownViewer.Mermaid) | Mermaid 图表 |
| [`WpfMarkdownViewer.All`](https://www.nuget.org/packages/WpfMarkdownViewer.All) | 核心和全部内置插件 |

## 能力插件

核心渲染器除 **Markdig** 外不依赖其他第三方库。较重的渲染能力位于可选插件程序集中；使用前引用所需插件，并在开始渲染前注册一次：

| 能力 | 插件程序集 | 注册槽 | 底层实现 |
| --- | --- | --- | --- |
| 语法高亮 | `WpfMarkdownViewer.Highlighting` | `Capabilities.Highlighting` | TextMateSharp(.Grammars) |
| LaTeX 数学公式 | `WpfMarkdownViewer.Math` | `Capabilities.Math` | WpfMath |
| SVG 图片 | `WpfMarkdownViewer.Svg` | `Capabilities.Svg` | SharpVectors.Reloaded |
| Mermaid 图表 | `WpfMarkdownViewer.Mermaid` | `Capabilities.Mermaid` | Mermaider + Mostlylucid.Dagre（依赖 SVG 插件） |

```csharp
// 只选择需要的能力：
WpfMarkdownViewer.Rendering.Capabilities.Highlighting = new TextMateHighlighter();
WpfMarkdownViewer.Rendering.Capabilities.Math         = new WpfMathRenderer();
WpfMarkdownViewer.Rendering.Capabilities.Svg          = new SvgRenderer();
WpfMarkdownViewer.Rendering.Capabilities.Mermaid      = new BuiltInMermaidRenderer();

// 或引用 WpfMarkdownViewer.All 元包，一次注册全部内置能力：
WpfMarkdownViewer.DefaultCapabilities.RegisterAll();
```

未注册某项能力时，内容会平稳降级：代码不着色，数学公式显示原始 `$…$` 文本，SVG 显示替代文本，Mermaid 显示为围栏代码块。

## 快速开始：流式文档

使用 `MarkdownScrollHost` 包装渲染器；它负责视口、粘底跟随和“跳到最新内容”交互：

```xml
<ctrl:MarkdownScrollHost xmlns:ctrl="clr-namespace:WpfMarkdownViewer.Controls;assembly=WpfMarkdownViewer"
                         x:Name="Host">
    <ctrl:MarkdownDocumentView x:Name="Viewer" />
</ctrl:MarkdownScrollHost>
```

```csharp
Viewer.LinkClicked += (_, e) => OpenInBrowser(e.Url); // 组件本身不会执行导航

await foreach (var token in model.StreamAsync(prompt))
    Viewer.AppendDelta(token); // 可从任意线程安全调用

Viewer.Complete();             // 完成并收敛到 Markdig 的权威解析结果
```

其他生命周期方法：`Reset()`（重新生成）、`Abort()`（取消流）、`SetMarkdown(string)`（直接渲染完整文档）。

## 快速开始：会话外壳

```csharp
var chat = new ConversationView { MarkdownStyle = MarkdownStyle.Dark };
Host.Content = chat;
chat.LinkClicked += (_, e) => OpenInBrowser(e.Url);
chat.MessageRegenerateRequested += (_, e) => Regenerate(e.MessageIndex);

chat.AddMessage(ChatRole.User, "解释一下快速排序。");

chat.StartMessage(ChatRole.Assistant);
await foreach (var token in model.StreamAsync(prompt))
    chat.AppendDelta(token);
chat.CompleteMessage();
```

## 主题

`MarkdownStyle` 是不可变 record，可以使用 `with` 派生定制样式：

```csharp
Viewer.ApplyTheme(MarkdownStyle.Dark);

Viewer.MarkdownStyle = MarkdownStyle.Light with
{
    BaseTypeface = new Typeface("Microsoft YaHei"),
    EmSize = 17,
    ParagraphLineHeight = 1.85,
    LinkBrush = new SolidColorBrush(Color.FromRgb(0x7c, 0x3a, 0xed)),
    HeadingScales = new[] { 2.1, 1.7, 1.4, 1.2, 1.1, 1.0 },
};
```

字体、字号、字重、行高、边距、列表缩进、颜色、配套 TextMate 代码主题和聊天气泡颜色均可配置。

## Markdown 支持范围

| 类别 | 支持内容 |
| --- | --- |
| 块级 | `#`–`######` 标题、段落、嵌套有序/无序列表、任务列表、引用、围栏代码（支持横向滚动）、GFM 表格与列对齐、分隔线、图片、块级数学公式 |
| 行内 | 粗体、斜体、删除线、行内代码、普通/引用式链接、裸 URL 自动链接、高亮、下划线、上下标、行内数学公式、硬换行、有限的原始 HTML |
| 数学 | `$…$`、`$$…$$`、`\(…\)` 和 `\[…\]`（通过 WpfMath 渲染 LaTeX） |
| 图片 | PNG/JPG 等位图和 SVG；支持 HTTP(S)（磁盘缓存和 ETag 重新验证）、本地文件、`data:` URI、`pack://`/资源 |
| 图表 | Mermaid 围栏代码——纯 .NET、输出矢量图 |
| 代码 | TextMate 语法高亮、多语言、语言栏、复制按钮，流式生成时实时重新高亮 |

行内标记最终会收敛到 Markdig：任意已完成块的流式预览应与 Markdig 对同一文本的解析一致，测试套件会验证这一约束。

## Mermaid（可插拔）

`WpfMarkdownViewer.Mermaid` 插件通过纯 .NET 管线将 Mermaid 渲染为矢量图：Mermaider 生成 SVG，Mostlylucid.Dagre 为流程图提供更接近 mermaid.js 的分层布局。无需浏览器。

可以通过 `Capabilities.Mermaid` 替换渲染引擎：

```csharp
// 禁用：Mermaid 将降级为代码块
WpfMarkdownViewer.Rendering.Capabilities.Mermaid = null;

// 或换成自己的远程服务、WebView2 等实现
Capabilities.Mermaid = new MyMermaidRenderer(); // 实现 IMermaidRenderer
```

## 选择与复制

- 可跨块选择；在会话外壳中还可跨消息选择。拖动到视口边缘时会自动滚动。
- `Ctrl+C` 或 `CopySelection()` 复制为纯文本 Markdown：代码块保留围栏，表格重建为管道格式，Mermaid 保留源码。
- `SelectAll()` 选择当前已经实现的全部内容。

## 主要公共 API

**`MarkdownDocumentView`**（`Panel`）——单个流式文档：
`AppendDelta`、`Complete`、`Reset`、`Abort`、`SetMarkdown`、`SelectAll`、`CopySelection`、`ApplyTheme`、`MarkdownStyle`、`ImageBasePath`、`VirtualizationEnabled`、`ShrinkToContentWidth`、`SelectionEnabled`，以及 `LinkClicked`、`DocumentChanged` 事件。

**`MarkdownScrollHost`**（`Grid`）——视口和自动滚动：
`Content`、`IsStickToBottom`、`JumpToLatest()`、`ScrollToTop()`。

**`ConversationView`**（`Panel`）——可选会话外壳：
`StartMessage`、`AppendDelta`、`CompleteMessage`、`AddMessage`、`Clear`、`SelectAll`、`CopySelection`、`ApplyTheme`、`MarkdownStyle`、`MessageCount`、`VirtualizationEnabled`、`AlwaysShowActions`，以及 `LinkClicked`、`MessageCompleted`、`MessageRegenerateRequested` 事件。

**`MarkdownStyle`**（record，命名空间 `WpfMarkdownViewer.Rendering`）——提供 `Light` / `Dark` 预设。

**`Capabilities`**（静态类，命名空间 `WpfMarkdownViewer.Rendering`）——保存高亮、数学、SVG、Mermaid 能力槽。元包中的 `DefaultCapabilities.RegisterAll()` 可一次注册全部内置实现。

## 架构

设计决策记录在 [`docs/adr`](https://github.com/QuickerOrg/WpfMarkdownViewer/tree/main/docs/adr)，领域术语记录在 [`CONTEXT.md`](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/CONTEXT.md)。主要决策包括：

- 使用自建块渲染器，不采用 FlowDocument/WebView2（ADR-0001）。
- 以 Markdig 为权威，流式解析器必须最终收敛（ADR-0002）。
- 单文档核心 + 可选会话外壳（ADR-0004）；非聊天场景直接使用核心。
- 借助不可变的已完成块实现两级虚拟化（ADR-0006）。
- 为自绘文本与选择使用扁平的可见空格行内 run（ADR-0005/0007）。
- 渲染器只读；导航与安全策略由宿主负责（ADR-0009）。
- 轻量核心 + 能力插件：重依赖位于独立程序集，使用方只承担实际启用的能力成本。

## 构建与测试

```powershell
dotnet build WpfMarkdownViewer.slnx
dotnet test tests/WpfMarkdownViewer.Tests/WpfMarkdownViewer.Tests.csproj
```

运行 `samples/WpfMarkdownViewer.Demo` 可以查看流式播放、主题切换、自定义样式和聊天记录；传入 `--conversation` 可直接进入会话模式。Demo 还会把参考快照写入 `artifacts/`。

## 依赖

核心程序集 `WpfMarkdownViewer` 只有一个第三方依赖：

| 包 | 用途 | 协议 |
| --- | --- | --- |
| Markdig | 权威 Markdown 解析 | BSD-2-Clause |

其他能力位于按需插件中：

| 插件 | 包 | 用途 | 协议 |
| --- | --- | --- | --- |
| `.Highlighting` | TextMateSharp(.Grammars) | 代码语法高亮 | MIT |
| `.Math` | WpfMath | LaTeX 数学公式 | MIT（包含另行授权的字体） |
| `.Svg` | SharpVectors.Reloaded | SVG → WPF 矢量图 | BSD-3-Clause |
| `.Mermaid` | Mermaider | 纯 .NET Mermaid → SVG | MIT |
| `.Mermaid` | Mostlylucid.Dagre | 流程图分层布局 | MIT |

## 状态与路线图

流式管线、会话外壳、数学公式、SVG、Mermaid 和主要体验优化均已实现，并由 200 多项测试覆盖。参见 [`docs/milestone-1.md`](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/docs/milestone-1.md) 和 [`docs/milestone-3.md`](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/docs/milestone-3.md)。

近期完成：流式“输入中”光标、长代码行横向滚动、表格列对齐、嵌套列表、有限原始行内 HTML、裸 URL 自动链接、引用式链接、硬换行、右键菜单、图片点击放大，以及有边界的图片/图表缓存。

尚未实现：脚注，以及结构化的逐块屏幕阅读器无障碍支持。

## 贡献与安全

欢迎贡献，请阅读 [`CONTRIBUTING.md`](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/CONTRIBUTING.md)。安全问题请按照 [`SECURITY.md`](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/SECURITY.md) 私下报告。NuGet 发布配置和标签发布流程见 [`docs/releasing.md`](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/docs/releasing.md)。

## 许可证

WpfMarkdownViewer 使用 [MIT License](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/LICENSE)。第三方组件保留各自许可证，详见 [`THIRD-PARTY-NOTICES.md`](https://github.com/QuickerOrg/WpfMarkdownViewer/blob/main/THIRD-PARTY-NOTICES.md)。
