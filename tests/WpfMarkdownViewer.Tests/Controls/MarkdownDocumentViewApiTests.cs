using WpfMarkdownViewer.Controls;

namespace WpfMarkdownViewer.Tests.Controls;

public class MarkdownDocumentViewApiTests
{
    [WpfFact]
    public void AppendDelta_FromAnyThread_DoesNotThrow()
    {
        // Control is constructed on the STA "UI" thread provided by [WpfFact].
        var view = new MarkdownDocumentView();

        // The streaming sink must accept tokens from a background thread (ADR: any-thread sink).
        var ex = Record.Exception(() =>
            Task.Run(() => view.AppendDelta("hello")).GetAwaiter().GetResult());

        Assert.Null(ex);
    }

    [WpfFact]
    public void PublicLifecycleMethods_DoNotThrow()
    {
        var view = new MarkdownDocumentView();

        var ex = Record.Exception(() =>
        {
            view.AppendDelta("# Title");
            view.Complete();
            view.Reset();
            view.Abort();
            view.SetMarkdown("**done**");
        });

        Assert.Null(ex);
    }
}
