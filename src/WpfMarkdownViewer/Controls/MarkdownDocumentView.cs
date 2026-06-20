using System.Collections.Concurrent;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
public class MarkdownDocumentView : Panel
{
    private const double Pad = 16;
    private const double BlockSpacing = 10;

    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly StringBuilder _source = new();
    private readonly StreamingBlockParser _parser = new();
    private readonly AdaptiveThrottlePolicy _policy = new();
    private readonly TextRenderTheme _theme = new();
    private readonly DispatcherTimer _pump;

    private long _tokensSeen;
    private long _tokensAtLastTick;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private DateTime _lastInputUtc = DateTime.UtcNow;
    private bool _completed;
    private bool _dirty;

    /// <summary>How many leading child visuals correspond to finalized, immutable Blocks (never rebuilt).</summary>
    private int _stableCount;

    public MarkdownDocumentView()
    {
        _pump = new DispatcherTimer(DispatcherPriority.Background) { Interval = _policy.MidInterval };
        _pump.Tick += OnPumpTick;
        _pump.Start();
    }

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

    /// <summary>Reconcile child visuals with the Document: keep finalized-Block visuals, rebuild the tail.</summary>
    private void Render()
    {
        var blocks = Document.Blocks;
        if (_stableCount > blocks.Count)
            _stableCount = 0;

        while (InternalChildren.Count > _stableCount)
            InternalChildren.RemoveAt(InternalChildren.Count - 1);

        for (int i = _stableCount; i < blocks.Count; i++)
            InternalChildren.Add(BlockViewFactory.Create(blocks[i], _theme));

        int stable = 0;
        while (stable < blocks.Count && blocks[stable].IsFinalized)
            stable++;
        _stableCount = stable;

        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double availW = availableSize.Width;
        double contentW = Math.Max(1, (double.IsInfinity(availW) ? 800 : availW) - 2 * Pad);

        double y = 0, maxChildW = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(contentW, double.PositiveInfinity));
            y += child.DesiredSize.Height + BlockSpacing;
            maxChildW = Math.Max(maxChildW, child.DesiredSize.Width);
        }
        double contentH = y > 0 ? y - BlockSpacing : 0;
        double width = double.IsInfinity(availW) ? maxChildW + 2 * Pad : availW;
        return new Size(width, contentH + 2 * Pad);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double w = Math.Max(0, finalSize.Width - 2 * Pad);
        double y = Pad;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(Pad, y, w, child.DesiredSize.Height));
            y += child.DesiredSize.Height + BlockSpacing;
        }
        return finalSize;
    }

    /// <summary>Drive a single synchronous flush. Test/host hook so streaming can be advanced deterministically.</summary>
    internal void FlushForTest() => Flush();

    protected virtual void OnLinkClicked(string url) =>
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(url));
}
