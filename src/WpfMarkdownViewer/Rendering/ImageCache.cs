using System.Windows.Media;

namespace WpfMarkdownViewer.Rendering;

/// <summary>In-memory cache of decoded images (bitmaps and SVG drawings) by resolved URI. Disk caching and revalidation of remote bytes live in <see cref="ImageLoader"/>.</summary>
internal static class ImageCache
{
    private static readonly Dictionary<string, ImageSource> Memory = new();

    public static bool TryMemory(string key, out ImageSource image) => Memory.TryGetValue(key, out image!);

    public static void PutMemory(string key, ImageSource image) => Memory[key] = image;
}
