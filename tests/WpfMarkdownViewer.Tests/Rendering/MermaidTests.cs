using System.Windows;
using System.Windows.Media;
using WpfMarkdownViewer.Controls;
using WpfMarkdownViewer.Mermaid;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Rendering;

public class MermaidTests
{
    private static T? FindDescendant<T>(DependencyObject root) where T : class
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;
            if (FindDescendant<T>(child) is { } deep)
                return deep;
        }
        return null;
    }

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
    public async Task BuiltInRenderer_RendersFlowchart_ToScalableImage()
    {
        var image = await new BuiltInMermaidRenderer()
            .RenderAsync(new MermaidRequest("flowchart TD\n  A[Start] --> B[End]", MarkdownStyle.Light));

        Assert.NotNull(image);
        Assert.True(image!.Width > 0 && image.Height > 0);
        Assert.True(image.IsFrozen);
    }

    [WpfFact]
    public void MermaidBlock_RoutesToMermaidView_WhenRendererPresent()
    {
        var view = LaidOut("```mermaid\nflowchart TD\n  A --> B\n```");

        Assert.NotNull(FindDescendant<MermaidView>(view));
        Assert.Null(FindDescendant<CodeBlockView>(view));
    }

    [WpfFact]
    public void MermaidBlock_FallsBackToCodeBlock_WhenRendererDisabled()
    {
        var previous = Capabilities.Mermaid;
        Capabilities.Mermaid = null;
        try
        {
            var view = LaidOut("```mermaid\nflowchart TD\n  A --> B\n```");

            Assert.NotNull(FindDescendant<CodeBlockView>(view));
            Assert.Null(FindDescendant<MermaidView>(view));
        }
        finally
        {
            Capabilities.Mermaid = previous;
        }
    }

    [WpfFact]
    public void CustomRenderer_CanBeInstalled()
    {
        var previous = Capabilities.Mermaid;
        var custom = new FakeRenderer();
        Capabilities.Mermaid = custom;
        try
        {
            Assert.Same(custom, Capabilities.Mermaid);
        }
        finally
        {
            Capabilities.Mermaid = previous;
        }
    }

    private sealed class FakeRenderer : IMermaidRenderer
    {
        public Task<ImageSource?> RenderAsync(MermaidRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<ImageSource?>(null);
    }
}
