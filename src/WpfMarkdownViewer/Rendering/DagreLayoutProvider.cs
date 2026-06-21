using Mermaider;
using Mermaider.Layout;
using Mermaider.Models;
using Mostlylucid.Dagre;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// A Mermaider layout provider that re-lays-out flowcharts with the dagre algorithm (Mostlylucid.Dagre) for
/// noticeably better node placement and edge routing than Mermaider's built-in layout. It reuses Mermaider's
/// own node sizing (via its internal default provider) and only overrides positions + edge geometry, and
/// falls back to the default layout for anything dagre can't handle (subgraphs, errors). Class/ER diagrams
/// delegate unchanged.
/// </summary>
internal sealed class DagreLayoutProvider : IGraphLayoutProvider
{
    // Built once; null if Mermaider's internal default provider can't be reflected (then we leave Mermaider's own layout in place).
    public static readonly DagreLayoutProvider? Instance = TryCreate();

    private readonly IGraphLayoutProvider _default;

    private DagreLayoutProvider(IGraphLayoutProvider @default) => _default = @default;

    private static DagreLayoutProvider? TryCreate()
    {
        try
        {
            var type = typeof(MermaidRenderer).Assembly.GetType("Mermaider.Layout.DefaultLayoutProvider");
            if (type is null || Activator.CreateInstance(type, nonPublic: true) is not IGraphLayoutProvider def)
                return null;
            return new DagreLayoutProvider(def);
        }
        catch
        {
            return null;
        }
    }

    public PositionedClassDiagram LayoutClass(ClassDiagram diagram) => _default.LayoutClass(diagram);

    public PositionedErDiagram LayoutEr(ErDiagram diagram) => _default.LayoutEr(diagram);

    public PositionedGraph LayoutFlowchart(MermaidGraph graph, RenderOptions? options = null, StrictModeOptions? strict = null)
    {
        var basePg = _default.LayoutFlowchart(graph, options, strict);

        // Subgraphs (compound layout) and trivial graphs stay on Mermaider's layout.
        if (graph.Subgraphs.Count > 0 || basePg.Nodes.Count < 2)
            return basePg;

        try
        {
            return RelayoutWithDagre(graph, basePg);
        }
        catch
        {
            return basePg; // never worse than Mermaider's own layout
        }
    }

    private static PositionedGraph RelayoutWithDagre(MermaidGraph graph, PositionedGraph basePg)
    {
        var dg = new DagreInputGraph { VerticalLayout = graph.Direction is Direction.TD or Direction.TB or Direction.BT };

        var dn = new Dictionary<string, DagreInputNode>();
        foreach (var n in basePg.Nodes)
            dn[n.Id] = dg.AddNode(n.Id, (float)n.Width, (float)n.Height);

        // dagre 2.0.1 throws on cycles, so reverse back-edges ourselves (acyclify) and undo on output.
        var back = DetectBackEdges(basePg.Edges);
        var dEdges = new DagreInputEdge?[basePg.Edges.Count];
        for (int i = 0; i < basePg.Edges.Count; i++)
        {
            var e = basePg.Edges[i];
            if (e.Source == e.Target || !dn.ContainsKey(e.Source) || !dn.ContainsKey(e.Target))
                continue; // self-loop / unknown endpoint: keep base geometry
            dEdges[i] = back.Contains(i)
                ? dg.AddEdge(dn[e.Target], dn[e.Source], 1)
                : dg.AddEdge(dn[e.Source], dn[e.Target], 1);
        }

        dg.Layout(null);

        var nodes = basePg.Nodes
            .Select(n => n with { X = dn[n.Id].X, Y = dn[n.Id].Y })
            .ToList();

        var edges = new List<PositionedEdge>(basePg.Edges.Count);
        for (int i = 0; i < basePg.Edges.Count; i++)
        {
            var e = basePg.Edges[i];
            if (dEdges[i] is not { } de || de.Points is not { Length: > 0 })
            {
                edges.Add(e); // unchanged base geometry
                continue;
            }
            var pts = de.Points.Select(p => new Point(p.X, p.Y)).ToList();
            if (back.Contains(i))
                pts.Reverse();
            Point? label = !string.IsNullOrEmpty(e.Label) ? pts[pts.Count / 2] : null;
            edges.Add(e with { Points = pts, LabelPosition = label });
        }

        const double margin = 8;
        double width = nodes.Count == 0 ? basePg.Width : nodes.Max(n => n.X + n.Width) + margin;
        double height = nodes.Count == 0 ? basePg.Height : nodes.Max(n => n.Y + n.Height) + margin;

        return basePg with { Width = width, Height = height, Nodes = nodes, Edges = edges };
    }

    /// <summary>DFS edge classification: an edge to a node currently on the recursion stack is a back-edge (part of a cycle).</summary>
    private static HashSet<int> DetectBackEdges(IReadOnlyList<PositionedEdge> edges)
    {
        var adj = new Dictionary<string, List<(string To, int Index)>>();
        void Ensure(string id) { if (!adj.ContainsKey(id)) adj[id] = new List<(string, int)>(); }
        for (int i = 0; i < edges.Count; i++)
        {
            Ensure(edges[i].Source);
            Ensure(edges[i].Target);
            if (edges[i].Source != edges[i].Target)
                adj[edges[i].Source].Add((edges[i].Target, i));
        }

        var color = new Dictionary<string, int>(); // 0 unseen, 1 on-stack, 2 done
        var back = new HashSet<int>();

        void Visit(string u)
        {
            color[u] = 1;
            foreach (var (v, idx) in adj[u])
            {
                int c = color.GetValueOrDefault(v);
                if (c == 1)
                    back.Add(idx);
                else if (c == 0)
                    Visit(v);
            }
            color[u] = 2;
        }

        foreach (var id in adj.Keys.ToList())
            if (color.GetValueOrDefault(id) == 0)
                Visit(id);

        return back;
    }
}
