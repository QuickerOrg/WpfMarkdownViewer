using WpfMarkdownViewer.Controls;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Controls;

public class MarkdownScrollHostTests
{
    [WpfFact]
    public void DefaultsToStickToBottom_AndHostsContent()
    {
        var host = new MarkdownScrollHost();
        var view = new MarkdownDocumentView();

        host.Content = view;

        Assert.True(host.IsStickToBottom);
        Assert.Same(view, host.Content);
    }

    [WpfFact]
    public void ApplyTheme_SwitchesViewBackground()
    {
        var view = new MarkdownDocumentView();

        view.ApplyTheme(TextRenderTheme.Dark);

        Assert.Same(TextRenderTheme.Dark.Background, view.Background);
    }
}
