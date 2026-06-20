using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using WpfMath.Parsers;
using WpfMath.Rendering;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// Builds and caches vector <see cref="Geometry"/> for inline math (<c>$…$</c>) via WpfMath (ADR-0012,
/// extended from block to inline). The geometry is colourless — it is filled with the theme brush at draw
/// time — so it depends only on the LaTeX and the font scale, which makes it safe to cache and reuse.
/// </summary>
internal static class InlineMath
{
    private static readonly XamlMath.TexFormulaParser Parser = WpfTeXFormulaParser.Instance;
    private static readonly ConcurrentDictionary<(string Latex, double Scale), (Geometry? Geometry, Rect Bounds)> Cache = new();

    /// <summary>Render <paramref name="latex"/> to geometry whose baseline sits at y = 0. False if it fails to parse / is empty.</summary>
    public static bool TryBuild(string latex, double scale, out Geometry geometry, out Rect bounds)
    {
        var (g, b) = Cache.GetOrAdd((latex, scale), key => Render(key.Latex, key.Scale));
        geometry = g!;
        bounds = b;
        return g is not null;
    }

    private static (Geometry?, Rect) Render(string latex, double scale)
    {
        try
        {
            var formula = Parser.Parse(latex);
            var environment = WpfTeXEnvironment.Create(
                XamlMath.TexStyle.Text, scale, "Arial", Brushes.Black, Brushes.Transparent);
            var geometry = formula.RenderToGeometry(environment, scale, 0, 0);
            geometry.Freeze();
            var bounds = geometry.Bounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                return (null, Rect.Empty);
            return (geometry, bounds);
        }
        catch
        {
            return (null, Rect.Empty);
        }
    }
}
