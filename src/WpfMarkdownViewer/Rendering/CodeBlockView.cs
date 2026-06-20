using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfMarkdownViewer.Highlighting;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// A self-drawn code block (ADR-0003): a rounded panel with a header (language label + copy button) and
/// TextMate-highlighted monospace lines drawn with per-token foreground colors. Horizontal scrolling of
/// very wide code is a later refinement; for now content beyond the width is clipped by the parent.
/// </summary>
internal sealed class CodeBlockView : FrameworkElement, ISelectableText
{
    private const double PadX = 12;
    private const double PadY = 10;
    private const double HeaderHeight = 26;

    private static readonly Dictionary<string, Brush> BrushCache = new();
    private static readonly Brush SubtleBrush = Frozen(Color.FromRgb(0x6B, 0x72, 0x80));

    private readonly string _code;
    private readonly string _languageLabel;
    private readonly IReadOnlyList<IReadOnlyList<ColoredSpan>> _lines;
    private readonly MarkdownStyle _theme;
    private readonly Typeface _mono;
    private readonly double _em;
    private readonly List<FormattedText> _formatted = new();

    private Rect _copyRect;
    private bool _copied;
    private DispatcherTimer? _resetTimer;

    private readonly List<string> _lineTexts;
    private double _charWidth;
    private double _lineHeight;
    private int _selStart = -1;
    private int _selEnd = -1;

    public CodeBlockView(string code, string? language, IReadOnlyList<IReadOnlyList<ColoredSpan>> lines, MarkdownStyle theme)
    {
        _code = code;
        _languageLabel = string.IsNullOrWhiteSpace(language) ? "code" : language!.Trim();
        _lines = lines;
        _theme = theme;
        _em = theme.EmSize - 1;
        _mono = new Typeface(theme.MonoTypeface.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        _lineTexts = lines.Select(spans => string.Concat(spans.Select(s => s.Text))).ToList();
    }

    private double Dpi
    {
        get
        {
            try { return VisualTreeHelper.GetDpi(this).PixelsPerDip; }
            catch { return 1.0; }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _formatted.Clear();
        double dpi = Dpi;
        double contentWidth = 0, contentHeight = 0;

        foreach (var spans in _lines)
        {
            string text = string.Concat(spans.Select(s => s.Text));
            var ft = new FormattedText(text.Length == 0 ? " " : text, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, _mono, _em, _theme.Foreground, dpi);

            int idx = 0;
            foreach (var span in spans)
            {
                if (span.Text.Length > 0 && span.ColorHex is not null)
                    ft.SetForegroundBrush(BrushFor(span.ColorHex), idx, span.Text.Length);
                idx += span.Text.Length;
            }

            _formatted.Add(ft);
            contentWidth = Math.Max(contentWidth, ft.WidthIncludingTrailingWhitespace);
            contentHeight += ft.Height;
        }

        _lineHeight = _formatted.Count > 0 ? _formatted[0].Height : _em * 1.4;
        _charWidth = new FormattedText("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _mono, _em, _theme.Foreground, dpi)
            .WidthIncludingTrailingWhitespace;

        double width = double.IsInfinity(availableSize.Width)
            ? contentWidth + 2 * PadX
            : availableSize.Width;
        double height = HeaderHeight + PadY + contentHeight + PadY;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(DrawingContext dc)
    {
        double w = RenderSize.Width, h = RenderSize.Height;
        dc.DrawRoundedRectangle(_theme.CodeBlockBackground, new Pen(_theme.Border, 1), new Rect(0, 0, w, h), 6, 6);
        dc.DrawLine(new Pen(_theme.Border, 1), new Point(0, HeaderHeight), new Point(w, HeaderHeight));

        double dpi = Dpi;
        var label = new FormattedText(_languageLabel, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            _theme.BaseTypeface, 12, SubtleBrush, dpi);
        dc.DrawText(label, new Point(PadX, (HeaderHeight - label.Height) / 2));

        string copyLabel = _copied ? "已复制" : "复制";
        var copy = new FormattedText(copyLabel, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            _theme.BaseTypeface, 12, _copied ? SubtleBrush : _theme.LinkBrush, dpi);
        double cx = w - PadX - copy.Width;
        dc.DrawText(copy, new Point(cx, (HeaderHeight - copy.Height) / 2));
        _copyRect = new Rect(cx - 6, 0, copy.Width + 12, HeaderHeight);

        double y = HeaderHeight + PadY;
        int off = 0;
        for (int li = 0; li < _formatted.Count; li++)
        {
            int lineLen = li < _lineTexts.Count ? _lineTexts[li].Length : 0;
            if (_selStart >= 0 && _selEnd > _selStart)
            {
                int s = Math.Max(_selStart, off);
                int e = Math.Min(_selEnd, off + lineLen);
                if (e > s)
                    dc.DrawRectangle(_theme.SelectionBackground, null,
                        new Rect(PadX + (s - off) * _charWidth, y, (e - s) * _charWidth, _formatted[li].Height));
            }
            dc.DrawText(_formatted[li], new Point(PadX, y));
            y += _formatted[li].Height;
            off += lineLen + 1; // +1 for the newline between lines
        }
    }

    // --- ISelectableText ---

    public string SelectableText => _code;

    public string MarkdownLinePrefix => string.Empty;

    public IReadOnlyList<InlineRun> SelectedRuns(int start, int end)
    {
        start = Math.Clamp(start, 0, _code.Length);
        end = Math.Clamp(end, 0, _code.Length);
        return end > start
            ? new[] { new InlineRun(0, _code[start..end], InlineStyle.None) }
            : Array.Empty<InlineRun>();
    }

    public int OffsetAtPoint(Point p)
    {
        if (_lineTexts.Count == 0 || _lineHeight <= 0)
            return 0;
        int line = (int)Math.Floor((p.Y - (HeaderHeight + PadY)) / _lineHeight);
        line = Math.Clamp(line, 0, _lineTexts.Count - 1);
        int col = _charWidth > 0 ? (int)Math.Round((p.X - PadX) / _charWidth) : 0;
        col = Math.Clamp(col, 0, _lineTexts[line].Length);
        int off = 0;
        for (int k = 0; k < line; k++)
            off += _lineTexts[k].Length + 1;
        return Math.Clamp(off + col, 0, _code.Length);
    }

    public void SetSelectedRange(int start, int end)
    {
        _selStart = Math.Clamp(Math.Min(start, end), 0, _code.Length);
        _selEnd = Math.Clamp(Math.Max(start, end), 0, _code.Length);
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Cursor = _copyRect.Contains(e.GetPosition(this)) ? Cursors.Hand : null;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_copyRect.Contains(e.GetPosition(this)))
        {
            try
            {
                Clipboard.SetText(_code);
                ShowCopied();
            }
            catch { /* clipboard may be locked by another process */ }
            e.Handled = true;
        }
        base.OnMouseLeftButtonDown(e);
    }

    private void ShowCopied()
    {
        _copied = true;
        InvalidateVisual();

        _resetTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _resetTimer.Stop();
        _resetTimer.Tick -= OnResetTick;
        _resetTimer.Tick += OnResetTick;
        _resetTimer.Start();
    }

    private void OnResetTick(object? sender, EventArgs e)
    {
        _resetTimer!.Stop();
        _copied = false;
        InvalidateVisual();
    }

    private static Brush BrushFor(string hex)
    {
        if (BrushCache.TryGetValue(hex, out var cached))
            return cached;
        Brush brush;
        try
        {
            brush = Frozen((Color)ColorConverter.ConvertFromString(hex));
        }
        catch
        {
            brush = SubtleBrush;
        }
        BrushCache[hex] = brush;
        return brush;
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
