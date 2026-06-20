# Self-built block renderer over WebView2 and FlowDocument

We render Markdown with a self-built, native WPF block renderer — each Block is its own lightweight visual and only the Active Block re-renders per tick — rather than embedding WebView2 or building on Markdig→FlowDocument.

WebView2 most easily matches ChatGPT's look, but it costs a large bundle (fixed runtime is +250MB), brings airspace/memory issues, and never feels native inside a WPF product. Markdig→FlowDocument is fast to build but rebuilds the whole visual tree on each update, so it cannot stream long replies smoothly. Because this component is long-term shared infrastructure for all of Quicker's AI features, we accept the larger engineering cost of a native renderer to get streaming performance, native feel, a small footprint, and deep control over selection/copy/theming.

## Considered Options

- **WebView2 + HTML/CSS** — best visual fidelity, mature ecosystem; rejected for bundle size, airspace, memory, non-native feel. Still useful for quick visual prototyping.
- **Markdig → FlowDocument** (e.g. Markdig.Wpf, MdXaml) — quickest to build in pure WPF; rejected because full-tree rebuild can't stream long content without jank and offers weak control over code blocks, selection, and copy.
- **Self-built block renderer** — chosen.
