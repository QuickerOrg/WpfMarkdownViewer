using System.Windows;
using System.Windows.Media;
using WpfMarkdownViewer.Controls;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Controls;

/// <summary>
/// Runtime style switching: existing Block visuals must pick up the new style (they bake it in at
/// construction), while the parsed Document and any stream in progress are kept.
/// </summary>
public class MarkdownStyleSwitchTests
{
    private const string Sample = """
        # Title

        Plain text with **bold**, `inline code` and a [link](https://example.com).

        > quoted words

        - list item

        | a | b |
        |---|---|
        | 1 | 2 |

        ```csharp
        var x = 1;
        ```
        """;

    [WpfFact]
    public void SwitchingStyle_RestylesEveryFinalizedBlock_WithoutReparsing()
    {
        var view = new MarkdownDocumentView { VirtualizationEnabled = false };
        view.SetMarkdown(Sample);
        Layout(view);
        var blocksBefore = view.Document.Blocks.ToList();
        Assert.Contains(Color(MarkdownStyle.Light.Foreground), BrushColors(view));

        view.MarkdownStyle = MarkdownStyle.Dark;
        Layout(view);

        // Parse results are reused as-is (no re-parse on a style switch).
        var blocksAfter = view.Document.Blocks.ToList();
        Assert.Equal(blocksBefore.Count, blocksAfter.Count);
        Assert.All(blocksBefore.Zip(blocksAfter), pair => Assert.Same(pair.First, pair.Second));

        var colors = BrushColors(view);
        var dark = MarkdownStyle.Dark;
        var light = MarkdownStyle.Light;
        Assert.Contains(Color(dark.Foreground), colors);          // paragraph / heading / list / quote / table text
        Assert.Contains(Color(dark.LinkBrush), colors);           // link
        Assert.Contains(Color(dark.CodeForeground), colors);      // inline code text
        Assert.Contains(Color(dark.InlineCodeBackground), colors);// inline code background
        Assert.Contains(Color(dark.CodeBlockBackground), colors); // code block + table header
        Assert.Contains(Color(dark.QuoteBar), colors);            // quote bar
        Assert.Contains(Color(dark.Border), colors);              // code block / table borders

        Assert.DoesNotContain(Color(light.Foreground), colors);
        Assert.DoesNotContain(Color(light.LinkBrush), colors);
        Assert.DoesNotContain(Color(light.CodeForeground), colors);
        Assert.DoesNotContain(Color(light.InlineCodeBackground), colors);
        Assert.DoesNotContain(Color(light.CodeBlockBackground), colors);
        Assert.DoesNotContain(Color(light.QuoteBar), colors);
        Assert.DoesNotContain(Color(light.Border), colors);
        Assert.Same(dark.Background, view.Background);
    }

    [WpfFact]
    public void SwitchingStyle_AppliesNewFontSizes_ToExistingBlocks()
    {
        var view = new MarkdownDocumentView { VirtualizationEnabled = false };
        view.SetMarkdown("# Title\n\nbody text");
        Layout(view);
        double heightBefore = view.DesiredSize.Height;

        var big = MarkdownStyle.Light with { EmSize = 24 };
        view.MarkdownStyle = big;
        Layout(view);

        var sizes = GlyphEmSizes(view);
        Assert.Contains(24d, sizes);                   // paragraph
        Assert.Contains(big.HeadingEm(1), sizes);      // heading
        Assert.DoesNotContain(MarkdownStyle.Light.EmSize, sizes);
        Assert.True(view.DesiredSize.Height > heightBefore, "Bigger fonts must re-measure the existing blocks.");
    }

    [WpfFact]
    public void SwitchingStyle_MidStream_KeepsStreamingAndCaret_ThenContinuesAppending()
    {
        var view = new MarkdownDocumentView { VirtualizationEnabled = false };
        view.AppendDelta("first paragraph\n\nstreaming **tail");
        view.FlushForTest();
        Layout(view);
        var activeBefore = ActiveParagraph(view);
        Assert.True(activeBefore.ShowCaret);

        // A delta queued but not yet flushed when the style changes must not be lost.
        view.AppendDelta(" words");
        view.MarkdownStyle = MarkdownStyle.Dark;
        Layout(view);

        var activeAfter = ActiveParagraph(view);
        Assert.NotSame(activeBefore, activeAfter);
        Assert.True(activeAfter.ShowCaret, "The streaming caret must move to the restyled active block.");
        Assert.False(activeBefore.ShowCaret);
        Assert.False(view.Document.Blocks[^1].IsFinalized, "A style switch must not finalize the Active Block.");

        view.FlushForTest();
        view.AppendDelta("** and more");
        view.FlushForTest();
        Layout(view);

        Assert.Same(activeAfter, ActiveParagraph(view)); // still updated in place after the switch
        Assert.Equal("streaming tail words and more", activeAfter.SelectableText);
        Assert.Equal("first paragraph", ((ParagraphView)VisualTreeHelper.GetChild(view, 0)).SelectableText);

        view.Complete();
        Layout(view);
        var colors = BrushColors(view);
        Assert.Contains(Color(MarkdownStyle.Dark.Foreground), colors);
        Assert.DoesNotContain(Color(MarkdownStyle.Light.Foreground), colors);
    }

    [WpfFact]
    public void ConversationView_ApplyTheme_RestylesFinalizedAndStreamingMessagesInPlace()
    {
        var view = new ConversationView { VirtualizationEnabled = false };
        view.AddMessage(ChatRole.User, "question");
        view.AddMessage(ChatRole.Assistant, "answer with `code`");
        view.StartMessage(ChatRole.Assistant);
        view.AppendDelta("streaming ");
        view.FlushActiveForTest();
        Layout(view);
        var finalizedViewBefore = DocumentViews(view)[1];

        view.ApplyTheme(MarkdownStyle.Dark);
        Layout(view);

        var views = DocumentViews(view);
        Assert.Same(finalizedViewBefore, views[1]); // restyled in place: not rebuilt / re-parsed
        Assert.Equal(3, views.Count);

        view.AppendDelta("continues");
        view.FlushActiveForTest();
        view.CompleteMessage();
        Layout(view);

        Assert.Contains("streaming continues", view.MessageTextForTest(2));
        var colors = BrushColors(view);
        Assert.Contains(Color(MarkdownStyle.Dark.Foreground), colors);
        Assert.Contains(Color(MarkdownStyle.Dark.SubtleForeground), ButtonForegrounds(view));
        Assert.DoesNotContain(Color(MarkdownStyle.Light.Foreground), colors);
        Assert.DoesNotContain(Color(MarkdownStyle.Light.SubtleForeground), ButtonForegrounds(view));
    }

    // --- helpers ---

    private static ParagraphView ActiveParagraph(MarkdownDocumentView view) =>
        (ParagraphView)VisualTreeHelper.GetChild(view, VisualTreeHelper.GetChildrenCount(view) - 1);

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(600, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, 600, view.DesiredSize.Height));
        view.UpdateLayout();
    }

    private static Color Color(Brush brush) => ((SolidColorBrush)brush).Color;

    private static List<MarkdownDocumentView> DocumentViews(DependencyObject root) =>
        Descendants(root).OfType<MarkdownDocumentView>().ToList();

    private static List<Color> ButtonForegrounds(DependencyObject root) =>
        Descendants(root).OfType<System.Windows.Controls.Button>()
            .Select(b => b.Foreground).OfType<SolidColorBrush>().Select(b => b.Color).ToList();

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    /// <summary>Every solid color the self-drawn block visuals painted (fills, pens, glyph foregrounds).</summary>
    private static HashSet<Color> BrushColors(DependencyObject root)
    {
        var colors = new HashSet<Color>();
        foreach (var drawing in Drawings(root))
        {
            switch (drawing)
            {
                case GeometryDrawing g:
                    Add(g.Brush);
                    Add(g.Pen?.Brush);
                    break;
                case GlyphRunDrawing glyphs:
                    Add(glyphs.ForegroundBrush);
                    break;
            }
        }
        return colors;

        void Add(Brush? brush)
        {
            if (brush is SolidColorBrush solid)
                colors.Add(solid.Color);
        }
    }

    private static HashSet<double> GlyphEmSizes(DependencyObject root) =>
        Drawings(root).OfType<GlyphRunDrawing>().Select(g => Math.Round(g.GlyphRun.FontRenderingEmSize, 2)).ToHashSet();

    private static IEnumerable<Drawing> Drawings(DependencyObject root)
    {
        foreach (var node in Descendants(root).Prepend(root).OfType<Visual>())
        {
            if (VisualTreeHelper.GetDrawing(node) is { } group)
            {
                foreach (var drawing in Flatten(group))
                    yield return drawing;
            }
        }
    }

    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children)
            {
                foreach (var nested in Flatten(child))
                    yield return nested;
            }
        }
        else
        {
            yield return drawing;
        }
    }
}
