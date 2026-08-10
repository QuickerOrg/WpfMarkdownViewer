using System.Windows;
using System.Windows.Media;
using WpfMarkdownViewer.Controls;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Tests.Controls;

public class MarkdownDocumentViewStreamingTests
{
    [WpfFact]
    public void PlainTextStreaming_KeepsTheActiveParagraphVisual()
    {
        var view = new MarkdownDocumentView();
        view.AppendDelta("hello");
        view.FlushForTest();
        Layout(view);
        var first = VisualTreeHelper.GetChild(view, 0);

        view.AppendDelta(" world");
        view.FlushForTest();
        Layout(view);
        var second = VisualTreeHelper.GetChild(view, 0);

        Assert.Same(first, second);
    }

    [WpfFact]
    public void ListStreaming_KeepsTheListAndExistingItemVisuals()
    {
        var view = new MarkdownDocumentView();
        view.AppendDelta("- first");
        view.FlushForTest();
        Layout(view);
        var firstList = VisualTreeHelper.GetChild(view, 0);
        var firstItem = VisualTreeHelper.GetChild(firstList, 0);

        view.AppendDelta(" item");
        view.FlushForTest();
        Layout(view);
        var secondList = VisualTreeHelper.GetChild(view, 0);
        var secondItem = VisualTreeHelper.GetChild(secondList, 0);

        Assert.Same(firstList, secondList);
        Assert.Same(firstItem, secondItem);
    }

    private static void Layout(MarkdownDocumentView view)
    {
        view.Measure(new Size(600, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, 600, view.DesiredSize.Height));
        view.UpdateLayout();
    }

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
