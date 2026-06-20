# Appearance configured via a strongly-typed MarkdownStyle record + DependencyProperty

Host-configurable appearance (fonts, sizes, weights, line heights, margins/padding, list indent, colors, paired TextMate theme) is exposed as a single immutable `MarkdownStyle` record, set through a `MarkdownStyle` dependency property on the control. Coordinated `Light`/`Dark` presets ship in the box; hosts derive variants with `with { … }`.

We chose this over the two alternatives a WPF developer would expect: a bag of `DynamicResource` keys (as LiveMarkdown.Avalonia does), and per-element WPF `Style` selectors. A strongly-typed object is discoverable and compile-checked, settable from code or XAML, and runtime-swappable (changing the property rebuilds the Block visuals). Crucially it does **not** require making the self-drawn per-Block visual types public and styleable, so it keeps the block-renderer surface closed (ADR-0010). The trade-offs we accept: it is less granular than CSS/Style selectors, and adding a knob means adding a property rather than a free-form resource key. A resource-key layer can be added on top later without breaking the typed API.

## Notes

- The property is named `MarkdownStyle`, not `Style`, because `FrameworkElement.Style` already exists.
- `MarkdownStyle` is a `record`, so `with` gives cheap, safe derivation; brushes are frozen for cross-thread/perf safety.
