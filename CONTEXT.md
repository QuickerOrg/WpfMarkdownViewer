# WPF Markdown Viewer

A native WPF component for rendering AI-generated Markdown with ChatGPT-quality visuals and smooth incremental streaming. Built as long-term shared infrastructure for Quicker's AI features (multiple model providers, plugin output, action docs), not as a one-off viewer.

## Language

**Document**:
One self-contained Markdown stream rendered by the core renderer — an assistant message, an action doc, or a plugin's output. A Document is a sequence of Blocks with at most one Active Block. The Conversation Shell composes many Documents but the core renders exactly one.
_Avoid_: message (too chat-specific), page, content

**Conversation Shell**:
The optional thin wrapper that composes many Documents into a transcript and owns message-level virtualization and autoscroll. It is not the core; non-chat consumers (plugin output, action docs) use the core Document renderer directly.
_Avoid_: chat control, transcript view

**Scroll Host**:
The control that owns the single scroll viewport and autoscroll for one or many Documents. The core Document renderer is non-scrolling content; the Scroll Host provides scrolling, sticky-bottom following, and the "jump to latest" affordance. Code blocks keep their own internal horizontal scroll, which is unrelated.
_Avoid_: scroll viewer, scroll container

**Block**:
The atomic unit of parsing, rendering, and incremental update — a paragraph, heading, code block, list, table, quote, or image. Each Block maps to one lightweight WPF visual.
_Avoid_: element, node, segment, 块

**Active Block**:
The single trailing Block currently receiving tokens. It is the only Block that re-renders on each update tick; all Blocks before it are immutable.
_Avoid_: last block, pending block, live block, 正在变化的块, 最后未完成块

**Finalize**:
The transition where a Block becomes immutable — triggered by a blank line, the start of a new Block, or message-completion. After a Block is finalized, Markdig's parse of it is authoritative.
_Avoid_: complete, stabilize, commit, seal, 完成, 稳定

**Converge**:
The requirement that the streaming preview of a Block equals Markdig's parse of the same text once the Block is finalized. A finalized Block whose preview differs from Markdig's result is a defect.
_Avoid_: match, reconcile, sync

**Selection Model**:
The document-level representation of the current text selection, anchored to Block and Visible-Space offset coordinates rather than to live visuals. It spans multiple Blocks and survives a Block being virtualized away. Copy derives from it.
_Avoid_: highlight, TextRange

**Source Space**:
The character-offset space of the raw Markdown buffer, including all markup characters (`**`, backticks, link syntax). Used by the streaming parser, Converge, and a Block's block-level source range.
_Avoid_: raw offset, markdown offset

**Visible Space**:
The character-offset space of the rendered, visible text — markup characters removed. Used by text layout, the Selection Model, hit-testing, and plain-text copy. The inline model and selection anchors live here.
_Avoid_: rendered offset, display offset

**Inline Run**:
The atomic unit of a paragraph's inline model: a contiguous Visible-Space range carrying fully-resolved style (weight, slant, code, foreground, optional link target). Non-overlapping runs form a flat list that drives both TextFormatter layout and hit-testing; nesting is folded away at projection time. Rebuilt wholesale each tick for the Active Block.
_Avoid_: span, segment, TextRun
