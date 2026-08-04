# MdXaml 对比调研笔记

> 调研对象：`whistyun/MdXaml`  
> 调研日期：2026-07-26  
> 一手资料范围：项目 README、当前 `master` 源码、GitHub 发布记录、NuGet 包页。  
> 当前主干基准：[`121282f5453fb0575efd7a59ac428d09cdf04e3d`](https://github.com/whistyun/MdXaml/commit/121282f5453fb0575efd7a59ac428d09cdf04e3d)（2026-03-08，合并 PR #108）。

## 先给结论

1. **MdXaml 是一个“Markdown 直接生成 WPF `FlowDocument`”的完整库族，不只是解析器。**它同时提供转换引擎、`MarkdownScrollViewer`、值转换器、WPF 样式、链接/锚点/图片处理，以及 HTML、SVG、GIF、YAML Front Matter、AvalonEdit 代码高亮等可选插件。项目 README 对其定位和最小使用方式有明确说明：[README](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/README.md#L1-L46)。
2. **比较时必须区分稳定版 1.27.0 和当前主干/2.0.0 预览版。**NuGet 上稳定版仍是 `1.27.0`（2024-02-06），最新包是 `2.0.0-pre202603081301`（2026-03-08）；2.0.0 预览只包含 `net462` 与 `net8.0-windows7.0` 资产，而当前源码将版本写为 2.0.0。来源：[NuGet 版本表](https://www.nuget.org/packages/MdXaml#versions-body-tab)、[2.0.0 预览目标框架](https://www.nuget.org/packages/MdXaml/2.0.0-pre202603081301#supportedframeworks-body-tab)、[主干构建属性](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/Directory.Build.props#L11-L14)。
3. **不能依据上游资料声称某个替代库“性能一定优于 MdXaml”。**MdXaml 仓库没有 BenchmarkDotNet、专用 benchmark 项目、基准数据或发布说明中的性能对比。仓库中出现的 `Stopwatch` 只用于 UI 自动化测试的启动/渲染超时和文件删除重试，并不是性能基准：[VisualTest](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/tests/VisualTest/Class1.cs#L37-L109)。
4. **“完全替代”应按兼容矩阵验证，不能只比较 Markdown 是否能显示。**至少要覆盖：语法输出、`FlowDocument` 结构和 `Tag`、样式资源、绑定/API、链接行为、锚点、图片 URI 与异步加载、HTML/SVG/GIF、代码高亮、Front Matter、目标框架和异常/线程语义。MdXaml 的这些能力分散在核心包与插件包中。

## 针对本仓库的最终判断

### 结论

- **不能把 WpfMarkdownViewer 称为 MdXaml 的完全替代品。**两者的核心契约不同：MdXaml 的中心是 `Markdown -> FlowDocument`，本库的中心是只读、自绘、可增量更新的 `MarkdownDocumentView`。现有 MdXaml 调用方若依赖 `FlowDocument`、`TextPointer`、分页/打印、ResourceDictionary 样式、绑定转换器或插件接口，无法只换包名/控件名完成迁移。
- **在“.NET 10 WPF 中显示 AI 流式回答”这个收窄场景，本库可以替代 MdXaml，而且设计更合适。**它提供线程安全的 `AppendDelta`、自适应刷新、只重建 Active Block 的视觉、块级和消息级虚拟化、跨块/跨消息选择复制、粘底滚动、会话外壳、数学公式和 Mermaid。这些都是 MdXaml 的 `FlowDocument` 全文替换路径没有直接提供的能力。入口和约束见[项目 README](../README.md)及 [`MarkdownDocumentView`](../src/WpfMarkdownViewer/Controls/MarkdownDocumentView.cs)。
- **本机微基准支持“目标场景更快”，但不支持“所有场景总体更快”。**纯文本样本的一次性渲染约快 2 倍；86 次增量更新的整轮耗时约为 MdXaml 全文替换路径的 1/70。图片、HTML、SVG/GIF、代码高亮、数学和 Mermaid 没有纳入，因此不能把结果外推到全功能配置。
- **当前仓库仍不具备成熟替代品的完整交付状态。**它尚未发布首个 NuGet 版本，只面向 `net10.0-windows`，公开自定义 Block 扩展面仍关闭。浮动依赖问题已经修复，并已准备可重复的 NuGet 打包与自动发布流程；仍需经过实际发布和生产使用验证，再讨论生产级全面迁移。

### 能力边界对照

| 维度 | WpfMarkdownViewer | MdXaml 主干 | 判断 |
|---|---|---|---|
| 核心输出 | 自绘 `Panel`/Block visuals | `FlowDocument` | 不兼容，不能 drop-in |
| 静态 Markdown | Markdig 完整解析后自绘 | 自有 Regex 解析后创建 WPF 文档树 | 都可用，语法/树结构不等价 |
| AI 流式更新 | 原生 `AppendDelta`，节流并复用已 Finalize Block | Markdown 变化时同步重建全文 | 本库明显更适合 |
| 长对话 | Block + message 两级虚拟化、Conversation Shell | 单个 `FlowDocumentScrollViewer` | 本库更适合 |
| 选择/复制 | 跨块、跨消息；重建 Markdown/HTML/plain text | 使用 WPF 文档选择能力 | 目标不同 |
| 数学/Mermaid | 可选原生插件 | 主干无对应内置插件 | 本库占优 |
| HTML/SVG/GIF/Front Matter | HTML 仅有限 inline；SVG 有；无 GIF/Front Matter | 均有独立插件 | MdXaml 覆盖更广 |
| 扩展面 | 高亮/数学/SVG/Mermaid四个能力槽；Block renderer 未公开 | block/inline/filter/loader/style/viewer 等公开插件集合 | MdXaml 更强 |
| WPF 文档能力 | 不提供编辑、`TextPointer`、分页/打印 | 继承 `FlowDocument` 生态 | MdXaml 占优 |
| 兼容性/交付 | `net10.0-windows`，未发 NuGet | `net462` + `net8.0-windows` 预览；另有广泛使用的 1.27 稳定包 | MdXaml 更成熟 |

### 当前库自身的性能限制

本库的视觉复用是真实的，但当前流式解析器仍在每次刷新时重新分段完整累计源码；代码注释也将“只重新分段 unsettled tail”列为后续优化：[`StreamingBlockParser`](../src/WpfMarkdownViewer/Streaming/StreamingBlockParser.cs)。因此：

- “只重建 Active Block”不等于整条管线是严格的 O(增量长度)；
- 文档越来越长、刷新粒度特别小时，完整源码规范化和重新分段仍会累积 CPU/分配；
- 虚拟化主要降低长文档稳态的已实现视觉数量，不会免除首次解析和首次测量成本；
- 当前 `CodeBlockView` 会格式化并保存代码块的全部行，尚不能把“超大单个代码块”也视为已经具备行级虚拟化。

## 本机对照微基准

### 方法

- 环境：Windows x64，.NET SDK `10.0.302`，单个 STA WPF 进程，Release；
- 对方版本：MdXaml `master` 的 `121282f5453fb0575efd7a59ac428d09cdf04e3d`（2.0.0 预览线）；
- 样本：27,345 字符，120 组标题、段落、中英文混排、粗斜体、行内代码、链接、列表和表格；
- 布局宽度：900 DIP；每次更新后调用 `Measure`、`Arrange`、`UpdateLayout`；
- 静态路径：新建 viewer，设置完整 Markdown，然后布局；
- 增量路径：按 320 字符切成 86 个 delta；本库每次 `AppendDelta` 后强制一次刷新，MdXaml 每次把增长后的完整字符串重新赋给 `Markdown`；
- 双方均只测核心能力，不注册高亮、SVG、HTML、GIF、数学或 Mermaid 插件；
- 数值经过预热后取多次操作均值；分配量来自当前 STA 线程的 `GC.GetAllocatedBytesForCurrentThread`。

### 结果

| 场景 | WpfMarkdownViewer | MdXaml | 本库相对结果 |
|---|---:|---:|---:|
| 静态：平均耗时 | 175.30 ms | 352.26 ms | 约 2.01× 快 |
| 静态：线程分配 | 26,674.4 KiB/op | 47,134.9 KiB/op | 约少 43% |
| 86 次增量：整轮平均耗时 | 192.34 ms | 13,412.02 ms | 约 69.7× 快 |
| 86 次增量：线程分配 | 41,432.9 KiB/op | 2,114,446.3 KiB/op | 约少 98% |

### 如何解释

增量差距很大并不意外：本库复用已 Finalize Block 的视觉，而 MdXaml 没有增量 API，等价迁移路径只能针对每个前缀重新解析并重建完整 `FlowDocument`。这组结果足以支持“AI token/delta 高频更新时，本库的当前实现明显更合适”。

但它不是通用性能排名，原因包括：

- MdXaml 被测的是完整替换路径，而不是上游不存在的增量路径；
- 样本没有图片、代码高亮和其他插件；
- 分配统计不覆盖其他线程，也不能代替峰值工作集、GC pause 和 UI 帧时间；
- 没测冷启动、网络图片、深层嵌套、超大单 Block、打印和滚动回收；
- 本库未启用 `MarkdownScrollHost` 视口，因此静态结果没有借助虚拟化占便宜；另一方面也没有测试虚拟化滚动重建成本。

## 构建与测试核验

- 本库核心项目在空 RAMDisk 构建目录中可 fresh restore/build：0 warning、0 error。
- 使用仓库现有、解析到 `Mermaider 0.8.0` 的资产执行完整构建，随后 211 项测试全部通过。
- 调研时在空 RAMDisk 做完整 fresh restore，发现 `WpfMarkdownViewer.Mermaid.csproj` 的 `Mermaider` 与 `Mostlylucid.Dagre` 都使用 `Version="*"`；2026-07-26 实际解析到 `Mermaider 0.12.1`，接口已把 `StrictModeOptions` 改为 `StrictStylingOptions`，因此出现 `CS0246` 与 `CS0535`。该问题随后已修复：依赖分别固定为兼容的 `Mermaider 0.8.0` 与 `Mostlylucid.Dagre 2.0.1`，完整 fresh restore/build 和 211 项测试均已通过。项目文件见 [`WpfMarkdownViewer.Mermaid.csproj`](../src/WpfMarkdownViewer.Mermaid/WpfMarkdownViewer.Mermaid.csproj)。
- MdXaml 当前主干的核心 `MdXaml.Test` 在 `net8.0-windows` 下 fresh restore/build 后 66 项测试全部通过，但编译有现存 nullable warnings。

固定依赖和可发布、可复现的 NuGet 构建已经完成。后续若要继续支撑性能结论，最优先的工程动作是加入可长期运行的 benchmark 项目，并在首个 NuGet 版本发布后验证真实消费路径。

## 版本与包形态

### 稳定版与当前主干不是同一个集成形态

- NuGet 稳定版 `MdXaml 1.27.0` 包含 `net45`、`net462`、`.NET Core 3.0` 和 `net6.0-windows7.0` 资产，并依赖 AvalonEdit 和 `MdXaml.Plugins`：[NuGet 1.27.0 框架及依赖](https://www.nuget.org/packages/MdXaml/1.27.0#supportedframeworks-body-tab)。
- 最新预览 `2.0.0-pre202603081301` 只包含 `net462` 与 `net8.0-windows7.0`，基础 `MdXaml` 包只列出 `MdXaml.Plugins` 依赖：[NuGet 2.0.0 预览框架及依赖](https://www.nuget.org/packages/MdXaml/2.0.0-pre202603081301#supportedframeworks-body-tab)。
- 当前源码将功能拆成：
  - `MdXaml`：核心转换、控件、样式、图片基础能力；
  - `MdXaml.Plugins`：插件接口和注册表；
  - `MdXaml.SyntaxHigh`：AvalonEdit 代码高亮；
  - `MdXaml.Html`：HTML；
  - `MdXaml.Svg`：SVG；
  - `MdXaml.AnimatedGif`：GIF；
  - `MdXaml.FrontMatter`：YAML Front Matter；
  - `MdXaml.Full`：聚合包。  
  这可由[解决方案目录](https://github.com/whistyun/MdXaml/tree/121282f5453fb0575efd7a59ac428d09cdf04e3d)和 [`MdXaml.Full.csproj`](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.Full/MdXaml.Full.csproj#L28-L37)核对。

这意味着替换判断要先确定比较对象：

- 如果目标是替换生产中常见的 1.27.0，需要考虑它仍把 AvalonEdit 集成在基础包中的既有行为/API。
- 如果目标是替换最新源码，则应对齐 2.0.0 的模块化边界，并把它仍处于 NuGet 预览状态作为迁移风险。

## 功能与 API

### 1. 核心转换与 WPF 集成

- `Markdown.Transform(string)` 返回 `FlowDocument`；另有公开的 `RunBlockGamut` 与 `RunSpanGamut`，允许调用方或插件复用块级/行内转换：[Markdown 核心入口](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L155-L176)、[块级与行内公开入口](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L270-L301)。
- `MarkdownScrollViewer` 是 `FlowDocumentScrollViewer` 派生控件，公开 Markdown 内容、样式、源 URI、片段锚点、链接命令、语法版本、插件和图片加载选项等属性；Markdown 变化后会重新生成整个文档：[属性与控件实现](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/MarkdownScrollViewer.cs#L314-L418)、[更新流程](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/MarkdownScrollViewer.cs#L116-L146)。
- `TextToFlowDocumentConverter` 实现 `IValueConverter`，可在 XAML Binding 中把字符串转换为 `FlowDocument`，并允许注入 `Markdown` 引擎和样式：[转换器源码](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/TextToFlowDocumentConverter.cs#L12-L98)。
- 内置 `Standard`、`Compact`、`GithubLike`、`Sasabune`、`SasabuneStandard`、`SasabuneCompact` 样式入口：[MarkdownStyle](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/MarkdownStyle.cs#L7-L72)。

### 2. Markdown 与自定义扩展语法

核心实现覆盖标题、段落、粗体/斜体、删除线、下划线、链接、图片、列表、引用、水平线、围栏与缩进代码块、表格、Emoji 等；测试集分别覆盖列表、表格、水平线、图片和 Emoji：[核心测试目录](https://github.com/whistyun/MdXaml/tree/121282f5453fb0575efd7a59ac428d09cdf04e3d/tests/MdXaml.Test)。

MdXaml 还定义了一组非标准/增强语法：

- 字母与罗马数字有序列表；
- 表格单元格换行、rowspan、colspan、单元格对齐；
- Textile 风格段落对齐；
- 删除线、下划线和颜色文字。  

格式示例来自项目自己的[增强语法文档](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/docs/original_enhance.md)。

`SyntaxManager` 定义 `Plain`、`Standard`、`MdXaml` 三类语法能力组合，并可分别开关 Note、表格、扩展水平线、对齐、删除线、扩展列表标记、Textile inline、图片尺寸：[SyntaxManager](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.Plugins/SyntaxManager.cs#L8-L68)。因此“Markdown 支持”不能只以 CommonMark/GFM 名称笼统比较，必须用相同语料检查实际 `FlowDocument` 树。

### 3. 链接、文档导航与图片

- `MarkdownScrollViewer.ClickAction` 支持浏览器打开、显示相对路径文档、显示全部文档、安全打开、安全显示和只高亮等策略；内部还会先处理当前 `FlowDocument` 的锚点：[链接动作选择](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/MarkdownScrollViewer.cs#L447-L493)、[ClickAction 枚举](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/MarkdownScrollViewer.cs#L652-L669)。
- 图片地址支持绝对路径、基于 `BaseUri` 的 pack URI、基于 `AssetPathRoot` 的 URI/文件路径等候选顺序：[图片候选 URI 构造](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L755-L797)。
- 图片默认走异步加载；`DisabledLazyLoad=true` 反而切换为同步等待。HTTP/HTTPS 位图通过 `ConcurrentDictionary<Uri, WeakReference<BitmapImage>>` 做弱引用缓存，UI 元素构造会切回 Dispatcher：[图片加载入口](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/ImageLoaderManager.cs#L21-L98)、[Dispatcher 与缓存写入](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/ImageLoaderManager.cs#L180-L273)。
- 可选加载器支持 SVG 和动画 GIF；`MdXaml.Full` 会自动注册这些插件：[Full Markdown 插件注册](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.Full/Markdown.cs#L10-L31)。

### 4. HTML、代码高亮与 Front Matter

- HTML 是可选插件，不应理解成基础包天然完整支持任意 HTML。插件通过 `HtmlAgilityPack` 解析并注册块级/行内解析器；包说明本身称其为 “cheap html processor”：[MdXaml.Html 项目依赖](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.Html/MdXaml.Html.csproj#L37-L50)、[HTML 插件注册](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.Html/HtmlPluginSetup.cs#L1-L19)。
- 代码高亮插件为每个代码块创建只读 AvalonEdit `TextEditor`，根据语言名加载高亮定义并处理滚轮/上下文菜单：[AvalonCodeBlockLoader](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.SyntaxHigh/AvalonCodeBlockLoader.cs#L13-L82)。
- Front Matter 插件只识别文档开头由 `---` 包围的 YAML，成功解析后从正文中剥离，并把 `YamlNode` 附加到 `FlowDocument`；解析失败则保留原文继续转换：[FrontMatterFilter](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.FrontMatter/FrontMatterFilter.cs#L9-L77)。
- `MdXaml.Full.MarkdownScrollViewer` 自动加入 Front Matter、HTML、SVG、GIF，且继承带代码高亮的 viewer；但 `MdXaml.Full.Markdown` 的自动注册列表没有 Front Matter。这一细微差异见 [Full viewer](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.Full/MarkdownScrollViewer.cs#L14-L35) 与 [Full engine](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.Full/Markdown.cs#L10-L31)。

### 5. 插件接口

`MdXamlPlugins` 暴露以下扩展集合：预处理/转换 Filter、顶层块解析器、块解析器、行内解析器、图片加载器、WPF 元素加载器、代码块加载器、高亮定义、样式覆写器、Viewer arranger 和插件 setup。集合改变会通知引擎重建解析配置，实例可克隆：[MdXamlPlugins](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.Plugins/MdXamlPlugins.cs#L10-L106)。

这是替代兼容性中的高风险面：如果现有调用方实现了 `IBlockParser`、`IInlineParser`、`IImageLoader`、`IElementLoader`、`ICodeBlockLoader`、`IFilter`、`IStyleOverwriter` 或 `IViewerArranger`，新库即使显示效果相同，也不是 API 级完全替代。

## 架构与性能相关实现

### 转换管线

当前主干不是基于 Markdig AST 的适配层，而是保留 MarkdownSharp/Markdown.Xaml 风格的自有转换器：

1. `Transform` 先做文本规范化；
2. 可选 Filter chain 逐层包装转换；
3. 核心解析器用大量预编译 Regex 扫描文本；
4. 块级和行内解析会递归处理子串；
5. 直接创建 WPF `Block`、`Inline`、`Table`、`List`、`InlineUIContainer` 等对象并组成 `FlowDocument`。  

证据：[Transform 管线](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L164-L176)、[解析器配置](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L178-L267)、[块级扫描](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L303-L398)、[行内递归扫描](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L400-L444)。

### 有利于性能的实现点

- 大部分核心正则表达式使用 `RegexOptions.Compiled`，避免每次解释正则；示例见[标题与表格正则](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L806-L822)和[表格正则](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L1447-L1464)。
- 解析参数在插件更新时构建为数组，不是在每一个 Markdown token 上重新组装插件集合：[ParseParam](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L2349-L2378)。
- 网络图片默认异步，并有弱引用缓存，减少同一远程位图仍存活时的重复下载/解码。
- 代码高亮定义有内部缓存：[InternalHighlightManager](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.SyntaxHigh/Highlighting/InternalHighlightManager.cs#L20-L63)。

### 可能成为瓶颈或响应性风险的实现点

以下是从源码推导出的工程风险，不是上游发布的 benchmark 结论：

- `MarkdownScrollViewer.UpdateMarkdown` 在依赖属性变化回调中同步调用 `Engine.Transform`，随后一次性替换整个 `Document`。没有增量解析、局部 DOM/文档树复用或取消机制；长文档和高频更新时，解析与大量 WPF 对象创建会直接进入交互延迟：[UpdateMarkdown](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/MarkdownScrollViewer.cs#L116-L146)。
- 每次 viewer 更新都会 clone 插件、重建引擎插件配置，再转换全文；插件数量增加时还会增加每段文本的候选 Regex 匹配：[UpdateMarkdown](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/MarkdownScrollViewer.cs#L116-L141)、[候选解析器循环](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/Markdown.cs#L303-L340)。
- 块级/行内解析包含递归、`Substring`、临时 `List<T>`、候选排序以及 `AddRange`，复杂嵌套语料的分配量和最坏延迟需要实测，不能只根据“编译正则”判断快慢。
- 带代码高亮时，每个 fenced code block 都创建一个 AvalonEdit `TextEditor` 控件；大量代码块时，成本明显不同于只创建 `Run`/`Paragraph` 的轻量实现：[AvalonCodeBlockLoader](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml.SyntaxHigh/AvalonCodeBlockLoader.cs#L21-L82)。
- `MarkdownScrollViewer.Open` 对 HTTP/HTTPS 文档在非 .NET Framework 路径使用 `DownloadTextAsync(...).GetAwaiter().GetResult()`，调用链仍是同步等待；若从 UI 路径触发，慢网络会影响响应性：[文档打开实现](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/MarkdownScrollViewer.cs#L571-L631)。
- 图片缓存只在 HTTP/HTTPS 位图路径写入，且是弱引用；本地/pack/data 图片和已被回收的远程位图仍可能重复解码：[缓存条件](https://github.com/whistyun/MdXaml/blob/121282f5453fb0575efd7a59ac428d09cdf04e3d/MdXaml/ImageLoaderManager.cs#L266-L273)。

## Benchmark 证据审计

对当前主干的项目文件、源码、README、docs、tests 搜索 `Benchmark`、`BenchmarkDotNet`、`performance`、`perf`、`Stopwatch` 后：

- 没有 BenchmarkDotNet 依赖；
- 没有 benchmark 项目；
- 没有固定语料、迭代/预热方案、耗时/分配/峰值内存数据；
- 没有与其他 WPF Markdown 库的对照结果；
- 唯一实际的 `Stopwatch` 使用位于 `tests/VisualTest/Class1.cs`，作用是 5 秒启动/绘制超时与文件删除重试，不记录吞吐量或分配。  

因此，关于“解析更快”“首屏更快”“内存更低”或“整体性能更好”的任何判断，都需要在双方相同版本、相同功能开关和相同 WPF 呈现路径下补做 benchmark。上游现有资料无法支持这些断言。

## 用于“完全替代”验收的最小矩阵

| 维度 | 必测项目 |
|---|---|
| 基础语法 | 标题、段落、软/硬换行、粗斜体嵌套、转义、链接、图片、引用、列表、代码、水平线 |
| 增强语法 | GFM 表格、表格 rowspan/colspan、字母/罗马列表、Note、对齐、颜色、下划线、图片尺寸、Emoji |
| 输出契约 | `FlowDocument` 的 Block/Inline 类型、层级、`Tag`、表格跨度、列表 `MarkerStyle`/`StartIndex` |
| WPF API | `Markdown.Transform`、`MarkdownScrollViewer` 的 DP 与 Binding、`TextToFlowDocumentConverter`、样式替换 |
| 导航 | 外部链接命令、回调、相对 Markdown 导航、标题锚点、Fragment 滚动、安全打开策略 |
| 资源 | file/pack/http/https/data URI、`BaseUri`、`AssetPathRoot`、失败占位、异步图片、缓存、SVG、GIF |
| 富内容 | HTML 支持范围、未知标签策略、AvalonEdit 语言高亮、自定义 xshd、YAML Front Matter |
| 扩展接口 | 全部 parser/loader/filter/style/viewer 插件接口及更新/克隆语义 |
| 兼容性 | 1.27.0 与 2.0.0 预览的目标框架、包拆分、程序集/命名空间、强名称、依赖差异 |
| 性能 | 冷/热解析、首次显示、连续更新、长文档、深层列表、大表格、多代码块、多图片、峰值内存、GC、UI 卡顿 |

## 建议的性能对比口径

若要得出可审查的性能结论，至少分开测：

1. **纯转换时间**：Markdown 字符串到完整 `FlowDocument`，禁用图片网络 I/O；
2. **首次可见时间**：设置 viewer 内容到 Dispatcher 完成布局/首屏可见；
3. **更新代价**：同一 viewer 连续替换 10/100 次短文和长文；
4. **分配与峰值内存**：尤其关注 WPF `TextElement`、表格、AvalonEdit 控件；
5. **图片路径**：本地、pack、HTTP 冷缓存、HTTP 热缓存分别测；
6. **功能等价配置**：双方都关闭或都开启 HTML、代码高亮、SVG/GIF，不能拿“轻量模式”与“全功能模式”直接比较。

在没有完成这些对照实验前，最稳妥的表述是：**可以比较架构上的潜在优势与风险，但不能断言总体性能优劣；可以列出功能覆盖度，但不能宣称 API/行为级完全替代。**
