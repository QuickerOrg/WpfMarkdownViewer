using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// A self-drawn block of wrapped text (ADR-0005): lays out an <see cref="InlineProjection"/> with WPF's
/// TextFormatter and draws the resulting lines in <see cref="OnRender"/>. Used for paragraphs, headings,
/// and (with a monospace projection + background) the M1 code box before TextMate highlighting (phase D3).
/// </summary>
internal sealed class ParagraphView : FrameworkElement
{
    private readonly TextRenderTheme _theme;
    private readonly double _emSize;
    private readonly FontWeight _weight;
    private readonly Brush? _background;
    private readonly Thickness _padding;
    private readonly bool _monospace;
    private readonly List<TextLine> _lines = new();

    // Kept for the lifetime of the view: the TextLines drawn in OnRender depend on this formatter's
    // context, so disposing it (e.g. a `using` in MeasureOverride) invalidates them ("ClientAbort").
    private TextFormatter? _formatter;

    private InlineProjection _projection;

    public ParagraphView(InlineProjection projection, TextRenderTheme theme,
        double emSize, FontWeight weight, Brush? background = null, Thickness padding = default, bool monospace = false)
    {
        _projection = projection;
        _theme = theme;
        _emSize = emSize;
        _weight = weight;
        _background = background;
        _padding = padding;
        _monospace = monospace;
    }

    /// <summary>Replace the projection (used when the Active Block grows) and re-layout.</summary>
    public void Update(InlineProjection projection)
    {
        _projection = projection;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        ClearLines();

        double wrap = availableSize.Width;
        if (double.IsInfinity(wrap) || wrap <= 0)
            wrap = 2000;
        wrap = Math.Max(1, wrap - _padding.Left - _padding.Right);

        var source = new InlineTextSource(_projection, _theme, _emSize, _weight, _monospace);
        var defaultFamily = _monospace ? _theme.MonoTypeface.FontFamily : _theme.BaseTypeface.FontFamily;
        var defaultTypeface = new Typeface(defaultFamily, FontStyles.Normal, _weight, FontStretches.Normal);
        var defaultProps = new InlineRunProperties(defaultTypeface, _emSize, _theme.Foreground, null, null);
        var paraProps = new InlineParagraphProperties(defaultProps);

        var formatter = _formatter ??= TextFormatter.Create();
        double height = 0, maxWidth = 0;
        int len = _projection.VisibleText.Length;

        if (len == 0)
        {
            var empty = formatter.FormatLine(source, 0, wrap, paraProps, null);
            _lines.Add(empty);
            height = empty.Height;
        }
        else
        {
            int idx = 0;
            while (idx < len)
            {
                var line = formatter.FormatLine(source, idx, wrap, paraProps, null);
                _lines.Add(line);
                height += line.Height;
                maxWidth = Math.Max(maxWidth, line.WidthIncludingTrailingWhitespace);
                if (line.Length <= 0)
                    break;
                idx += line.Length;
            }
        }

        return new Size(maxWidth + _padding.Left + _padding.Right, height + _padding.Top + _padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(DrawingContext dc)
    {
        if (_background is not null)
            dc.DrawRectangle(_background, null, new Rect(new Point(0, 0), RenderSize));

        double y = _padding.Top;
        foreach (var line in _lines)
        {
            line.Draw(dc, new Point(_padding.Left, y), InvertAxes.None);
            y += line.Height;
        }
    }

    private void ClearLines()
    {
        foreach (var line in _lines)
            line.Dispose();
        _lines.Clear();
    }
}
