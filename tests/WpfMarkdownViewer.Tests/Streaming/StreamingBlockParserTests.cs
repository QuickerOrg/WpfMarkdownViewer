using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

public class StreamingBlockParserTests
{
    private static StreamingBlockParser Parse(string source, bool complete = false)
    {
        var p = new StreamingBlockParser();
        p.Reparse(source, complete);
        return p;
    }

    [Fact]
    public void EmptySource_ProducesNoBlocks()
    {
        var doc = Parse("").Document;

        Assert.Empty(doc.Blocks);
        Assert.Null(doc.ActiveBlock);
    }

    [Fact]
    public void LinkDefinitions_AreCollected_AndNotRenderedAsBlocks()
    {
        var doc = Parse("See [the site][ref] for more.\n\n[ref]: https://example.com \"title\"", complete: true).Document;

        Assert.Equal("https://example.com", doc.LinkDefinitions["ref"]);
        var p = Assert.Single(doc.Blocks); // the [ref]: … definition line is not a block
        Assert.Equal(BlockKind.Paragraph, p.Kind);
    }

    [Fact]
    public void GrowingParagraph_IsActiveNotFinalized()
    {
        var doc = Parse("hello wor").Document;

        var p = Assert.Single(doc.Blocks);
        Assert.Equal(BlockKind.Paragraph, p.Kind);
        Assert.False(p.IsFinalized);
        Assert.Same(p, doc.ActiveBlock);
    }

    [Fact]
    public void ParagraphFollowedByBlank_IsFinalized_NoActiveBlock()
    {
        var doc = Parse("hello\n\n").Document;

        var p = Assert.Single(doc.Blocks);
        Assert.True(p.IsFinalized);
        Assert.Null(doc.ActiveBlock);
    }

    [Fact]
    public void SoftWrappedLines_AreOneParagraph()
    {
        var doc = Parse("line one\nline two").Document;

        Assert.Single(doc.Blocks);
        Assert.Equal(BlockKind.Paragraph, doc.Blocks[0].Kind);
    }

    [Fact]
    public void Heading_WithNewline_IsRecognizedAndFinalized()
    {
        var doc = Parse("## Title\n").Document;

        var h = Assert.IsType<HeadingBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(2, h.Level);
        Assert.True(h.IsFinalized);
    }

    [Fact]
    public void Heading_WithoutNewline_IsActive()
    {
        var doc = Parse("### Stil").Document;

        var h = Assert.IsType<HeadingBlock>(Assert.Single(doc.Blocks));
        Assert.False(h.IsFinalized);
        Assert.Same(h, doc.ActiveBlock);
    }

    [Fact]
    public void HeadingThenParagraph_NoBlankBetween_AreTwoBlocks()
    {
        var doc = Parse("# H\npara").Document;

        Assert.Equal(2, doc.Blocks.Count);
        Assert.True(doc.Blocks[0].IsFinalized);
        Assert.Equal(BlockKind.Heading, doc.Blocks[0].Kind);
        Assert.Equal(BlockKind.Paragraph, doc.Blocks[1].Kind);
        Assert.Same(doc.Blocks[1], doc.ActiveBlock);
    }

    [Fact]
    public void OpenFence_IsActiveCode_WithLanguage_NotClosed()
    {
        var doc = Parse("```csharp\nvar x = 1;").Document;

        var code = Assert.IsType<CodeBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("csharp", code.Language);
        Assert.False(code.FenceClosed);
        Assert.False(code.IsFinalized);
        Assert.Same(code, doc.ActiveBlock);
    }

    [Fact]
    public void ClosedFence_IsFinalizedCode()
    {
        var doc = Parse("```\ncode\n```\n").Document;

        var code = Assert.IsType<CodeBlock>(Assert.Single(doc.Blocks));
        Assert.True(code.FenceClosed);
        Assert.True(code.IsFinalized);
    }

    [Fact]
    public void PipesInsideFence_DoNotBecomeAParagraph()
    {
        // An unclosed fence keeps everything inside it as one code block.
        var doc = Parse("```\n| a | b |\nmore").Document;

        Assert.Single(doc.Blocks);
        Assert.Equal(BlockKind.Code, doc.Blocks[0].Kind);
    }

    [Fact]
    public void UnorderedList_IsRecognized()
    {
        var doc = Parse("- one\n- two").Document;

        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.False(list.Ordered);
    }

    [Fact]
    public void OrderedList_IsRecognized()
    {
        var doc = Parse("1. one\n2. two").Document;

        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.True(list.Ordered);
    }

    [Fact]
    public void Blockquote_IsRecognized()
    {
        var doc = Parse("> quoted\n> more").Document;

        Assert.Equal(BlockKind.Quote, Assert.Single(doc.Blocks).Kind);
    }

    [Fact]
    public void Table_IsRecognizedWhenDelimiterRowPresent()
    {
        var doc = Parse("| a | b |\n| - | - |\n| 1 | 2 |").Document;

        Assert.Equal(BlockKind.Table, Assert.Single(doc.Blocks).Kind);
    }

    [Fact]
    public void PipeRow_WithoutDelimiter_IsNotATable()
    {
        var doc = Parse("| a | b |\njust text").Document;

        Assert.Equal(BlockKind.Paragraph, doc.Blocks[0].Kind);
    }

    [Fact]
    public void ThematicBreak_IsRecognized()
    {
        var doc = Parse("---", complete: true).Document;
        Assert.Equal(BlockKind.ThematicBreak, Assert.Single(doc.Blocks).Kind);
    }

    [Fact]
    public void ThematicBreak_BetweenParagraphs()
    {
        var doc = Parse("above\n\n***\n\nbelow", complete: true).Document;
        Assert.Equal(
            new[] { BlockKind.Paragraph, BlockKind.ThematicBreak, BlockKind.Paragraph },
            doc.Blocks.Select(b => b.Kind));
    }

    [Fact]
    public void BlockMath_MultiLine_IsRecognized()
    {
        var doc = Parse("$$\n\\int_0^1 x\\,dx\n$$", complete: true).Document;
        Assert.Equal(BlockKind.Math, Assert.Single(doc.Blocks).Kind);
    }

    [Fact]
    public void StreamComplete_FinalizesTheTrailingBlock()
    {
        var doc = Parse("still typing", complete: true).Document;

        Assert.True(Assert.Single(doc.Blocks).IsFinalized);
        Assert.Null(doc.ActiveBlock);
    }

    [Fact]
    public void RawText_OfFinalizedBlock_MatchesSourceSlice()
    {
        var doc = Parse("# Title\n\nbody").Document;

        // A Block's RawText is its own line(s); the blank separator line belongs to no Block.
        Assert.Equal("# Title\n", doc.Blocks[0].RawText);
        Assert.Equal(0, doc.Blocks[0].SourceStart);
        Assert.Equal(BlockKind.Paragraph, doc.Blocks[1].Kind);
        Assert.Equal("body", doc.Blocks[1].RawText);
    }
}
