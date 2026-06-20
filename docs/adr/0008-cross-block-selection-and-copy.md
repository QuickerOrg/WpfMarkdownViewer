# Cross-block selection and copy strategy

Selection is a single continuous range whose two ends are `(BlockId, VisibleOffset)` anchors. Only the active end (the one tracking the mouse) needs point→offset hit-testing, and it is always in the viewport and realized; the other end is frozen as a logical anchor at drag start, independent of whether its Block still has a visual; Blocks that fall between the two ends in document order are taken wholesale. Copy puts three formats on the clipboard at once: plain text (from Visible Space), HTML, and Markdown — both HTML and Markdown are serialized from the Inline Run model, not from source slices.

## Why this is the way it is

Hit-testing only the moving end means mid Blocks never need geometry, so virtualized Blocks (ADR-0006) participate in selection at zero cost — a reader who expects every selected Block to be measured should not "fix" this. Serializing Markdown from the runs (rather than slicing the Source-Space text) keeps copy free of the fine visible↔source map that ADR-0007 deliberately omits, and makes partial selections trivially well-formed (re-wrap, never emit a dangling `**`). The deliberate cost: **copied Markdown is normalized** — it may differ from the author's original spelling (`_italic_` becomes `*italic*`, extra spaces collapse). This is intended, not a defect; for copying AI-generated Markdown it is acceptable.

## Plain-text fidelity

Plain text uses a "readable text" style: keep structural prefixes (list bullets, code-block body verbatim, tables as tab-separated rows for Excel), drop inline markup (bold/italic/code markers, heading `#`, quote `>`, link URLs). The code block's own Copy button copies the raw code only, independent of the selection.
