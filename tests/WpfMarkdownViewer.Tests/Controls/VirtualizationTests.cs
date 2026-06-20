using System.Linq;
using System.Windows;
using WpfMarkdownViewer.Controls;

namespace WpfMarkdownViewer.Tests.Controls;

public class VirtualizationTests
{
    private static MarkdownDocumentView LongDocument(int paragraphs)
    {
        var view = new MarkdownDocumentView();
        view.SetMarkdown(string.Join("\n\n", Enumerable.Range(0, paragraphs).Select(i => $"paragraph number {i}")));
        Layout(view, 200);
        return view;
    }

    private static void Layout(MarkdownDocumentView view, double height)
    {
        view.Measure(new Size(400, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, 400, height));
        view.UpdateLayout();
    }

    [WpfFact]
    public void NoViewport_RealizesAllBlocks()
    {
        var view = LongDocument(40);

        Assert.Equal(40, view.SlotCountForTest);
        Assert.Equal(40, view.RealizedCountForTest); // no Scroll Host ⇒ realize all
    }

    [WpfFact]
    public void WithSmallViewport_OffscreenFinalizedBlocks_AreVirtualized()
    {
        var view = LongDocument(60);
        Assert.Equal(60, view.RealizedCountForTest); // heights measured first

        ((IVirtualizingContent)view).SetViewport(0, 200);
        Layout(view, 200);

        int realized = view.RealizedCountForTest;
        Assert.True(realized < 60, $"expected virtualization, but {realized}/60 realized");
        Assert.True(realized >= 1);
    }

    [WpfFact]
    public void VirtualizationDisabled_KeepsAllRealized_EvenWithViewport()
    {
        var view = LongDocument(60);
        view.VirtualizationEnabled = false;

        ((IVirtualizingContent)view).SetViewport(0, 200);
        Layout(view, 200);

        Assert.Equal(60, view.RealizedCountForTest);
    }

    [WpfFact]
    public void ScrollingDown_RealizesLaterBlocks()
    {
        var view = LongDocument(60);
        ((IVirtualizingContent)view).SetViewport(0, 200);
        Layout(view, 200);

        // Scroll far down; blocks near the new viewport must realize.
        ((IVirtualizingContent)view).SetViewport(1200, 200);
        Layout(view, 200);

        Assert.True(view.RealizedCountForTest >= 1);
        Assert.True(view.RealizedCountForTest < 60);
    }
}
