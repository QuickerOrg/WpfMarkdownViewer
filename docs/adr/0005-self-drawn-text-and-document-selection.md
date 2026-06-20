# Self-drawn text with a document-level selection model

Paragraph Blocks draw their own inline text with `TextFormatter`/`DrawingContext` (cached line-breaks, no `Run` objects), rather than using `TextBlock` inlines or a `FlowDocument`/`RichTextBox`. Text selection is a hand-built subsystem that operates on the document model, not on WPF's text stack.

WPF only gives selection, copy, IME, and UIAutomation "for free" with `FlowDocument`/`RichTextBox`, which ADR-0001 rejected for streaming performance. Choosing self-drawn text for performance and control therefore means we own those concerns. We accept that, because selection in this component is inherently cross-Block (a drag spans paragraphs, code blocks, and lists) and must keep working even when a selected Block's visual has been virtualized away — so it has to live as a document-level model regardless. Selection over the document model is also what makes ADR-0006's virtualization safe.

## Consequences

- Selection and hit-testing are explicit workstreams, not free behaviors. The renderer is read-only, so caret/IME/text-input are out of scope, and accessibility is provided at a basic level via an AutomationPeer — see ADR-0009.
- The selection model is anchored to the Block/text-offset model, not to live visuals; it survives virtualization.
- Copy derives from the selection model (plain text by default; Markdown/HTML are later additions).
