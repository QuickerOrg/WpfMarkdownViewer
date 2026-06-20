using System.Collections.Concurrent;
using System.Text;
using System.Windows.Media;
using Mermaider;
using MermaidOptions = Mermaider.Models.RenderOptions;

namespace WpfMarkdownViewer.Rendering;

/// <summary>What to render: the Mermaid source plus the theme to colour it with.</summary>
public sealed record MermaidRequest(string Source, MarkdownStyle Theme);

/// <summary>
/// Pluggable renderer for <c>```mermaid</c> blocks (ADR-0010 extension surface). The default is a pure-.NET
/// engine (no browser/JS); a host can swap in a remote service or a WebView2-based renderer by assigning
/// <see cref="Mermaid.Renderer"/>. Returns a frozen <see cref="ImageSource"/> (vector when possible) or null
/// to fall back to a code block.
/// </summary>
public interface IMermaidRenderer
{
    Task<ImageSource?> RenderAsync(MermaidRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Configures how <c>```mermaid</c> diagrams are rendered.</summary>
public static class Mermaid
{
    /// <summary>The active renderer. Default: built-in pure-.NET (Mermaider) → SVG → vector. Set null to render mermaid as a plain code block; set a custom one to swap engines.</summary>
    public static IMermaidRenderer? Renderer { get; set; } = new BuiltInMermaidRenderer();
}

/// <summary>Built-in renderer: Mermaider parses + lays out + renders the diagram to SVG (off the UI thread), then SharpVectors turns it into a scalable vector drawing. Results are cached per source + theme.</summary>
internal sealed class BuiltInMermaidRenderer : IMermaidRenderer
{
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new();

    public Task<ImageSource?> RenderAsync(MermaidRequest request, CancellationToken cancellationToken = default)
    {
        var theme = request.Theme;
        string key = string.Join('|', request.Source, Hex(theme.Background), Hex(theme.Foreground), Hex(theme.Border), Hex(theme.LinkBrush));
        if (Cache.TryGetValue(key, out var cached))
            return Task.FromResult<ImageSource?>(cached);

        return Task.Run(() =>
        {
            try
            {
                var options = new MermaidOptions
                {
                    Bg = Hex(theme.Background),
                    Fg = Hex(theme.Foreground),
                    Surface = Hex(theme.CodeBlockBackground),
                    Line = Hex(theme.Border),
                    Border = Hex(theme.Border),
                    Accent = Hex(theme.LinkBrush),
                    Muted = Hex(theme.SubtleForeground),
                    RoundedEdges = true,
                };
                string svg = MermaidRenderer.RenderSvg(request.Source, options);
                svg = MermaidSvgFlattener.Flatten(svg); // resolve CSS vars/color-mix that SharpVectors can't
                var image = SvgImage.Parse(Encoding.UTF8.GetBytes(svg));
                if (image is not null)
                    Cache[key] = image;
                return image;
            }
            catch
            {
                return (ImageSource?)null; // invalid or unsupported diagram ⇒ caller falls back to a code block
            }
        }, cancellationToken);
    }

    private static string Hex(Brush brush) =>
        brush is SolidColorBrush s ? $"#{s.Color.R:X2}{s.Color.G:X2}{s.Color.B:X2}" : "#000000";
}
