using System.Text;
using System.Windows;
using System.Windows.Media;
using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// Document-level drag-selection over the <see cref="ISelectableText"/> leaves beneath a root element
/// (ADR-0008). Treats every selectable leaf under the root, in visual-tree order, as one flat text space;
/// maps points to (segment, offset), highlights the spanned ranges, and serializes the selection to
/// plain-text Markdown for copy. Reused by both the single-Document view and the Conversation Shell so a
/// drag can span Blocks within a message and, at the shell level, span messages.
/// </summary>
internal sealed class SelectionController
{
    private readonly FrameworkElement _root;
    private readonly List<ISelectableText> _selectables = new();
    private (int Segment, int Offset) _anchor;
    private (int Segment, int Offset) _focus;

    public SelectionController(FrameworkElement root) => _root = root;

    public bool IsDragging { get; private set; }
    public bool HasSelection { get; private set; }

    /// <summary>Start a drag at <paramref name="point"/> (root coordinates). False if there is nothing selectable.</summary>
    public bool Begin(Point point)
    {
        RebuildSelectables();
        if (_selectables.Count == 0)
            return false;
        _anchor = _focus = Locate(point);
        Clear();
        IsDragging = true;
        return true;
    }

    public void Update(Point point)
    {
        _focus = Locate(point);
        Apply();
    }

    public void End() => IsDragging = false;

    public void SelectAll()
    {
        RebuildSelectables();
        if (_selectables.Count == 0)
            return;
        _anchor = (0, 0);
        _focus = (_selectables.Count - 1, _selectables[^1].SelectableText.Length);
        Apply();
    }

    public void Clear()
    {
        HasSelection = false;
        foreach (var s in _selectables)
            s.SetSelectedRange(0, 0);
    }

    /// <summary>Copy the selection as plain-text Markdown only (the text IS the Markdown source, so it round-trips).</summary>
    public void Copy()
    {
        if (!HasSelection || _selectables.Count == 0)
            return;
        string markdown = BuildMarkdown();
        if (markdown.Length == 0)
            return;
        try
        {
            var data = new DataObject();
            data.SetText(markdown, TextDataFormat.UnicodeText);
            Clipboard.SetDataObject(data, true);
        }
        catch { /* clipboard busy */ }
    }

    private void RebuildSelectables()
    {
        _selectables.Clear();
        Collect(_root);
        // Visual-tree order can diverge from top-to-bottom order (virtualization re-adds re-realized
        // elements at the end), so order segments by their on-screen Y to keep selection coordinates monotonic.
        var ordered = _selectables.OrderBy(TopOf).ToList(); // OrderBy is stable, preserving ties' tree order
        _selectables.Clear();
        _selectables.AddRange(ordered);
    }

    private double TopOf(ISelectableText s)
    {
        var fe = (FrameworkElement)s;
        try { return fe.TransformToAncestor(_root).Transform(new Point(0, 0)).Y; }
        catch { return 0; }
    }

    private void Collect(DependencyObject node)
    {
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is ISelectableText selectable)
                _selectables.Add(selectable);
            else
                Collect(child);
        }
    }

    private (int Segment, int Offset) Locate(Point p)
    {
        for (int i = 0; i < _selectables.Count; i++)
        {
            var fe = (FrameworkElement)_selectables[i];
            Point topLeft = fe.TransformToAncestor(_root).Transform(new Point(0, 0));
            if (p.Y <= topLeft.Y + fe.RenderSize.Height)
            {
                double localY = Math.Clamp(p.Y - topLeft.Y, 0, Math.Max(0, fe.RenderSize.Height - 1));
                return (i, _selectables[i].OffsetAtPoint(new Point(p.X - topLeft.X, localY)));
            }
        }
        int last = _selectables.Count - 1;
        return (last, _selectables[last].SelectableText.Length);
    }

    private (int Lo, int LoOff, int Hi, int HiOff) Ordered()
    {
        bool anchorFirst = _anchor.Segment < _focus.Segment
            || (_anchor.Segment == _focus.Segment && _anchor.Offset <= _focus.Offset);
        return anchorFirst
            ? (_anchor.Segment, _anchor.Offset, _focus.Segment, _focus.Offset)
            : (_focus.Segment, _focus.Offset, _anchor.Segment, _anchor.Offset);
    }

    private void Apply()
    {
        var (lo, loOff, hi, hiOff) = Ordered();
        HasSelection = lo != hi || loOff != hiOff;
        for (int i = 0; i < _selectables.Count; i++)
        {
            if (i < lo || i > hi)
                _selectables[i].SetSelectedRange(0, 0);
            else if (lo == hi)
                _selectables[i].SetSelectedRange(loOff, hiOff);
            else if (i == lo)
                _selectables[i].SetSelectedRange(loOff, _selectables[i].SelectableText.Length);
            else if (i == hi)
                _selectables[i].SetSelectedRange(0, hiOff);
            else
                _selectables[i].SetSelectedRange(0, _selectables[i].SelectableText.Length);
        }
    }

    private IEnumerable<(int Index, IReadOnlyList<InlineRun> Runs)> SelectedRunsPerSegment()
    {
        var (lo, loOff, hi, hiOff) = Ordered();
        for (int i = lo; i <= hi; i++)
        {
            int s = i == lo ? loOff : 0;
            int e = i == hi ? hiOff : _selectables[i].SelectableText.Length;
            if (e > s)
                yield return (i, _selectables[i].SelectedRuns(s, e));
        }
    }

    public string BuildMarkdown()
    {
        var (lo, loOff, hi, hiOff) = Ordered();
        var sb = new StringBuilder();
        bool prevBlock = false;
        for (int i = lo; i <= hi; i++)
        {
            int s = i == lo ? loOff : 0;
            int e = i == hi ? hiOff : _selectables[i].SelectableText.Length;
            if (e <= s)
                continue;

            string piece;
            bool block = false;
            // Code blocks and tables rebuild their own block Markdown; ordinary text uses prefix + inline runs.
            if (_selectables[i].SelectedBlockMarkdown(s, e) is { Length: > 0 } blockMd)
            {
                piece = blockMd;
                block = true;
            }
            else
            {
                string prefix = i != lo || loOff == 0 ? _selectables[i].MarkdownLinePrefix : string.Empty;
                piece = prefix + RunSerializer.ToMarkdown(_selectables[i].SelectedRuns(s, e));
            }

            if (sb.Length > 0)
                sb.Append(block || prevBlock ? "\n\n" : "\n"); // blank line around block elements
            sb.Append(piece);
            prevBlock = block;
        }
        return sb.ToString();
    }

    public string BuildHtml() =>
        string.Join("<br>", SelectedRunsPerSegment().Select(x => RunSerializer.ToHtml(x.Runs)));

    public string BuildText()
    {
        if (!HasSelection || _selectables.Count == 0)
            return string.Empty;

        var (lo, loOff, hi, hiOff) = Ordered();
        var sb = new StringBuilder();
        for (int i = lo; i <= hi; i++)
        {
            string text = _selectables[i].SelectableText;
            int s = i == lo ? loOff : 0;
            int e = i == hi ? hiOff : text.Length;
            if (e > s)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(text[s..e]);
            }
        }
        return sb.ToString();
    }

    // --- Test hooks ---

    internal IReadOnlyList<string> SelectableTexts()
    {
        RebuildSelectables();
        return _selectables.Select(s => s.SelectableText).ToList();
    }

    internal string SelectAndGetText(int segA, int offA, int segB, int offB)
    {
        RebuildSelectables();
        _anchor = (segA, offA);
        _focus = (segB, offB);
        Apply();
        return BuildText();
    }

    internal string SelectAndGetMarkdown(int segA, int offA, int segB, int offB)
    {
        RebuildSelectables();
        _anchor = (segA, offA);
        _focus = (segB, offB);
        Apply();
        return BuildMarkdown();
    }

    internal string SelectAndGetHtml(int segA, int offA, int segB, int offB)
    {
        RebuildSelectables();
        _anchor = (segA, offA);
        _focus = (segB, offB);
        Apply();
        return BuildHtml();
    }
}
