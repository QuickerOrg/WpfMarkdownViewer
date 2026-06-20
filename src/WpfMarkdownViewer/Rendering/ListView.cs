using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// A self-drawn list: each item is a wrapped <see cref="ParagraphView"/> on its own line with a bullet
/// (unordered) or number (ordered) drawn in the gutter. M1 renders a flat list; nesting is later.
/// </summary>
internal sealed class ListView : Panel
{
    private const double Indent = 24;
    private const double ItemSpacing = 2;

    private readonly TextRenderTheme _theme;
    private readonly List<string> _markers = new();
    private readonly List<double> _itemTops = new();

    public ListView(ListBlock list, TextRenderTheme theme)
    {
        _theme = theme;
        int n = 1;
        foreach (string content in ParseItems(list.RawText))
        {
            _markers.Add(list.Ordered ? $"{n}." : "•");
            InternalChildren.Add(new ParagraphView(
                InlineProjector.Project(content), theme, theme.EmSize, FontWeights.Normal));
            n++;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double contentW = Math.Max(1, (double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width) - Indent);
        double y = 0, maxChildW = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(contentW, double.PositiveInfinity));
            y += child.DesiredSize.Height + ItemSpacing;
            maxChildW = Math.Max(maxChildW, child.DesiredSize.Width);
        }
        double height = y > 0 ? y - ItemSpacing : 0;
        double width = double.IsInfinity(availableSize.Width) ? maxChildW + Indent : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _itemTops.Clear();
        double y = 0;
        double childW = Math.Max(0, finalSize.Width - Indent);
        foreach (UIElement child in InternalChildren)
        {
            _itemTops.Add(y);
            child.Arrange(new Rect(Indent, y, childW, child.DesiredSize.Height));
            y += child.DesiredSize.Height + ItemSpacing;
        }
        return finalSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double dpi;
        try { dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { dpi = 1.0; }

        for (int i = 0; i < _markers.Count && i < _itemTops.Count; i++)
        {
            var marker = new FormattedText(_markers[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                _theme.BaseTypeface, _theme.EmSize, _theme.Foreground, dpi);
            dc.DrawText(marker, new Point(Indent - 18, _itemTops[i]));
        }
    }

    private static IEnumerable<string> ParseItems(string raw)
    {
        foreach (string line in raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            string t = line.TrimStart();
            if (t.Length >= 2 && (t[0] is '-' or '*' or '+') && t[1] == ' ')
            {
                yield return t[2..].Trim();
                continue;
            }
            int d = 0;
            while (d < t.Length && char.IsAsciiDigit(t[d]))
                d++;
            if (d > 0 && d < t.Length && (t[d] is '.' or ')'))
            {
                yield return t[(d + 1)..].Trim();
                continue;
            }
            yield return t.Trim(); // continuation / fallback
        }
    }
}
