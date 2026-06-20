namespace WpfMarkdownViewer.Controls;

/// <summary>
/// Non-scrolling content that can virtualize against the Scroll Host's viewport (ADR-0006). The Scroll
/// Host pushes the current viewport (top offset + height, in content coordinates) on scroll/resize so the
/// content can realize only on-screen Blocks and drop off-screen finalized ones.
/// </summary>
internal interface IVirtualizingContent
{
    void SetViewport(double top, double height);
}
