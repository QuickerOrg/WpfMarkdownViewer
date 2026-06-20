# WpfMath for block math rendering

Block math (`$$ … $$`) is rendered with WpfMath, which parses LaTeX and renders to a WPF `Geometry`. We draw that geometry filled with the style foreground, so formulas are native vector (crisp at any DPI) and follow the theme. This resolves the spike deferred in M1 (WpfMath vs CSharpMath).

WpfMath's `RenderToGeometry` gives exactly what the self-drawn architecture (ADR-0005) wants — a geometry we fill ourselves — rather than a pre-rendered bitmap, so it stays sharp and theme-colored without a browser. The alternative, CSharpMath, renders via SkiaSharp bitmaps on WPF, which would not match the vector/themeable goal. Block math only for now: a `$$` fence on its own line (matching Markdig; a single-line `$$…$$` is inline math). Inline math falls back to inline-code styling — proper inline math layout (baseline-aligned within a text line) is later work.

## Notes

- `WpfTeXFormulaParser.Instance` parses; `WpfTeXEnvironment.Create(TexStyle.Display, scale, font, fg, bg)` + `formula.RenderToGeometry(env, scale, 0, 0)` yields the geometry. Its bounds can have a negative origin (ascenders), so the view translates by `-bounds.TopLeft` when drawing.
- Parse failures fall back to the raw LaTeX in monospace.
