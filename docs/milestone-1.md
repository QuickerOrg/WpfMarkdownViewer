# Milestone 1 — 流式管线垂直切片

## 目标

打通整条流式渲染管线并验证 Converge，覆盖**最小块集**。深度优先（ADR-0009 决策 / 问题 9）：先证明最难的架构端到端跑通，再谈功能广度与打磨。

**最小块集**：段落、标题、粗/斜/行内码、围栏代码块、无序/有序列表、引用块。

**不在 M1（后续里程碑）**：跨块选区与复制（ADR-0008）、真正的虚拟化（ADR-0006）、图片/数学/Mermaid、会话外壳（ADR-0004 的 shell）、扩展面开放（ADR-0010）、结构化无障碍。
注意：M1 的数据模型仍按 ADR 的「可见空间坐标 / 不可变 finalized 块 / 块级源码区间」建好，为上述留接缝。

---

## 阶段 A · 骨架与契约

- **A1 解决方案结构**：核心库项目 + 演示宿主 app + 测试项目；引用 Markdig、TextMateSharp。
  - 完成标准：三项目编译通过，演示 app 能起空窗口。
- **A2 公共 API 表面（仅签名）**：`MarkdownDocumentView` 控件，方法 `AppendDelta(string)` / `Complete()` / `Reset()` / `Abort()` / `SetMarkdown(string)`；事件 `LinkClicked`。先空实现。
  - 完成标准：API 编译可调用；任意线程调用 `AppendDelta` 不抛（线程契约占位）。
- **A3 Block 模型骨架**：`MdBlock` 抽象（块级源码区间 Start/End、`IsFinalized`），`Document` 持有有序 Block 列表，单一 Active Block 概念。
  - 完成标准：单测可构造 Document、追加/finalize Block、查询 Active Block。

## 阶段 B · 流式输入管线

- **B1 线程安全输入缓冲**：`AppendDelta` 任意线程入队，UI 线程消费。
  - 完成标准：多线程并发 append 无数据竞争（压测）。
- **B2 自适应节流 flush**：`DispatcherTimer` + 离散三档（慢/中/快：来即刷 / 33ms / 66–80ms）+ 空闲>150ms 立刷。合并缓冲 → 驱动解析 → 只重绘 Active Block。
  - 完成标准：档位选择逻辑可纯单测（给定速率→给定间隔）；空闲触发立刷。
- **B3 智能流式块级状态机**：累积文本切成 Block 边界，识别最小块集；维护单一 Active Block；块边界触发上一块 Finalize；未闭合内联标记乐观隐藏。
  - 完成标准：逐 token 喂入样例，块边界与 Active Block 推进正确。

## 阶段 C · 收敛与最终解析

- **C1 Finalize 钩子**：块 finalize 时用 Markdig 解析该块文本，产出权威块模型（ADR-0002）。
  - 完成标准：finalized 块的模型来自 Markdig。
- **C2 Converge 测试套件**：对样例集断言「流式预览的 finalized 块」==「Markdig 解析结果」。
  - 完成标准：套件绿；任一 divergence 即失败（这是 ADR-0002 的守护）。

## 阶段 D · 自绘渲染

- **D1 Inline Run 投影**：从流式状态机 / Markdig 内联树 → 扁平 `Inline Run`（可见空间 + 已解析样式：字重/字形/code/前景/链接目标）；未闭合标记乐观隐藏（ADR-0007）。
  - 完成标准：嵌套样例（`**a *b* c**`、`[**x**](url)`）折叠为正确的非重叠 run。
- **D2 自绘段落控件**：实现 `TextSource`，`TextFormatter` 排版 Inline Run，缓存换行；标题为同机制的更大字号/字重。
  - 完成标准：段落/标题正确换行渲染；重测量只发生在 Active Block。
- **D3 TextMate 代码块控件**：TextMateSharp 按行高亮、语言栏、复制按钮（整段原始代码）、横向滚动；流式追加可增量重高亮。
  - 完成标准：流式代码块边吐边高亮，未闭合围栏不崩。
- **D4 列表 / 引用块**：轻量自绘或布局，保留项目符号 / 引用条。
  - 完成标准：嵌套列表与引用渲染正确。
- **D5 块→可视分发**：内部 `IBlockRenderer` 注册表（ADR-0010，封闭），内置块全走此机制。
  - 完成标准：新增内置块类型只需注册一个渲染器。

## 阶段 E · 容器与滚动

- **E1 Scroll Host**：核心为非滚动内容；Scroll Host 提供单视口 + 黏底跟随 + 滚上脱离 + 「跳到最新」（ADR-0008 / 问题 10）。
  - 完成标准：流式时黏底；用户滚上则脱离并出现跳转按钮；回到底部重新黏住。
- **E2 主题骨架**：明/暗主题资源（DynamicResource），代码主题配对 TextMate 主题，M1 至少一套，结构留好（问题 11）。
  - 完成标准：自绘块从主题资源取色；切换主题刷新。

## 阶段 F · 演示与验证

- **F1 演示宿主**：模拟 token 流（可调速率），驱动 `AppendDelta`，展示流动渲染。
  - 完成标准：可调速回放一段 Markdown，观察连续流动。
- **F2 基础 AutomationPeer**：暴露 Document 可访问纯文本（复用纯文本序列化），兼供 UI 测试断言（ADR-0009）。
  - 完成标准：UI 测试能读取并断言渲染出的纯文本。
- **F3 端到端验证**：喂一段含全部最小块类型的 Markdown，逐 token 流式，观察平滑度 + finalize 收敛**无闪烁**。
  - 完成标准：finalize 时无可见重排（Converge 达成）；全程 UI 线程不卡顿。

---

## M1 验收（Definition of Done）

1. 逐 token 流式渲染最小块集，观感连续、UI 不抖。
2. finalize 时无闪烁——Converge 测试套件（C2）全绿。
3. 代码块流式高亮、复制按钮可用。
4. Scroll Host 黏底 / 脱离 / 跳转行为正确。
5. AutomationPeer 暴露纯文本，UI 测试可断言内容。
6. 演示 app 可调速回放，作为可视验证。
