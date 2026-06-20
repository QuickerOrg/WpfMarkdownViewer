using System.Windows;

namespace WpfMarkdownViewer.Rendering;

/// <summary>
/// A self-drawn text leaf that participates in document-level selection (ADR-0008). The selection
/// controller treats the visual tree's selectable leaves, in document order, as one flat text space;
/// each leaf hit-tests points to offsets and draws its own highlight for a selected sub-range.
/// </summary>
internal interface ISelectableText
{
    /// <summary>The visible text of this segment (Visible Space).</summary>
    string SelectableText { get; }

    /// <summary>Map a point (in this element's coordinates) to a visible-text offset.</summary>
    int OffsetAtPoint(Point point);

    /// <summary>Highlight the visible range [start, end); pass an empty range to clear.</summary>
    void SetSelectedRange(int start, int end);
}
