using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// A self-drawn block image (M2-4). Loads the bitmap asynchronously (http downloads stream in), shows a
/// placeholder while loading and an error box on failure, and scales to fit the available width keeping
/// aspect ratio. Decoded bitmaps are cached per URL. Inline images and relative-path bases are later work.
/// </summary>
internal sealed class ImageView : FrameworkElement
{
    private const double PlaceholderHeight = 140;
    private const double PlaceholderWidth = 280;

    private readonly string _url;
    private readonly string _alt;
    private readonly string? _basePath;
    private readonly MarkdownStyle _theme;

    private string _cacheKey = string.Empty;
    private BitmapImage? _bitmap;
    private bool _failed;

    public ImageView(ImageBlock block, MarkdownStyle theme, string? basePath = null)
    {
        _url = block.Url;
        _alt = block.Alt;
        _basePath = basePath;
        _theme = theme;
        BeginLoad();
    }

    private void BeginLoad()
    {
        var uri = Resolve(_url, _basePath);
        if (uri is null)
        {
            _failed = true;
            return;
        }
        _cacheKey = uri.ToString();

        if (ImageCache.TryMemory(_cacheKey, out var mem))
        {
            _bitmap = mem;
            return;
        }

        string cacheFile = ImageCache.FileFor(_cacheKey);
        if (File.Exists(cacheFile))
        {
            try
            {
                OnLoaded(LoadFromFile(cacheFile));
                return;
            }
            catch { /* fall through to re-fetch */ }
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = uri;
            bmp.EndInit();

            if (bmp.IsDownloading)
            {
                bmp.DownloadCompleted += (_, _) => { ImageCache.Save(cacheFile, bmp); OnLoaded(bmp); };
                bmp.DownloadFailed += (_, _) => OnFailed();
                bmp.DecodeFailed += (_, _) => OnFailed();
            }
            else
            {
                ImageCache.Save(cacheFile, bmp);
                OnLoaded(bmp);
            }
        }
        catch
        {
            _failed = true;
        }
    }

    private static BitmapImage LoadFromFile(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        return bmp;
    }

    private static Uri? Resolve(string url, string? basePath)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs))
            return abs;
        if (string.IsNullOrEmpty(basePath))
            return null;
        if (Uri.TryCreate(basePath, UriKind.Absolute, out var baseUri) && baseUri.Scheme is "http" or "https")
            return Uri.TryCreate(baseUri, url, out var combined) ? combined : null;
        try
        {
            return new Uri(Path.GetFullPath(Path.Combine(basePath, url)));
        }
        catch
        {
            return null;
        }
    }

    private void OnLoaded(BitmapImage bmp)
    {
        if (bmp.CanFreeze)
            bmp.Freeze();
        if (_cacheKey.Length > 0)
            ImageCache.PutMemory(_cacheKey, bmp);
        _bitmap = bmp;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnFailed()
    {
        _failed = true;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double maxW = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        if (_bitmap is { PixelWidth: > 0 })
        {
            double w = Math.Min(maxW, _bitmap.PixelWidth);
            double h = _bitmap.PixelHeight * (w / _bitmap.PixelWidth);
            return new Size(w, h);
        }
        return new Size(Math.Min(maxW, PlaceholderWidth), PlaceholderHeight);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(DrawingContext dc)
    {
        if (_bitmap is not null)
        {
            dc.DrawImage(_bitmap, new Rect(0, 0, RenderSize.Width, RenderSize.Height));
            return;
        }

        dc.DrawRoundedRectangle(_theme.CodeBlockBackground, new Pen(_theme.Border, 1),
            new Rect(0, 0, RenderSize.Width, RenderSize.Height), 6, 6);

        double dpi;
        try { dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { dpi = 1.0; }
        string label = _failed ? $"⚠ 图片加载失败 {_alt}" : $"加载图片… {_alt}";
        var ft = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            _theme.BaseTypeface, _theme.EmSize - 1, _theme.Foreground, dpi)
        {
            MaxTextWidth = Math.Max(1, RenderSize.Width - 20),
            MaxLineCount = 3,
        };
        dc.DrawText(ft, new Point(10, 10));
    }
}
