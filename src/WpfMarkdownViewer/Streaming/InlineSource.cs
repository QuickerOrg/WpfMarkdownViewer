using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Streaming;

/// <summary>
/// Extracts the inline source text of a text-bearing Block (paragraph, heading): strips block-level
/// markup (heading <c>#</c>s) and normalizes soft-wrapped lines to spaces, ready for an inline projector.
/// </summary>
public static class InlineSource
{
    public static string Extract(MdBlock block) => block switch
    {
        HeadingBlock => StripHeading(block.RawText),
        _ => NormalizeSoftWraps(block.RawText),
    };

    private static string NormalizeSoftWraps(string raw) =>
        raw.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\n', ' ').Trim();

    private static string StripHeading(string raw)
    {
        string line = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        int nl = line.IndexOf('\n');
        if (nl >= 0)
            line = line[..nl];

        int k = 0;
        while (k < line.Length && line[k] == ' ')
            k++;
        while (k < line.Length && line[k] == '#')
            k++;
        while (k < line.Length && line[k] == ' ')
            k++;

        return line[k..].TrimEnd().TrimEnd('#').TrimEnd();
    }
}
