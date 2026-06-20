# Milestone 3 — 会话外壳 Conversation Shell（ADR-0004）

## 目标

在已就绪的单 Document 核心之上，提供**可选的、薄的**会话外壳：把多条消息（每条一个 `MarkdownDocumentView`）组合成 transcript，承担核心刻意不做的**消息级**职责——按角色的 ChatGPT 式样式、跨消息自动滚动、以及消息级虚拟化。核心仍然每条消息只渲染一个 Document（ADR-0004）。

非聊天消费者（插件输出、动作文档）继续直接用核心，不经过外壳。

## 阶段 A · 组合与流式（本刀，已完成）

- `ChatRole`：User / Assistant。
- `ConversationView : Panel, IVirtualizingContent`：
  - 纵向堆叠每条消息一个 `MarkdownDocumentView`。
  - **角色样式（ChatGPT 风）**：用户消息右对齐气泡（`UserBubbleBackground`，主题感知）；助手消息全宽无气泡。
  - **流式 API（UI 线程）**：`StartMessage(role)` / `AppendDelta(delta)` / `CompleteMessage()`；静态路径 `AddMessage(role, markdown)`；`Clear()`。活跃消息（尾部）永不虚拟化，类比 Active Block。
  - **两级虚拟化转发**：把宿主视口换算进每条消息坐标后转发给子 `MarkdownDocumentView`，使块级虚拟化（ADR-0006）在外壳内继续生效。
  - **自动滚动**：复用 `MarkdownScrollHost` 黏底逻辑，跟随尾部流式消息。
  - 事件：`LinkClicked`（向上冒泡）、`MessageCompleted`；`MarkdownStyle` 运行时可换（活跃消息原地重绘，已完成消息按需重建）。
- 完成标准：多轮 用户/助手 流式回放，气泡/全宽分明，代码块/表格/列表/公式正常；`ConversationViewTests` 绿。

## 阶段 B · 消息级虚拟化（已完成）

- `MeasureOverride` 两遍：先按缓存/估算高度定位，realize 视口内消息、drop 离屏的**已完成**消息（仅保留缓存高度），再转发子视口 + 测量。
- 离屏丢弃的消息滚回视口时从缓存的 Markdown 源重建（`Realize` 自带 `SetMarkdown`）。活跃（流式）消息永不虚拟化。
- 完成标准：长 transcript 下已实现消息数有界；滚动条/布局稳定不跳。`ConversationViewTests` 虚拟化用例绿。

## 阶段 C · 行内数学（已完成，独立于外壳）

- 流式 `InlineProjector` 识别 `$…$`（best-effort，规则与 Markdig dollarmath 收敛）；`MarkdigInlineProjector` 的 `MathInline` 改投 `InlineStyle.Math`。
- 渲染：`MathInlineObject : TextEmbeddedObject` 把 WpfMath 矢量几何嵌入文本行，按数学轴居中对齐（`InlineMath` 缓存几何）；解析失败回退为等宽文本。
- 复制：`RunSerializer` 把数学 run 还原为 `$latex$`。`InlineConvergeTests` 增数学样例。

## 阶段 D · 聊天观感打磨（已完成）

- **用户气泡随内容收缩**：`MarkdownDocumentView.ShrinkToContentWidth`；外壳仅用户消息开启。
- **复制为纯文本 Markdown**：剪贴板只放 UnicodeText（不再附带 HTML / 自定义格式）；代码块还原 ``` 围栏，表格改为原子选区并重建管线表格。
- **跨消息文本选择**：抽取可复用 `SelectionController`（按根收集 `ISelectableText` 叶子、按 Y 排序）；`MarkdownDocumentView` 委托并新增 `SelectionEnabled`；`ConversationView` 拥有跨全部已实现消息的单一选区。
- **消息操作条**：每条消息下方"复制"（整条 Markdown）；助手消息额外"重新生成"（抛 `MessageRegenerateRequested`）。默认 hover 显示，`AlwaysShowActions` 可常显。

## 阶段 E · 对标 LiveMarkdown.Avalonia 补齐（已完成）

- **数学定界符**：`\(…\)` / `\[…\]` 归一化为 `$…$` / `$$…$$`（跳过围栏代码）。
- **拖选自动滚动**：拖到视口边缘自动滚动并延展选区（`SelectionController` + `IScrollHostAware`）。
- **SVG 图片**：SharpVectors 矢量渲染；图片加载统一为 `ImageLoader`（data URI、pack/资源、http 条件请求 ETag）。
- **Mermaid 图表**：纯 .NET（Mermaider）→ SVG → SharpVectors，全程本地无浏览器；可插拔 `IMermaidRenderer` 扩展点（ADR-0010），内置实现。`MermaidSvgFlattener` 把 Mermaider 的 CSS 变量/`color-mix` 扁平化成 SharpVectors 可解析的内联值。`​```mermaid` 块识别后渲染为矢量图，失败回退为代码块，整块原子选区可复制回 ` ```mermaid` 源码。

## 不在 M3

- 头像、时间戳、点赞点踩等更多聊天 chrome。
- 被消息级虚拟化丢弃的离屏消息不参与跨消息选区（与块级一致）。
- Mermaid：依赖 Mermaider 覆盖的图类型（流程/时序/状态/类/ER/饼/时间线/gitgraph/思维导图…）。
