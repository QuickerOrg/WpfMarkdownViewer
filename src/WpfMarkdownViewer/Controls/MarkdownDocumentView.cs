using System.Collections.Concurrent;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Rendering;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Controls;

/// <summary>Raised when the user activates a link. The component never navigates itself (security: AI-generated content).</summary>
public sealed class LinkClickedEventArgs : EventArgs
{
    public string Url { get; }

    public LinkClickedEventArgs(string url) => Url = url;
}

/// <summary>
/// The core single-Document Markdown renderer (CONTEXT.md: "Document"). Read-only (ADR-0009); self-drawn
/// blocks (ADR-0005). It is non-scrolling content that stacks Block visuals top-to-bottom; the Scroll
/// Host (phase E) wraps it. Finalized Block visuals are immutable and reused; only the Active Block's
/// visual is rebuilt per tick.
/// </summary>
/// <remarks>
/// <see cref="AppendDelta"/> is safe to call from any thread; everything else is expected on the UI
/// thread. A background <see cref="DispatcherTimer"/> drains the queue on an adaptive cadence
/// ("自适应离散三档") and re-derives + re-renders the Document.
/// </remarks>
public class MarkdownDocumentView : Panel, IVirtualizingContent
{
    private const double VirtualizationBuffer = 500;
    private const double EstimatedBlockHeight = 40;

    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly StringBuilder _source = new();
    private readonly StreamingBlockParser _parser = new();
    private readonly AdaptiveThrottlePolicy _policy = new();
    private readonly DispatcherTimer _pump;

    private MarkdownStyle _theme = MarkdownStyle.Light;

    private long _tokensSeen;
    private long _tokensAtLastTick;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private DateTime _lastInputUtc = DateTime.UtcNow;
    private bool _completed;
    private bool _dirty;

    /// <summary>How many leading slots correspond to finalized, immutable Blocks (never rebuilt).</summary>
    private int _stableCount;

    private readonly List<BlockSlot> _slots = new();
    private double _viewportTop;
    private double _viewportHeight; // 0 ⇒ no Scroll Host connected ⇒ realize all (no virtualization)

    private sealed class BlockSlot
    {
        public required MdBlock Block { get; init; }
        public FrameworkElement? View { get; set; }
        public double Height { get; set; }
        public double Y { get; set; }
        public bool Finalized { get; set; }
    }

    /// <summary>When false, all Blocks stay realized (no virtualization) — e.g. for printing/snapshots. Default true.</summary>
    public bool VirtualizationEnabled { get; set; } = true;

    /// <summary>When true, the measured width shrinks to the content (capped at the available width) instead of filling it — used for user chat bubbles (M3). Default false.</summary>
    public bool ShrinkToContentWidth { get; set; }

    /// <summary>Base path or URI for resolving relative image URLs (M2-4). Null ⇒ only absolute URLs load.</summary>
    public string? ImageBasePath { get; set; }

    void IVirtualizingContent.SetViewport(double top, double height)
    {
        _viewportTop = top;
        _viewportHeight = height;
        InvalidateMeasure();
    }

    internal int SlotCountForTest => _slots.Count;
    internal int RealizedCountForTest => _slots.Count(s => s.View is not null);

    public MarkdownDocumentView()
    {
        Background = _theme.Background;
        Focusable = true;
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, _) => CopySelection()));
        _pump = new DispatcherTimer(DispatcherPriority.Background) { Interval = _policy.MidInterval };
        _pump.Tick += OnPumpTick;
        _pump.Start();
    }

    /// <summary>The appearance configuration (fonts, sizes, margins, colors). Settable in code or XAML; runtime-swappable (M2-1).</summary>
    public static readonly DependencyProperty MarkdownStyleProperty = DependencyProperty.Register(
        nameof(MarkdownStyle), typeof(MarkdownStyle), typeof(MarkdownDocumentView),
        new PropertyMetadata(MarkdownStyle.Light, OnMarkdownStyleChanged));

    public MarkdownStyle MarkdownStyle
    {
        get => (MarkdownStyle)GetValue(MarkdownStyleProperty);
        set => SetValue(MarkdownStyleProperty, value);
    }

    private static void OnMarkdownStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (MarkdownDocumentView)d;
        view._theme = (MarkdownStyle)e.NewValue;
        view.Background = view._theme.Background;
        view._stableCount = 0;
        view.Render();
    }

    /// <summary>Convenience: switch the appearance at runtime (routes through the MarkdownStyle property).</summary>
    public void ApplyTheme(MarkdownStyle theme) => MarkdownStyle = theme ?? throw new ArgumentNullException(nameof(theme));

    /// <summary>The parsed Document. Exposed for tests and tooling.</summary>
    internal Document Document => _parser.Document;

    /// <summary>Raised when the user activates a link. The host decides whether/how to navigate.</summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    /// <summary>Raised on the UI thread after a flush changes the Document.</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>Append a streamed token/delta. Thread-safe: may be called from any thread.</summary>
    public void AppendDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta))
            return;
        _incoming.Enqueue(delta);
        Interlocked.Add(ref _tokensSeen, delta.Length);
        _lastInputUtc = DateTime.UtcNow;
    }

    /// <summary>Signal that the stream is complete; finalizes the Active Block (Markdig becomes authoritative).</summary>
    public void Complete()
    {
        _completed = true;
        _dirty = true;
        Flush();
    }

    /// <summary>Clear all state so the Document can be re-streamed (e.g. "regenerate response").</summary>
    public void Reset()
    {
        while (_incoming.TryDequeue(out _)) { }
        _source.Clear();
        _completed = false;
        Interlocked.Exchange(ref _tokensSeen, 0);
        _tokensAtLastTick = 0;
        _stableCount = 0;
        _parser.Reparse(string.Empty, streamComplete: false);
        Render();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Best-effort finalize a cancelled stream; stops sticky-follow and expects no more tokens.</summary>
    public void Abort()
    {
        _completed = true;
        _dirty = true;
        Flush();
        // Phase E: stop sticky-bottom following.
    }

    /// <summary>Render already-complete Markdown with no token stream (static path).</summary>
    public void SetMarkdown(string markdown)
    {
        Reset();
        AppendDelta(markdown);
        Complete();
    }

    private void OnPumpTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        bool idle = _policy.IsIdle(now - _lastInputUtc);

        Flush();

        double elapsedSec = Math.Max((now - _lastTickUtc).TotalSeconds, 1e-3);
        long current = Interlocked.Read(ref _tokensSeen);
        double rate = (current - _tokensAtLastTick) / elapsedSec;
        _tokensAtLastTick = current;
        _lastTickUtc = now;

        _pump.Interval = idle ? _policy.MidInterval : _policy.NextInterval(rate);
    }

    /// <summary>Drain the queue into the source buffer, re-derive the Document, and re-render. UI thread only.</summary>
    private void Flush()
    {
        bool changed = _dirty;
        _dirty = false;
        while (_incoming.TryDequeue(out var delta))
        {
            _source.Append(delta);
            changed = true;
        }
        if (!changed)
            return;

        // Streaming → best-effort preview; on completion → Markdig is authoritative (ADR-0002).
        if (_completed)
            _parser.FinalizeFromMarkdig(_source.ToString());
        else
            _parser.Reparse(_source.ToString(), streamComplete: false);

        Render();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reconcile slots with the Document: keep finalized-Block slots, rebuild the tail. Realization is decided in measure.</summary>
    private void Render()
    {
        var blocks = Document.Blocks;
        if (_stableCount > blocks.Count || _stableCount > _slots.Count)
        {
            InternalChildren.Clear();
            _slots.Clear();
            _stableCount = 0;
        }

        for (int i = _slots.Count - 1; i >= _stableCount; i--)
        {
            if (_slots[i].View is { } v)
                InternalChildren.Remove(v);
            _slots.RemoveAt(i);
        }

        for (int i = _stableCount; i < blocks.Count; i++)
        {
            var view = BlockViewFactory.Create(blocks[i], _theme, RaiseLink);
            InternalChildren.Add(view);
            _slots.Add(new BlockSlot { Block = blocks[i], View = view });
        }

        int stable = 0;
        while (stable < blocks.Count && blocks[stable].IsFinalized)
            stable++;
        _stableCount = stable;
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].Finalized = i < _stableCount;

        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var pad = _theme.ContentPadding;
        double spacing = _theme.BlockSpacing;
        double availW = availableSize.Width;
        double contentW = Math.Max(1, (double.IsInfinity(availW) ? 800 : availW) - pad.Left - pad.Right);

        bool virtualize = VirtualizationEnabled && _viewportHeight > 0;
        double bufTop = _viewportTop - VirtualizationBuffer;
        double bufBottom = _viewportTop + _viewportHeight + VirtualizationBuffer;

        // Pass 1: provisional Y from cached/estimated heights, then realize/devirtualize per viewport.
        double y = pad.Top;
        foreach (var slot in _slots)
        {
            slot.Y = y;
            y += (slot.Height > 0 ? slot.Height : EstimatedBlockHeight) + spacing;
        }
        foreach (var slot in _slots)
        {
            bool onScreen = !virtualize || !slot.Finalized || slot.Height <= 0
                || (slot.Y <= bufBottom && slot.Y + Math.Max(slot.Height, EstimatedBlockHeight) >= bufTop);
            if (onScreen && slot.View is null)
            {
                slot.View = BlockViewFactory.Create(slot.Block, _theme, RaiseLink, ImageBasePath);
                InternalChildren.Add(slot.View);
            }
            else if (!onScreen && slot.View is { } v && slot.Finalized)
            {
                InternalChildren.Remove(v);
                slot.View = null;
            }
        }

        // Pass 2: measure realized slots, cache their heights, compute final layout.
        y = pad.Top;
        double maxW = 0;
        foreach (var slot in _slots)
        {
            if (slot.View is { } v)
            {
                v.Measure(new Size(contentW, double.PositiveInfinity));
                slot.Height = v.DesiredSize.Height;
                maxW = Math.Max(maxW, v.DesiredSize.Width);
            }
            else if (slot.Height <= 0)
            {
                slot.Height = EstimatedBlockHeight;
            }
            slot.Y = y;
            y += slot.Height + spacing;
        }

        double contentBottom = _slots.Count > 0 ? y - spacing : pad.Top;
        double natural = maxW + pad.Left + pad.Right;
        double width = double.IsInfinity(availW) ? natural
            : ShrinkToContentWidth ? Math.Min(availW, natural)
            : availW;
        return new Size(width, contentBottom + pad.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pad = _theme.ContentPadding;
        double w = Math.Max(0, finalSize.Width - pad.Left - pad.Right);
        foreach (var slot in _slots)
            slot.View?.Arrange(new Rect(pad.Left, slot.Y, w, slot.View.DesiredSize.Height));
        return finalSize;
    }

    /// <summary>Drive a single synchronous flush. Test/host hook so streaming can be advanced deterministically.</summary>
    internal void FlushForTest() => Flush();

    /// <summary>The whole Document as accessible read-only plain text (ADR-0009). Also the basis for plain-text copy.</summary>
    internal string GetAccessibleText() => DocumentTextSerializer.ToPlainText(Document);

    protected override AutomationPeer OnCreateAutomationPeer() => new MarkdownDocumentAutomationPeer(this);

    private void RaiseLink(string url) => OnLinkClicked(url);

    // --- Selection (ADR-0008): document-level drag-select over ParagraphView text leaves ---

    private readonly List<ISelectableText> _selectables = new();
    private (int Segment, int Offset) _anchor;
    private (int Segment, int Offset) _focus;
    private bool _selecting;
    private bool _hasSelection;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.Handled) // a child handled it (e.g. a link or the code copy button)
            return;

        RebuildSelectables();
        if (_selectables.Count == 0)
            return;

        Focus();
        _anchor = _focus = Locate(e.GetPosition(this));
        _selecting = true;
        ClearSelection();
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_selecting)
            return;
        _focus = Locate(e.GetPosition(this));
        ApplySelection();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_selecting)
            return;
        _selecting = false;
        ReleaseMouseCapture();
    }

    private void RebuildSelectables()
    {
        _selectables.Clear();
        CollectSelectables(this);
    }

    private void CollectSelectables(DependencyObject node)
    {
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is ISelectableText selectable)
                _selectables.Add(selectable);
            else
                CollectSelectables(child);
        }
    }

    private (int Segment, int Offset) Locate(Point p)
    {
        for (int i = 0; i < _selectables.Count; i++)
        {
            var fe = (FrameworkElement)_selectables[i];
            Point topLeft = fe.TransformToAncestor(this).Transform(new Point(0, 0));
            if (p.Y <= topLeft.Y + fe.RenderSize.Height)
            {
                double localY = Math.Clamp(p.Y - topLeft.Y, 0, Math.Max(0, fe.RenderSize.Height - 1));
                return (i, _selectables[i].OffsetAtPoint(new Point(p.X - topLeft.X, localY)));
            }
        }
        int last = _selectables.Count - 1;
        return (last, _selectables[last].SelectableText.Length);
    }

    private (int Lo, int LoOff, int Hi, int HiOff) OrderedSelection()
    {
        bool anchorFirst = _anchor.Segment < _focus.Segment
            || (_anchor.Segment == _focus.Segment && _anchor.Offset <= _focus.Offset);
        return anchorFirst
            ? (_anchor.Segment, _anchor.Offset, _focus.Segment, _focus.Offset)
            : (_focus.Segment, _focus.Offset, _anchor.Segment, _anchor.Offset);
    }

    private void ApplySelection()
    {
        var (lo, loOff, hi, hiOff) = OrderedSelection();
        _hasSelection = lo != hi || loOff != hiOff;
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

    private void ClearSelection()
    {
        _hasSelection = false;
        foreach (var s in _selectables)
            s.SetSelectedRange(0, 0);
    }

    /// <summary>Select all text and draw the highlight.</summary>
    public void SelectAll()
    {
        RebuildSelectables();
        if (_selectables.Count == 0)
            return;
        _anchor = (0, 0);
        _focus = (_selectables.Count - 1, _selectables[^1].SelectableText.Length);
        ApplySelection();
    }

    /// <summary>Copy the current selection as plain-text Markdown only (the text IS the Markdown source, so it round-trips into any editor).</summary>
    public void CopySelection()
    {
        if (!_hasSelection || _selectables.Count == 0)
            return;
        string markdown = BuildSelectedMarkdown();
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

    private IEnumerable<(int Index, IReadOnlyList<InlineRun> Runs)> SelectedRunsPerSegment()
    {
        var (lo, loOff, hi, hiOff) = OrderedSelection();
        for (int i = lo; i <= hi; i++)
        {
            int s = i == lo ? loOff : 0;
            int e = i == hi ? hiOff : _selectables[i].SelectableText.Length;
            if (e > s)
                yield return (i, _selectables[i].SelectedRuns(s, e));
        }
    }

    private string BuildSelectedMarkdown()
    {
        var (lo, loOff, hi, hiOff) = OrderedSelection();
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
                piece = prefix + Streaming.RunSerializer.ToMarkdown(_selectables[i].SelectedRuns(s, e));
            }

            if (sb.Length > 0)
                sb.Append(block || prevBlock ? "\n\n" : "\n"); // blank line around block elements
            sb.Append(piece);
            prevBlock = block;
        }
        return sb.ToString();
    }

    // Retained for the HTML test hook (ADR-0008 keeps the run→HTML mapping); no longer placed on the clipboard.
    private string BuildSelectedHtml() =>
        string.Join("<br>", SelectedRunsPerSegment().Select(x => Streaming.RunSerializer.ToHtml(x.Runs)));

    private string BuildSelectedText()
    {
        if (!_hasSelection || _selectables.Count == 0)
            return string.Empty;

        var (lo, loOff, hi, hiOff) = OrderedSelection();
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

    internal IReadOnlyList<string> SelectableTextsForTest()
    {
        RebuildSelectables();
        return _selectables.Select(s => s.SelectableText).ToList();
    }

    internal string SelectAndGetTextForTest(int segA, int offA, int segB, int offB)
    {
        RebuildSelectables();
        _anchor = (segA, offA);
        _focus = (segB, offB);
        ApplySelection();
        return BuildSelectedText();
    }

    internal string SelectAndGetMarkdownForTest(int segA, int offA, int segB, int offB)
    {
        RebuildSelectables();
        _anchor = (segA, offA);
        _focus = (segB, offB);
        ApplySelection();
        return BuildSelectedMarkdown();
    }

    internal string SelectAndGetHtmlForTest(int segA, int offA, int segB, int offB)
    {
        RebuildSelectables();
        _anchor = (segA, offA);
        _focus = (segB, offB);
        ApplySelection();
        return BuildSelectedHtml();
    }

    protected virtual void OnLinkClicked(string url) =>
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(url));
}
