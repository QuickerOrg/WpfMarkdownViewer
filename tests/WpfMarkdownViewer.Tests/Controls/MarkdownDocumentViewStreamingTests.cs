using WpfMarkdownViewer.Controls;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Tests.Controls;

public class MarkdownDocumentViewStreamingTests
{
    [WpfFact]
    public void AppendDelta_ThenFlush_PopulatesDocument()
    {
        var view = new MarkdownDocumentView();

        view.AppendDelta("# Title\n\n");
        view.AppendDelta("a paragraph");
        view.FlushForTest();

        Assert.Equal(2, view.Document.Blocks.Count);
        Assert.Equal(BlockKind.Heading, view.Document.Blocks[0].Kind);
        Assert.True(view.Document.Blocks[0].IsFinalized);
        Assert.Equal(BlockKind.Paragraph, view.Document.Blocks[1].Kind);
        Assert.Same(view.Document.Blocks[1], view.Document.ActiveBlock);
    }

    [WpfFact]
    public void Complete_FinalizesTheActiveBlock()
    {
        var view = new MarkdownDocumentView();
        view.AppendDelta("trailing text");

        view.Complete();

        Assert.Null(view.Document.ActiveBlock);
        Assert.True(view.Document.Blocks[0].IsFinalized);
    }

    [WpfFact]
    public void DocumentChanged_RaisedOnFlush()
    {
        var view = new MarkdownDocumentView();
        int raised = 0;
        view.DocumentChanged += (_, _) => raised++;

        view.AppendDelta("hello");
        view.FlushForTest();

        Assert.True(raised >= 1);
    }
}
