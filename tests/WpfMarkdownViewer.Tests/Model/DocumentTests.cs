using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Tests.Model;

public class DocumentTests
{
    [Fact]
    public void NewDocument_HasNoBlocksAndNoActiveBlock()
    {
        var doc = new Document();

        Assert.Empty(doc.Blocks);
        Assert.Null(doc.ActiveBlock);
    }

    [Fact]
    public void AppendBlock_MakesItTheActiveBlock()
    {
        var doc = new Document();
        var p = new ParagraphBlock { RawText = "hello" };

        doc.AppendBlock(p);

        Assert.Same(p, doc.ActiveBlock);
        Assert.Single(doc.Blocks);
        Assert.False(p.IsFinalized);
    }

    [Fact]
    public void AppendingSecondBlock_FinalizesThePrevious_EnforcingSingleActiveBlock()
    {
        var doc = new Document();
        var first = new ParagraphBlock { RawText = "first" };
        var second = new HeadingBlock { RawText = "## h", Level = 2 };

        doc.AppendBlock(first);
        doc.AppendBlock(second);

        Assert.True(first.IsFinalized);
        Assert.Same(second, doc.ActiveBlock);
        Assert.Equal(2, doc.Blocks.Count);
    }

    [Fact]
    public void FinalizeActive_ClearsTheActiveBlock_AndIsIdempotent()
    {
        var doc = new Document();
        var p = new ParagraphBlock { RawText = "x" };
        doc.AppendBlock(p);

        doc.FinalizeActive();
        doc.FinalizeActive(); // idempotent

        Assert.True(p.IsFinalized);
        Assert.Null(doc.ActiveBlock);
    }

    [Fact]
    public void SourceEnd_IsStartPlusRawLength()
    {
        var p = new ParagraphBlock { SourceStart = 10, RawText = "abcde" };

        Assert.Equal(15, p.SourceEnd);
    }

    [Fact]
    public void Clear_EmptiesTheDocument()
    {
        var doc = new Document();
        doc.AppendBlock(new ParagraphBlock { RawText = "a" });

        doc.Clear();

        Assert.Empty(doc.Blocks);
        Assert.Null(doc.ActiveBlock);
    }
}
