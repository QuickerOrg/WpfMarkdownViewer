# Markdig as source of truth with a converging streaming parser

We run two parsers. During streaming, a hand-rolled, lenient block-level state machine produces a best-effort preview of the Active Block. On Finalize, Markdig re-parses the completed Block and its tree is authoritative.

Two parsers is surprising, so the reason matters: partial token input is often ambiguous (a line of `| a | b |` may be a paragraph or a table header until the next line arrives), and Markdig is built for complete documents, not incomplete streams. We still want a single canonical structure, so Markdig stays the source of truth and the streaming parser's job is to **Converge** — its preview of a finalized Block must equal Markdig's parse of the same text. Any divergence on a finalized Block is a defect, which turns "flicker on completion" from an accepted cost into a measurable, fixable bug.

## Consequences

- The streaming state machine must be smart enough to recognize structured Blocks (tables, fenced code, headings, lists) as they form, to minimize visible re-rendering on Finalize.
- There is an irreducible class of cases that cannot be resolved from partial input (e.g. an unclosed code fence); for these the preview is best-effort and corrected on Finalize.
- A divergence test suite (streaming preview vs. Markdig) is the natural guardrail for Converge.
