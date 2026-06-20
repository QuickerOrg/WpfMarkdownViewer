using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// A self-drawn GitHub-flavored table: a grid of inline-projected cells with a shaded, bold header row
/// and 1px borders. Column widths size to content (capped); cells wrap within their column. M1 scope.
/// </summary>
internal sealed class TableView : Panel
{
    private const double MaxColContent = 320;
    private const double MinColContent = 24;

    private readonly MarkdownStyle _theme;

    private double CellPadX => _theme.TableCellPaddingX;
    private double CellPadY => _theme.TableCellPaddingY;
    private readonly int _rows;
    private readonly int _cols;
    private double[] _colW = Array.Empty<double>();
    private double[] _rowH = Array.Empty<double>();

    public TableView(TableBlock table, MarkdownStyle theme, Action<string>? onLink = null)
    {
        _theme = theme;
        var rows = Parse(table.RawText);
        _rows = rows.Count;
        _cols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);

        for (int r = 0; r < _rows; r++)
        {
            bool header = r == 0;
            for (int c = 0; c < _cols; c++)
            {
                string text = c < rows[r].Count ? rows[r][c] : string.Empty;
                InternalChildren.Add(new ParagraphView(
                    InlineProjector.Project(text), theme, theme.EmSize,
                    header ? FontWeights.Bold : FontWeights.Normal, lineHeightFactor: 1.4, onLink: onLink));
            }
        }
    }

    private ParagraphView Cell(int r, int c) => (ParagraphView)InternalChildren[r * _cols + c];

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_cols == 0)
            return new Size(0, 0);

        _colW = new double[_cols];
        _rowH = new double[_rows];

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                var cell = Cell(r, c);
                cell.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                _colW[c] = Math.Max(_colW[c], cell.DesiredSize.Width);
            }

        for (int c = 0; c < _cols; c++)
            _colW[c] = Math.Clamp(_colW[c], MinColContent, MaxColContent) + 2 * CellPadX;

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                var cell = Cell(r, c);
                cell.Measure(new Size(Math.Max(1, _colW[c] - 2 * CellPadX), double.PositiveInfinity));
                _rowH[r] = Math.Max(_rowH[r], cell.DesiredSize.Height + 2 * CellPadY);
            }

        return new Size(_colW.Sum() + 1, _rowH.Sum() + 1);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        for (int r = 0; r < _rows; r++)
        {
            double x = 0;
            for (int c = 0; c < _cols; c++)
            {
                Cell(r, c).Arrange(new Rect(x + CellPadX, y + CellPadY,
                    Math.Max(0, _colW[c] - 2 * CellPadX), Math.Max(0, _rowH[r] - 2 * CellPadY)));
                x += _colW[c];
            }
            y += _rowH[r];
        }
        return finalSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_cols == 0)
            return;

        double tableW = _colW.Sum();
        double tableH = _rowH.Sum();

        if (_rows > 0)
            dc.DrawRectangle(_theme.CodeBlockBackground, null, new Rect(0, 0, tableW, _rowH[0]));

        var pen = new Pen(_theme.Border, 1);

        double y = 0;
        for (int r = 0; r <= _rows; r++)
        {
            dc.DrawLine(pen, new Point(0, y), new Point(tableW, y));
            if (r < _rows)
                y += _rowH[r];
        }

        double x = 0;
        for (int c = 0; c <= _cols; c++)
        {
            dc.DrawLine(pen, new Point(x, 0), new Point(x, tableH));
            if (c < _cols)
                x += _colW[c];
        }
    }

    private static List<List<string>> Parse(string raw)
    {
        var rows = new List<List<string>>();
        foreach (string line in raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line) || !line.Contains('|'))
                continue;
            string t = line.Trim();
            if (t.StartsWith('|'))
                t = t[1..];
            if (t.EndsWith('|'))
                t = t[..^1];
            rows.Add(t.Split('|').Select(s => s.Trim()).ToList());
        }

        if (rows.Count >= 2 && IsDelimiterRow(rows[1]))
            rows.RemoveAt(1);
        return rows;
    }

    private static bool IsDelimiterRow(List<string> cells) =>
        cells.Count > 0 && cells.All(c =>
        {
            string t = c.Trim();
            return t.Length > 0 && t.Contains('-') && t.All(ch => ch is '-' or ':');
        });
}
