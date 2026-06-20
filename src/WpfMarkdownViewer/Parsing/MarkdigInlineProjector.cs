using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Parsing;

/// <summary>
/// Projects inline content into <see cref="InlineRun"/>s using Markdig's inline AST — the authoritative
/// projection used on finalize (ADR-0002/0007). The streaming <see cref="Streaming.InlineProjector"/>
/// must Converge to this for closed input.
/// </summary>
public static class MarkdigInlineProjector
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().UseMathematics().Build();

    public static InlineProjection Project(string source)
    {
        var doc = Markdown.Parse(source, Pipeline);
        var para = doc.Descendants<Markdig.Syntax.ParagraphBlock>().FirstOrDefault()
                   ?? doc.Descendants<LeafBlock>().FirstOrDefault(b => b.Inline is not null);

        var visible = new StringBuilder();
        var runs = new List<InlineRun>();
        if (para?.Inline is { } container)
            Walk(container, InlineStyle.None, link: null, visible, runs);
        return new InlineProjection(visible.ToString(), runs);
    }

    private static void Walk(ContainerInline container, InlineStyle style, string? link, StringBuilder visible, List<InlineRun> runs)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    Emit(literal.Content.ToString(), style, link, visible, runs);
                    break;
                case CodeInline code:
                    Emit(code.Content, style | InlineStyle.Code, link, visible, runs);
                    break;
                case Markdig.Extensions.Mathematics.MathInline math:
                    Emit(math.Content.ToString(), style | InlineStyle.Code, link, visible, runs); // inline math fallback
                    break;
                case EmphasisInline emphasis:
                    Walk(emphasis, style | EmphasisStyle(emphasis), link, visible, runs);
                    break;
                case LinkInline { IsImage: false } anchor:
                    Walk(anchor, style, anchor.Url ?? string.Empty, visible, runs);
                    break;
                case LineBreakInline:
                    Emit(" ", style, link, visible, runs);
                    break;
                case ContainerInline nested:
                    Walk(nested, style, link, visible, runs);
                    break;
            }
        }
    }

    private static InlineStyle EmphasisStyle(EmphasisInline e) => e.DelimiterChar switch
    {
        '~' => e.DelimiterCount >= 2 ? InlineStyle.Strikethrough : InlineStyle.None, // ~~strike~~ ; ~sub~ deferred
        '=' => InlineStyle.Highlight, // ==mark==
        '+' => InlineStyle.Underline, // ++ins++
        '^' => InlineStyle.None, // ^sup^ deferred
        _ => e.DelimiterCount >= 2 ? InlineStyle.Bold : InlineStyle.Italic, // * or _
    };

    private static void Emit(string text, InlineStyle style, string? link, StringBuilder visible, List<InlineRun> runs)
    {
        if (text.Length == 0)
            return;
        runs.Add(new InlineRun(visible.Length, text, style, link));
        visible.Append(text);
    }
}
