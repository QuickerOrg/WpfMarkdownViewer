using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Controls;

/// <summary>
/// The optional, thin Conversation Shell (CONTEXT.md: "Conversation Shell"; ADR-0004). It composes many
/// Documents into a transcript by stacking one <see cref="MarkdownDocumentView"/> per message and owns the
/// message-level concerns the core deliberately leaves out: per-role chrome (ChatGPT-style — user turns are
/// right-aligned bubbles, assistant turns full-width) and, together with <see cref="MarkdownScrollHost"/>,
/// autoscroll across the whole transcript. The core still renders exactly one Document per message.
/// </summary>
/// <remarks>
/// Non-scrolling content: like <see cref="MarkdownDocumentView"/> it is meant to live inside a
/// <see cref="MarkdownScrollHost"/>. The streaming methods (<see cref="StartMessage"/>,
/// <see cref="AppendDelta"/>, <see cref="CompleteMessage"/>) are expected on the UI thread; each message's
/// own renderer handles the adaptive flush cadence. Block-level virtualization (ADR-0006) keeps working
/// inside each message because the shell forwards the host viewport down to every realized message view.
/// </remarks>
[System.Windows.Markup.ContentProperty(nameof(MessagesHost))]
public class ConversationView : Panel, IVirtualizingContent
{
    private const double UserBubbleMaxWidthFraction = 0.82;
    private const double UserBubbleRightMargin = 16;

    private sealed class MessageSlot
    {
        public required ChatRole Role { get; init; }
        public readonly StringBuilder Markdown = new();
        public FrameworkElement? Element { get; set; }   // realized container: the bubble (user) or the view itself (assistant)
        public MarkdownDocumentView? View { get; set; }  // the inner renderer
        public double Height { get; set; }
        public double Y { get; set; }
        public bool Finalized { get; set; }
        public bool IsActive { get; set; }               // the trailing message currently receiving tokens
    }

    private readonly List<MessageSlot> _slots = new();
    private MarkdownStyle _style = MarkdownStyle.Light;
    private double _messageSpacing = 22;

    private double _viewportTop;
    private double _viewportHeight; // 0 ⇒ no Scroll Host connected

    public ConversationView()
    {
        Background = _style.Background;
    }

    /// <summary>XAML content sink placeholder so the control can be declared with no children. Not used in code.</summary>
    public UIElementCollection MessagesHost => InternalChildren;

    /// <summary>The appearance applied to every message (assistant full-width, user bubble derives from it). Runtime-swappable.</summary>
    public MarkdownStyle MarkdownStyle
    {
        get => _style;
        set => ApplyTheme(value);
    }

    /// <summary>When false, every message realizes all its Blocks (e.g. for printing/snapshots). Default true.</summary>
    public bool VirtualizationEnabled { get; set; } = true;

    /// <summary>Number of messages in the transcript.</summary>
    public int MessageCount => _slots.Count;

    /// <summary>Raised when the user activates a link in any message. The host decides whether/how to navigate.</summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    /// <summary>Raised after a streaming message is completed (<see cref="CompleteMessage"/>).</summary>
    public event EventHandler? MessageCompleted;

    // --- Streaming API (UI thread) ---

    /// <summary>Begin a new trailing message; finalizes the previous active one. Tokens then flow via <see cref="AppendDelta"/>.</summary>
    public void StartMessage(ChatRole role)
    {
        CompleteMessage();
        var slot = new MessageSlot { Role = role, IsActive = true };
        Realize(slot);              // the active message is always realized (never virtualized), like the Active Block
        _slots.Add(slot);
        InvalidateMeasure();
    }

    /// <summary>Append a streamed token/delta to the active message.</summary>
    public void AppendDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta))
            return;
        var active = ActiveSlot();
        if (active is null)
        {
            StartMessage(ChatRole.Assistant); // tolerate AppendDelta before StartMessage
            active = ActiveSlot()!;
        }
        active.Markdown.Append(delta);
        active.View!.AppendDelta(delta);
    }

    /// <summary>Finalize the active message (Markdig becomes authoritative for it). No-op if none is active.</summary>
    public void CompleteMessage()
    {
        var active = ActiveSlot();
        if (active is null)
            return;
        active.View!.Complete();
        active.Finalized = true;
        active.IsActive = false;
        MessageCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Add an already-complete message with no token stream (static path).</summary>
    public void AddMessage(ChatRole role, string markdown)
    {
        CompleteMessage();
        var slot = new MessageSlot { Role = role, Finalized = true };
        slot.Markdown.Append(markdown);
        Realize(slot);
        slot.View!.SetMarkdown(markdown);
        _slots.Add(slot);
        InvalidateMeasure();
    }

    /// <summary>Remove every message and reset the transcript.</summary>
    public void Clear()
    {
        InternalChildren.Clear();
        _slots.Clear();
        InvalidateMeasure();
    }

    /// <summary>Switch the appearance at runtime; rebuilds message chrome to match the new theme.</summary>
    public void ApplyTheme(MarkdownStyle theme)
    {
        _style = theme ?? throw new ArgumentNullException(nameof(theme));
        Background = _style.Background;
        foreach (var slot in _slots)
        {
            if (slot.IsActive && slot.View is { } live)
            {
                // Don't rebuild the streaming view (SetMarkdown would finalize it) — restyle it in place.
                live.ApplyTheme(StyleForRole(slot.Role));
                if (slot.Element is Border bubble)
                    bubble.Background = _style.UserBubbleBackground;
            }
            else if (slot.Element is not null)
            {
                Devirtualize(slot);
                Realize(slot);
                if (slot.Finalized)
                    slot.View!.SetMarkdown(slot.Markdown.ToString());
            }
        }
        InvalidateMeasure();
    }

    private MessageSlot? ActiveSlot() => _slots.Count > 0 && _slots[^1].IsActive ? _slots[^1] : null;

    // --- Realization / chrome ---

    private void Realize(MessageSlot slot)
    {
        var view = new MarkdownDocumentView
        {
            MarkdownStyle = StyleForRole(slot.Role),
            VirtualizationEnabled = VirtualizationEnabled,
        };
        view.LinkClicked += OnChildLinkClicked;
        slot.View = view;

        FrameworkElement element = view;
        if (slot.Role == ChatRole.User)
        {
            element = new Border
            {
                Background = _style.UserBubbleBackground,
                CornerRadius = new CornerRadius(14),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, UserBubbleRightMargin, 0),
                Child = view,
            };
        }

        slot.Element = element;
        InternalChildren.Add(element);
    }

    private void Devirtualize(MessageSlot slot)
    {
        if (slot.Element is not null)
            InternalChildren.Remove(slot.Element);
        if (slot.View is not null)
            slot.View.LinkClicked -= OnChildLinkClicked;
        slot.Element = null;
        slot.View = null;
    }

    // User bubble: the Border supplies the inset and background, so the inner view drops its padding and goes transparent.
    private MarkdownStyle StyleForRole(ChatRole role) => role == ChatRole.User
        ? _style with { ContentPadding = new Thickness(16, 10, 16, 10), Background = Brushes.Transparent }
        : _style;

    private void OnChildLinkClicked(object? sender, LinkClickedEventArgs e) => LinkClicked?.Invoke(this, e);

    void IVirtualizingContent.SetViewport(double top, double height)
    {
        _viewportTop = top;
        _viewportHeight = height;
        InvalidateMeasure();
    }

    // --- Layout ---

    protected override Size MeasureOverride(Size availableSize)
    {
        double availW = availableSize.Width;
        double contentW = Math.Max(1, double.IsInfinity(availW) ? 800 : availW);

        double y = 0;
        double maxW = 0;
        foreach (var slot in _slots)
        {
            slot.Y = y;

            if (slot.Element is Border bubble)
                bubble.MaxWidth = contentW * UserBubbleMaxWidthFraction;

            // Two-level virtualization (ADR-0006): forward the host viewport, translated into the message's
            // own coordinates, so the message renders only its on-screen Blocks.
            if (slot.View is IVirtualizingContent vc && _viewportHeight > 0)
                vc.SetViewport(_viewportTop - y, _viewportHeight);

            if (slot.Element is { } el)
            {
                el.Measure(new Size(contentW, double.PositiveInfinity));
                slot.Height = el.DesiredSize.Height;
                maxW = Math.Max(maxW, el.DesiredSize.Width);
            }

            y += slot.Height + _messageSpacing;
        }

        double height = _slots.Count > 0 ? y - _messageSpacing : 0;
        double width = double.IsInfinity(availW) ? maxW : availW;
        return new Size(width, Math.Max(0, height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var slot in _slots)
            // Arrange across the full width; the user bubble (HorizontalAlignment.Right) positions itself.
            slot.Element?.Arrange(new Rect(0, slot.Y, finalSize.Width, slot.Height));
        return finalSize;
    }

    // --- Test hooks ---

    internal int RealizedCountForTest => _slots.Count(s => s.View is not null);

    internal void FlushActiveForTest() => ActiveSlot()?.View?.FlushForTest();

    internal string MessageTextForTest(int index) => _slots[index].View?.GetAccessibleText() ?? string.Empty;

    internal string AccessibleTextForTest()
    {
        var sb = new StringBuilder();
        foreach (var slot in _slots)
        {
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(slot.Role).Append(": ").Append(slot.Markdown.ToString().TrimEnd());
        }
        return sb.ToString();
    }
}
