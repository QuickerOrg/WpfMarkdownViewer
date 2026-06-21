using Mermaider;
using Mermaider.Models;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Rendering;

public class DagreLayoutProviderTests
{
    private const string Prime =
        "flowchart TD\n  A[输入 n] --> B{n < 2 ?}\n  B -- 是 --> C[非质数]\n  B -- 否 --> D[i = 2]\n" +
        "  D --> E{i*i <= n ?}\n  E -- 否 --> F[是质数]\n  E -- 是 --> G{n % i == 0 ?}\n  G -- 是 --> C\n  G -- 否 --> H[i++] --> E";

    private static PositionedGraph Layout(string src)
    {
        Assert.NotNull(DagreLayoutProvider.Instance);
        return DagreLayoutProvider.Instance!.LayoutFlowchart(MermaidRenderer.Parse(src), new RenderOptions(), new StrictModeOptions());
    }

    [Fact]
    public void CyclicFlowchart_LaysOutWithoutThrowing_InLayeredOrder()
    {
        var pg = Layout(Prime); // contains the cycle E→G→H→E that crashes raw dagre 2.0.1

        Assert.Equal(8, pg.Nodes.Count);
        var y = pg.Nodes.ToDictionary(n => n.Id, n => n.Y);
        // Sugiyama layering top-to-bottom: A above B above D above E above G.
        Assert.True(y["A"] < y["B"], "A above B");
        Assert.True(y["B"] < y["D"], "B above D");
        Assert.True(y["D"] < y["E"], "D above E");
        Assert.True(y["E"] < y["G"], "E above G");
    }

    [Fact]
    public void BackEdge_IsRoutedWithPoints()
    {
        var pg = Layout(Prime);
        var backEdge = pg.Edges.First(e => e.Source == "H" && e.Target == "E");
        Assert.True(backEdge.Points.Count >= 2);
    }

    [Fact]
    public void NodeSizes_ArePreservedFromMermaider()
    {
        var pg = Layout(Prime);
        // sizes come from Mermaider's own measurement, so every node has a real box
        Assert.All(pg.Nodes, n => Assert.True(n.Width > 0 && n.Height > 0));
    }

    [Fact]
    public void RenderSvg_WithDagreProvider_DiffersFromDefault()
    {
        string def = MermaidRenderer.RenderSvg(Prime, new RenderOptions());
        string dag = MermaidRenderer.RenderSvg(Prime, new RenderOptions { LayoutProvider = DagreLayoutProvider.Instance });

        Assert.NotEqual(def, dag); // the dagre layout actually takes effect end-to-end
    }
}
