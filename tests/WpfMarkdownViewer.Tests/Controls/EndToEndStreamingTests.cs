using System.Windows.Automation.Peers;
using WpfMarkdownViewer.Controls;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Tests.Controls;

/// <summary>
/// The Milestone 1 soul test (phase F): stream a sample covering the whole block set token-by-token,
/// finalize, and assert the Document converged to the right structure and the accessible text is correct.
/// </summary>
public class EndToEndStreamingTests
{
    private const string Sample =
        "# Title\n\nintro **bold**\n\n- a\n- b\n\n```csharp\nvar x = 1;\n```\n\n> note\n\n| h1 | h2 |\n| - | - |\n| 1 | 2 |\n";

    private static MarkdownDocumentView StreamSample()
    {
        var view = new MarkdownDocumentView();
        for (int i = 0; i < Sample.Length; i += 3)
        {
            view.AppendDelta(Sample.Substring(i, Math.Min(3, Sample.Length - i)));
            view.FlushForTest(); // advance the pipeline deterministically
        }
        view.Complete();
        return view;
    }

    [WpfFact]
    public void StreamingFullSample_ConvergesToExpectedBlockStructure()
    {
        var view = StreamSample();

        Assert.Equal(
            new[] { BlockKind.Heading, BlockKind.Paragraph, BlockKind.List, BlockKind.Code, BlockKind.Quote, BlockKind.Table },
            view.Document.Blocks.Select(b => b.Kind));
        Assert.Null(view.Document.ActiveBlock); // everything finalized after Complete
        Assert.All(view.Document.Blocks, b => Assert.True(b.IsFinalized));
    }

    [WpfFact]
    public void AccessibleText_ContainsRenderedContent_AcrossAllBlocks()
    {
        var text = StreamSample().GetAccessibleText();

        Assert.Contains("Title", text);
        Assert.Contains("intro bold", text); // inline markup stripped
        Assert.Contains("• a", text);
        Assert.Contains("var x = 1;", text); // code verbatim
        Assert.Contains("note", text);
        Assert.Contains("h1\th2", text); // table tab-separated
    }

    [WpfFact]
    public void AutomationPeer_ExposesTheAccessibleText()
    {
        var view = new MarkdownDocumentView();
        view.SetMarkdown("# Hello\n\nworld");

        var peer = UIElementAutomationPeer.CreatePeerForElement(view);

        Assert.NotNull(peer);
        string name = peer!.GetName();
        Assert.Contains("Hello", name);
        Assert.Contains("world", name);
    }
}
