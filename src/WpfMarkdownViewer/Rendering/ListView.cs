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
    private readonly List<Marker> _markers = new();
    private readonly List<double> _itemTops = new();

    private double Indent => _theme.ListIndent;

    public ListView(ListBlock list, MarkdownStyle theme, Action<string>? onLink = null)
    {
        _theme = theme;
        int n = 1;
        foreach (var item in ParseItems(list.RawText))
        {
            _markers.Add(item.IsTask
                ? new Marker(null, IsTask: true, item.Checked)
                : new Marker(list.Ordered ? $"{n}." : "•", IsTask: false, Checked: false));
            InternalChildren.Add(new ParagraphView(
                InlineProjector.Project(item.Content), theme, theme.EmSize, FontWeights.Normal,
                lineHeightFactor: theme.ListLineHeight, onLink: onLink));
            n++;
        }
    }

    private readonly record struct Item(string Content, bool IsTask, bool Checked);

    private readonly record struct Marker(string? Text, bool IsTask, bool Checked);

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
            var m = _markers[i];
            if (m.IsTask)
            {
                DrawCheckbox(dc, _itemTops[i], m.Checked);
            }
            else
            {
                var marker = new FormattedText(m.Text!, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    _theme.BaseTypeface, _theme.EmSize, _theme.Foreground, dpi);
                dc.DrawText(marker, new Point(Indent - 18, _itemTops[i]));
            }
        }
    }

    /// <summary>Self-drawn checkbox (uniform box; accent fill + check when checked) so it matches the unchecked box and never overlaps the text.</summary>
    private void DrawCheckbox(DrawingContext dc, double itemTop, bool isChecked)
    {
        double size = Math.Round(_theme.EmSize * 0.92);
        double lineHeight = _theme.EmSize * _theme.ListLineHeight;
        double bx = Indent - 6 - size;
        double by = itemTop + (lineHeight - size) / 2;
        var rect = new Rect(bx, by, size, size);

        if (isChecked)
        {
            dc.DrawRoundedRectangle(_theme.LinkBrush, null, rect, 3, 3);
            var check = new Pen(_theme.Background, Math.Max(1.4, size * 0.13))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            var p0 = new Point(bx + size * 0.24, by + size * 0.52);
            var p1 = new Point(bx + size * 0.42, by + size * 0.70);
            var p2 = new Point(bx + size * 0.76, by + size * 0.30);
            dc.DrawLine(check, p0, p1);
            dc.DrawLine(check, p1, p2);
        }
        else
        {
            dc.DrawRoundedRectangle(null, new Pen(_theme.QuoteBar, 1.3), rect, 3, 3);
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
