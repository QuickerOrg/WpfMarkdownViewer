# Paragraph inline model: Visible-Space offsets and flat runs

A paragraph's inline content is modeled as a flat list of non-overlapping Inline Runs in Visible Space (rendered text, markup removed), each carrying fully-resolved style (weight, slant, code, foreground, optional link target). We do not index by Source Space (raw Markdown offsets) and we do not keep Markdig's nested inline tree at runtime.

Self-drawn text via TextFormatter lays out only visible characters, so markup like `**` must not exist in the layout text — Visible Space is therefore mandatory for layout, and using it for the Selection Model and hit-testing too keeps one coordinate space for everything users touch. Flat resolved runs are exactly what TextFormatter's TextSource and hit-testing want; nesting (`**a *b* c**`, `[**x**](url)`) is folded into per-segment effective style at projection time. The Block keeps only a block-level Source-Space range; when a feature needs nesting or markup (e.g. copy-as-Markdown), we re-derive from that source range rather than carrying a tree. The Active Block's run list is rebuilt wholesale each tick (single bounded block, avoids incremental-sync bugs); finalize re-projects from Markdig as the authority.

## Consequences

- Selection anchors are `(BlockId, VisibleOffset)`; they are stable against markup and survive virtualization.
- Unclosed inline markup is previewed optimistically (markup hidden, style applied), then reconciled on finalize per Converge — a rare unclosed marker becomes literal text and the preview rolls back.
- Copy-as-Markdown / source-linked features must reconstruct from the Block's Source-Space range, since no fine visible↔source map is kept by default.
