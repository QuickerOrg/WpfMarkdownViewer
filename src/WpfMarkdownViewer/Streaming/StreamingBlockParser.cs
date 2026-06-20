using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Streaming;

/// <summary>
/// The smart streaming block-level state machine (see grilling decision "智能流式状态机" and ADR-0002).
/// Segments the accumulated source into the minimal Block set, maintaining a single Active Block and
/// finalizing Blocks at their boundaries. It is a best-effort preview that must Converge to Markdig on
/// finalize (verified by the Converge suite in phase C).
/// </summary>
/// <remarks>
/// Milestone 1 re-segments the whole source each tick for simplicity. Re-segmenting only the unsettled
/// tail (finalized Blocks are immutable, ADR-0002/0006) is a later performance optimization.
/// </remarks>
public sealed class StreamingBlockParser
{
    public Document Document { get; } = new();

    /// <summary>Re-derive the Document's Blocks from the full source. When <paramref name="streamComplete"/> is true, every Block is finalized.</summary>
    public void Reparse(string source, bool streamComplete)
    {
        var lines = SplitLines(source);
        Document.SetBlocks(Segment(source, lines, streamComplete));
    }

    internal readonly record struct SourceLine(int Start, string Text, bool HasNewline);

    internal static List<SourceLine> SplitLines(string s)
    {
        var result = new List<SourceLine>();
        int lineStart = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != '\n')
                continue;
            int textEnd = i > lineStart && s[i - 1] == '\r' ? i - 1 : i;
            result.Add(new SourceLine(lineStart, s[lineStart..textEnd], HasNewline: true));
            lineStart = i + 1;
        }
        if (lineStart < s.Length)
            result.Add(new SourceLine(lineStart, s[lineStart..], HasNewline: false));
        return result;
    }

    internal static List<MdBlock> Segment(string source, List<SourceLine> lines, bool streamComplete)
    {
        var blocks = new List<MdBlock>();
        int i = 0;
        while (i < lines.Count)
        {
            if (IsBlank(lines[i].Text))
            {
                i++;
                continue;
            }

            int startLine = i;
            string firstLine = lines[i].Text;
            MdBlock block;
            bool ownClosed;

            if (TryFence(firstLine, out char fenceChar, out int fenceLen, out string? lang))
            {
                int j = i + 1;
                bool fenceClosed = false;
                while (j < lines.Count)
                {
                    if (IsClosingFence(lines[j].Text, fenceChar, fenceLen))
                    {
                        fenceClosed = true;
                        j++;
                        break;
                    }
                    j++;
                }
                block = new CodeBlock { Language = lang, FenceClosed = fenceClosed };
                i = j;
                ownClosed = fenceClosed;
            }
            else if (HeadingLevel(firstLine) is int lvl and > 0)
            {
                block = new HeadingBlock { Level = lvl };
                i = startLine + 1;
                ownClosed = lines[startLine].HasNewline;
            }
            else if (IsQuote(firstLine))
            {
                int j = i;
                while (j < lines.Count && !IsBlank(lines[j].Text) && IsQuote(lines[j].Text))
                    j++;
                block = new QuoteBlock();
                i = j;
                ownClosed = false;
            }
            else if (IsListItem(firstLine))
            {
                int j = i;
                while (j < lines.Count && !IsBlank(lines[j].Text)
                       && (IsListItem(lines[j].Text) || IsIndentedContinuation(lines[j].Text)))
                    j++;
                block = new ListBlock { Ordered = IsOrdered(firstLine) };
                i = j;
                ownClosed = false;
            }
            else
            {
                int j = i;
                while (j < lines.Count && !IsBlank(lines[j].Text) && !IsStructural(lines[j].Text))
                    j++;
                block = new ParagraphBlock();
                i = j;
                ownClosed = false;
            }

            SetSpan(block, source, lines, startLine, i - 1);

            bool moreContentAfter = i < lines.Count;
            block.IsFinalized = streamComplete
                ? true
                : moreContentAfter
                    ? true
                    : block.Kind switch
                    {
                        BlockKind.Code => ((CodeBlock)block).FenceClosed,
                        BlockKind.Heading => ownClosed,
                        _ => false,
                    };

            blocks.Add(block);
        }
        return blocks;
    }

    private static void SetSpan(MdBlock block, string source, List<SourceLine> lines, int startLine, int endLine)
    {
        int start = lines[startLine].Start;
        int end = endLine + 1 < lines.Count ? lines[endLine + 1].Start : source.Length;
        block.SourceStart = start;
        block.RawText = source[start..end];
    }

    // --- line classification helpers ---

    private static bool IsBlank(string text) => text.AsSpan().Trim().IsEmpty;

    private static int LeadingSpaces(string text, int max)
    {
        int k = 0;
        while (k < max && k < text.Length && text[k] == ' ')
            k++;
        return k;
    }

    internal static int HeadingLevel(string text)
    {
        int k = LeadingSpaces(text, 3);
        int hashes = 0;
        while (k + hashes < text.Length && text[k + hashes] == '#')
            hashes++;
        if (hashes is >= 1 and <= 6 && (k + hashes == text.Length || text[k + hashes] == ' '))
            return hashes;
        return 0;
    }

    internal static bool TryFence(string text, out char fenceChar, out int fenceLen, out string? lang)
    {
        fenceChar = '\0';
        fenceLen = 0;
        lang = null;
        int k = LeadingSpaces(text, 3);
        if (k >= text.Length || (text[k] != '`' && text[k] != '~'))
            return false;
        char c = text[k];
        int len = 0;
        while (k + len < text.Length && text[k + len] == c)
            len++;
        if (len < 3)
            return false;
        fenceChar = c;
        fenceLen = len;
        string info = text[(k + len)..].Trim();
        lang = info.Length == 0 ? null : info;
        return true;
    }

    private static bool IsClosingFence(string text, char fenceChar, int openLen)
    {
        var t = text.AsSpan().Trim();
        if (t.Length < openLen)
            return false;
        foreach (char ch in t)
            if (ch != fenceChar)
                return false;
        return true;
    }

    private static bool IsQuote(string text)
    {
        int k = LeadingSpaces(text, 3);
        return k < text.Length && text[k] == '>';
    }

    internal static bool IsListItem(string text)
    {
        int k = LeadingSpaces(text, 3);
        if (k >= text.Length)
            return false;
        char c = text[k];
        if ((c == '-' || c == '*' || c == '+') && k + 1 < text.Length && text[k + 1] == ' ')
            return true;
        int d = k;
        while (d < text.Length && char.IsAsciiDigit(text[d]))
            d++;
        return d > k && d < text.Length && (text[d] == '.' || text[d] == ')')
               && d + 1 < text.Length && text[d + 1] == ' ';
    }

    private static bool IsOrdered(string text)
    {
        int k = LeadingSpaces(text, 3);
        return k < text.Length && char.IsAsciiDigit(text[k]);
    }

    private static bool IsIndentedContinuation(string text) =>
        text.Length > 0 && (text[0] == ' ' || text[0] == '\t');

    private static bool IsStructural(string text) =>
        HeadingLevel(text) > 0 || TryFence(text, out _, out _, out _) || IsQuote(text) || IsListItem(text);
}
