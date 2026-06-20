using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfMath;
using WpfMath.Parsers;
using WpfMath.Rendering;
using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// A self-drawn block math formula (M2-5) rendered natively to vector geometry via WpfMath (no browser),
/// filled with the style foreground so it follows the theme. On parse failure, falls back to the raw
/// LaTeX in monospace. Inline math (<c>$…$</c>) is handled separately by <see cref="MathInlineObject"/>.
/// </summary>
internal sealed class MathView : FrameworkElement
{
    private static readonly XamlMath.TexFormulaParser Parser = WpfTeXFormulaParser.Instance;

    private readonly string _latex;
    private readonly MarkdownStyle _theme;
    private readonly double _scale;

    private Geometry? _geometry;
    private Rect _bounds;
    private string? _error;

    public MathView(MathBlock block, MarkdownStyle theme)
    {
        _latex = MathText.Extract(block.RawText);
        _theme = theme;
        _scale = theme.EmSize * 1.4;
        Build();
    }

    private void Build()
    {
        try
        {
            var formula = Parser.Parse(_latex);
            var environment = WpfTeXEnvironment.Create(
                XamlMath.TexStyle.Display, _scale, "Arial", _theme.Foreground, _theme.Background);
            var geometry = formula.RenderToGeometry(environment, _scale, 0, 0);
            geometry.Freeze();
            _bounds = geometry.Bounds;
            if (_bounds.IsEmpty || _bounds.Width <= 0 || _bounds.Height <= 0)
                _error = "empty formula";
            else
                _geometry = geometry;
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_geometry is not null)
            return new Size(_bounds.Width + 4, _bounds.Height + 4);
        return new Size(Math.Min(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width, 400), _scale + 8);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(DrawingContext dc)
    {
        if (_geometry is not null)
        {
            dc.PushTransform(new TranslateTransform(2 - _bounds.Left, 2 - _bounds.Top));
            dc.DrawGeometry(_theme.Foreground, null, _geometry);
            dc.Pop();
            return;
        }

        double dpi;
        try { dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { dpi = 1.0; }
        var ft = new FormattedText($"$$ {_latex} $$", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            _theme.MonoTypeface, _theme.EmSize - 1, _theme.CodeForeground, dpi);
        dc.DrawText(ft, new Point(0, 0));
    }
}
