namespace WpfMarkdownViewer.Controls;

/// <summary>Who authored a message in the Conversation Shell (ADR-0004 / M3).</summary>
public enum ChatRole
{
    /// <summary>A user turn. Rendered ChatGPT-style: a right-aligned, max-width bubble.</summary>
    User,

    /// <summary>An assistant turn. Rendered full-width with no bubble (the streaming target).</summary>
    Assistant,
}
