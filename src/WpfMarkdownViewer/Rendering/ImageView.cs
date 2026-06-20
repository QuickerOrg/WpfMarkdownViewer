using System.Globalization;
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

    private static readonly Dictionary<string, BitmapImage> Cache = new();

    private readonly string _url;
    private readonly string _alt;
    private readonly MarkdownStyle _theme;

    private BitmapImage? _bitmap;
    private bool _failed;

    public ImageView(ImageBlock block, MarkdownStyle theme)
    {
        _url = block.Url;
        _alt = block.Alt;
        _theme = theme;
        BeginLoad();
    }

    private void BeginLoad()
    {
        if (Cache.TryGetValue(_url, out var cached))
        {
            _bitmap = cached;
            return;
        }
        if (!Uri.TryCreate(_url, UriKind.Absolute, out var uri))
        {
            _failed = true;
            return;
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
                bmp.DownloadCompleted += (_, _) => OnLoaded(bmp);
                bmp.DownloadFailed += (_, _) => OnFailed();
                bmp.DecodeFailed += (_, _) => OnFailed();
            }
            else
            {
                OnLoaded(bmp);
            }
        }
        catch
        {
            _failed = true;
        }
    }

    private void OnLoaded(BitmapImage bmp)
    {
        if (bmp.CanFreeze)
            bmp.Freeze();
        Cache[_url] = bmp;
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
