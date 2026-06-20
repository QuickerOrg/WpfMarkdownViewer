using System.Windows;
using WpfMarkdownViewer.Controls;

namespace WpfMarkdownViewer.Tests.Controls;

public class SelectionTests
{
    private static MarkdownDocumentView LaidOut(string markdown)
    {
        var view = new MarkdownDocumentView();
        view.SetMarkdown(markdown);
        view.Measure(new Size(400, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, 400, 4000));
        view.UpdateLayout();
        return view;
    }

    [WpfFact]
    public void Selectables_AreParagraphTextLeaves_InDocumentOrder()
    {
        var view = LaidOut("alpha\n\nbeta\n\ngamma");

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, view.SelectableTextsForTest());
    }

    [WpfFact]
    public void Selection_AcrossBlocks_CopiesPartialEndsAndWholeMiddle()
    {
        var view = LaidOut("alpha\n\nbeta\n\ngamma");

        string text = view.SelectAndGetTextForTest(segA: 0, offA: 2, segB: 2, offB: 3);

        Assert.Equal("pha\nbeta\ngam", text);
    }

    [WpfFact]
    public void Selection_WithinOneBlock_CopiesSubrange()
    {
        var view = LaidOut("hello world");

        string text = view.SelectAndGetTextForTest(0, 0, 0, 5);

        Assert.Equal("hello", text);
    }

    [WpfFact]
    public void Selection_ReversedDrag_IsNormalized()
    {
        var view = LaidOut("hello world");

        // focus before anchor — order must normalize
        string text = view.SelectAndGetTextForTest(segA: 0, offA: 11, segB: 0, offB: 6);

        Assert.Equal("world", text);
    }

    [WpfFact]
    public void ListItemsAndCells_AreSelectable_NotJustTopLevelBlocks()
    {
        var view = LaidOut("- one\n- two\n\npara");

        // list items are nested ParagraphViews, collected as their own segments
        Assert.Equal(new[] { "one", "two", "para" }, view.SelectableTextsForTest());
    }

    [WpfFact]
    public void Copy_PreservesFormatting_AsMarkdownAndHtml()
    {
        // visible text: "hello bold world"; "bold" is at offsets 6..10
        var view = LaidOut("hello **bold** world");

        Assert.Equal("**bold**", view.SelectAndGetMarkdownForTest(0, 6, 0, 10));
        Assert.Equal("<b>bold</b>", view.SelectAndGetHtmlForTest(0, 6, 0, 10));
    }
}
