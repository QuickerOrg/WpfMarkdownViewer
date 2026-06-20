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
    private const double ItemSpacing = 2;

    private readonly MarkdownStyle _theme;
    private readonly List<string> _markers = new();
    private readonly List<double> _itemTops = new();

    private double Indent => _theme.ListIndent;

    public ListView(ListBlock list, MarkdownStyle theme, Action<string>? onLink = null)
    {
        _theme = theme;
        int n = 1;
        foreach (var item in ParseItems(list.RawText))
        {
            _markers.Add(item.IsTask ? (item.Checked ? "☑" : "☐") : list.Ordered ? $"{n}." : "•");
            InternalChildren.Add(new ParagraphView(
                InlineProjector.Project(item.Content), theme, theme.EmSize, FontWeights.Normal,
                lineHeightFactor: theme.ListLineHeight, onLink: onLink));
            n++;
        }
    }

    private readonly record struct Item(string Content, bool IsTask, bool Checked);

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

    private static IEnumerable<Item> ParseItems(string raw)
    {
        foreach (string line in raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            string t = line.TrimStart();
            string content;
            if (t.Length >= 2 && (t[0] is '-' or '*' or '+') && t[1] == ' ')
            {
                content = t[2..].Trim();
            }
            else
            {
                int d = 0;
                while (d < t.Length && char.IsAsciiDigit(t[d]))
                    d++;
                content = d > 0 && d < t.Length && (t[d] is '.' or ')') ? t[(d + 1)..].Trim() : t.Trim();
            }

            // Task marker: [ ] / [x] / [X] at the start of the item content.
            if (content.Length >= 3 && content[0] == '[' && content[2] == ']' && (content[1] is ' ' or 'x' or 'X'))
                yield return new Item(content[3..].TrimStart(), IsTask: true, Checked: content[1] is 'x' or 'X');
            else
                yield return new Item(content, IsTask: false, Checked: false);
        }
    }
}
