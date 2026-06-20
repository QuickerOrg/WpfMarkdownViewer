using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace WpfMarkdownViewer.Rendering;

/// <summary>Two-level image cache (M2-4): in-memory by resolved URI, plus a file-backed cache keyed by SHA-256 of the URI.</summary>
internal static class ImageCache
{
    private static readonly string Dir = Path.Combine(Path.GetTempPath(), "WpfMarkdownViewer", "images");
    private static readonly Dictionary<string, BitmapImage> Memory = new();

    public static bool TryMemory(string key, out BitmapImage bitmap) => Memory.TryGetValue(key, out bitmap!);

    public static void PutMemory(string key, BitmapImage bitmap) => Memory[key] = bitmap;

    public static string FileFor(string key)
    {
        Directory.CreateDirectory(Dir);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(Dir, hash + ".img");
    }

    public static void Save(string file, BitmapSource bitmap)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = File.Create(file);
            encoder.Save(fs);
        }
        catch { /* cache is best-effort */ }
    }
}
