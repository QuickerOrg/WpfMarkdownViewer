using System.Collections.Concurrent;
using System.Text;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Controls;

/// <summary>Raised when the user activates a link. The component never navigates itself (security: AI-generated content).</summary>
public sealed class LinkClickedEventArgs : EventArgs
{
    public string Url { get; }

    public LinkClickedEventArgs(string url) => Url = url;
}

/// <summary>
/// The core single-Document Markdown renderer (see CONTEXT.md: "Document"). Read-only: it displays,
/// (later) selects and copies, but never accepts text input (ADR-0009).
/// </summary>
/// <remarks>
/// <see cref="AppendDelta"/> is safe to call from any thread; everything else is expected on the UI
/// thread. A background <see cref="DispatcherTimer"/> drains the incoming queue on an adaptive cadence
/// (ADR / "自适应离散三档") and re-derives the Document. Visual rendering of the Document arrives in phase D.
/// </remarks>
public class MarkdownDocumentView : Control
{
    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly StringBuilder _source = new();
    private readonly StreamingBlockParser _parser = new();
    private readonly AdaptiveThrottlePolicy _policy = new();
    private readonly DispatcherTimer _pump;

    private long _tokensSeen;
    private long _tokensAtLastTick;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private DateTime _lastInputUtc = DateTime.UtcNow;
    private bool _completed;
    private bool _dirty;

    public MarkdownDocumentView()
    {
        _pump = new DispatcherTimer(DispatcherPriority.Background) { Interval = _policy.MidInterval };
        _pump.Tick += OnPumpTick;
        _pump.Start();
    }

    /// <summary>The parsed Document. Exposed for tests and (later) the renderer.</summary>
    internal Document Document => _parser.Document;

    /// <summary>Raised when the user activates a link. The host decides whether/how to navigate.</summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    /// <summary>Raised on the UI thread after a flush changes the Document. The renderer (phase D) subscribes to this.</summary>
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
        _parser.Reparse(string.Empty, streamComplete: false);
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

    /// <summary>Drain the incoming queue into the source buffer and re-derive the Document. UI thread only.</summary>
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
        _parser.Reparse(_source.ToString(), _completed);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drive a single synchronous flush. Test/host hook so streaming can be advanced deterministically.</summary>
    internal void FlushForTest() => Flush();

    protected virtual void OnLinkClicked(string url) =>
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(url));
}
