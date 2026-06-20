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

    public static FrameworkElement Create(MdBlock block, TextRenderTheme theme) => block switch
    {
        HeadingBlock h => new ParagraphView(
            InlineProjector.Project(InlineSource.Extract(h)), theme,
            emSize: HeadingEm(h.Level, theme.EmSize), weight: FontWeights.Bold),

        CodeBlock c => CreateCodeView(c, theme),

        ListBlock l => new ListView(l, theme),

        QuoteBlock q => new QuoteView(q, theme),

        _ => new ParagraphView(
            InlineProjector.Project(InlineSource.Extract(block)), theme,
            emSize: theme.EmSize, weight: FontWeights.Normal),
    };

    private static FrameworkElement CreateCodeView(CodeBlock c, TextRenderTheme theme)
    {
        string code = CodeText.Extract(c);
        var lines = HighlighterFor(theme.TextMateTheme).Highlight(code, c.Language);
        return new CodeBlockView(code, c.Language, lines, theme);
    }

    private static double HeadingEm(int level, double baseEm) => level switch
    {
        1 => baseEm * 1.8,
        2 => baseEm * 1.5,
        3 => baseEm * 1.3,
        4 => baseEm * 1.15,
        5 => baseEm * 1.05,
        _ => baseEm,
    };
}
