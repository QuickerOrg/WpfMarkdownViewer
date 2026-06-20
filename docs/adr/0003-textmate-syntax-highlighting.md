# TextMate grammars for code-block syntax highlighting

Code blocks are highlighted with TextMate grammars via TextMateSharp, rather than hand-written lexers or AvalonEdit's XSHD highlighter.

The code block is the most experience-defining element, and ChatGPT-grade quality means broad language coverage with good fidelity — which the VS Code TextMate grammar ecosystem provides out of the box. TextMate's per-line, stateful (begin/end stack) model also fits streaming: it highlights line-by-line and tolerates incomplete code. The trade-offs we accept: an extra dependency, grammar/theme assets that grow the bundle, and regex-based highlighting that needs performance care on very large blocks (mitigated by per-line highlighting plus code-block virtualization).

## Considered Options

- **TextMateSharp (TextMate grammars)** — chosen; best coverage/fidelity, streams per-line.
- **Custom lightweight lexers** — zero deps and fast, but every grammar is ours to maintain and the long tail of languages renders plain.
- **AvalonEdit highlighter** — mature and per-line, but embeds an editor to use only its highlighter and covers fewer languages (XSHD).
