using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;

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
/// Milestone 1 phase A establishes the public contract only. The streaming buffer, adaptive throttle,
/// parser, and rendering are wired in later phases. <see cref="AppendDelta"/> is safe to call from any
/// thread; everything else is expected on the UI thread.
/// </remarks>
public class MarkdownDocumentView : Control
{
    private readonly ConcurrentQueue<string> _incoming = new();

    /// <summary>Raised when the user activates a link. The host decides whether/how to navigate.</summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    /// <summary>Append a streamed token/delta. Thread-safe: may be called from any thread.</summary>
    public void AppendDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta))
            return;
        _incoming.Enqueue(delta);
        // Phase B: a UI-thread adaptive-throttle flush will drain this queue.
    }

    /// <summary>Signal that the stream is complete; finalizes the Active Block (Markdig becomes authoritative).</summary>
    public void Complete()
    {
        // Phase B/C.
    }

    /// <summary>Clear all state so the Document can be re-streamed (e.g. "regenerate response").</summary>
    public void Reset()
    {
        while (_incoming.TryDequeue(out _)) { }
        // Phase B: also clears the parsed Document and visuals.
    }

    /// <summary>Best-effort finalize a cancelled stream; stops sticky-follow and expects no more tokens.</summary>
    public void Abort()
    {
        // Phase B.
    }

    /// <summary>Render already-complete Markdown with no token stream (static path).</summary>
    public void SetMarkdown(string markdown)
    {
        Reset();
        AppendDelta(markdown);
        Complete();
    }

    protected virtual void OnLinkClicked(string url) =>
        LinkClicked?.Invoke(this, new LinkClickedEventArgs(url));
}
