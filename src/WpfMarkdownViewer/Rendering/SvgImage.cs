using System.IO;
using System.Text;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// Parses SVG bytes into a scalable WPF vector drawing via SharpVectors (M3, parity with LiveMarkdown).
/// Unlike bitmaps these stay crisp at any size and follow no theme — the SVG's own colors are used.
/// Bytes are fetched by <see cref="ImageLoader"/>; parsing is done off the UI thread and the result frozen.
/// </summary>
internal static class SvgImage
{
    /// <summary>Extension hint (the authoritative check is <see cref="LooksLikeSvg"/> on the fetched bytes).</summary>
    public static bool IsSvg(Uri uri) =>
        uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    /// <summary>Sniff whether bytes are SVG (XML with an &lt;svg&gt; root), independent of URL extension.</summary>
    public static bool LooksLikeSvg(byte[] bytes)
    {
        int n = Math.Min(bytes.Length, 512);
        string head = Encoding.UTF8.GetString(bytes, 0, n);
        int svg = head.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svg < 0)
            return false;
        int xml = head.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        return xml < 0 || xml <= svg; // only the XML prolog (if any) may precede <svg
    }

    public static DrawingImage? Parse(byte[] bytes)
    {
        try
        {
            var settings = new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true };
            using var reader = new FileSvgReader(settings, isEmbedded: false);
            using var stream = new MemoryStream(bytes);
            DrawingGroup? drawing = reader.Read(stream);
            if (drawing is null || drawing.Bounds.Width <= 0 || drawing.Bounds.Height <= 0)
                return null;

            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
