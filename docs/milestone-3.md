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

## 阶段 B · 消息级虚拟化（下一刀）

- 离屏**已完成**消息丢弃其视图但缓存测量高度（粒度从 Block 换成 Message），滚回视口再重建（从缓存的 Markdown 源）。
- 完成标准：长 transcript 下已实现消息数有界；滚动条/布局稳定不跳。

## 不在 M3

- 跨**消息**文本选择（核心内的跨块选择已有；跨消息为后续）。
- 头像、消息操作条（复制整条/重生成）、时间戳等聊天 chrome。
- 用户气泡随短文本收缩宽度（当前填充至最大宽度）。

## 已知与外壳无关的限制

- 行内数学 `$...$` 暂未实现（仅块级 `$$...$$`），在消息中按字面显示。
