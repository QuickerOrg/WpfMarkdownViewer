using System.Collections.Concurrent;
using System.Windows;
using TextMateSharp.Grammars;
using WpfMarkdownViewer.Highlighting;
using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// Builds the self-drawn visual for a Block. This is the seed of the internal block-renderer registry
/// (ADR-0010, closed for now): all built-in Blocks are produced here through one mechanism.
/// </summary>
internal static class BlockViewFactory
{
    private static readonly ConcurrentDictionary<ThemeName, CodeHighlighter> Highlighters = new();

    private static CodeHighlighter HighlighterFor(ThemeName theme) =>
        Highlighters.GetOrAdd(theme, t => new CodeHighlighter(t));

    public static FrameworkElement Create(MdBlock block, MarkdownStyle theme, Action<string>? onLink = null) => block switch
    {
        HeadingBlock h => new ParagraphView(
            InlineProjector.Project(InlineSource.Extract(h)), theme,
            emSize: theme.HeadingEm(h.Level), weight: theme.HeadingWeight,
            lineHeightFactor: theme.HeadingLineHeight, onLink: onLink),

        CodeBlock c => CreateCodeView(c, theme),

        TableBlock t => new TableView(t, theme, onLink),

        ThematicBreakBlock => new HrView(theme),

        ImageBlock img => new ImageView(img, theme),

        MathBlock m => new MathView(m, theme),

        ListBlock l => new ListView(l, theme, onLink),

        QuoteBlock q => new QuoteView(q, theme, onLink),

        _ => new ParagraphView(
            InlineProjector.Project(InlineSource.Extract(block)), theme,
            emSize: theme.EmSize, weight: FontWeights.Normal,
            lineHeightFactor: theme.ParagraphLineHeight, onLink: onLink),
    };

    private static FrameworkElement CreateCodeView(CodeBlock c, MarkdownStyle theme)
    {
        string code = CodeText.Extract(c);
        var lines = HighlighterFor(theme.TextMateTheme).Highlight(code, c.Language);
        return new CodeBlockView(code, c.Language, lines, theme);
    }
}
