using System.IO;
using System.Net.Http;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// Loads SVG images as scalable WPF vector drawings via SharpVectors (M3, parity with LiveMarkdown). Unlike
/// bitmaps these stay crisp at any size and follow no theme — the SVG's own colors are used. Parsing runs
/// off the UI thread and the result is frozen, so it can be handed back to the UI cheaply.
/// </summary>
internal static class SvgImage
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static bool IsSvg(Uri uri) =>
        uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    public static async Task<DrawingImage?> LoadAsync(Uri uri)
    {
        try
        {
            byte[] bytes = uri.IsFile
                ? await File.ReadAllBytesAsync(uri.LocalPath)
                : await Http.GetByteArrayAsync(uri);
            return await Task.Run(() => Parse(bytes));
        }
        catch
        {
            return null;
        }
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
