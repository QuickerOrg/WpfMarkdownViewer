using System.Text;
using WpfMarkdownViewer.Model;

namespace WpfMarkdownViewer.Streaming;

/// <summary>
/// Projects a Block's inline source into a flat list of <see cref="InlineRun"/>s in Visible Space
/// (ADR-0007). This is the streaming, best-effort projector: it is always optimistic — an opened but
/// not-yet-closed marker (e.g. <c>**bold</c>) hides its marker and applies its style to the remainder,
/// matching the ChatGPT "边吐边变粗" feel. On finalize, <see cref="Parsing.MarkdigInlineProjector"/>
/// is authoritative and the two must Converge for closed input.
/// </summary>
public static class InlineProjector
{
    public static InlineProjection Project(string source)
    {
        var visible = new StringBuilder();
        var runs = new List<InlineRun>();
        var pending = new StringBuilder();
        var style = InlineStyle.None;

        void Flush()
        {
            if (pending.Length > 0)
            {
                runs.Add(new InlineRun(visible.Length - pending.Length, pending.ToString(), style));
                pending.Clear();
            }
        }

        void AppendChar(char c)
        {
            pending.Append(c);
            visible.Append(c);
        }

        int i = 0;
        int n = source.Length;
        while (i < n)
        {
            char c = source[i];

            if (c is '*' or '_' && Matches(source, i, c, 3))
            {
                Flush();
                style ^= InlineStyle.Bold | InlineStyle.Italic;
                i += 3;
            }
            else if (c is '*' or '_' && Matches(source, i, c, 2))
            {
                Flush();
                style ^= InlineStyle.Bold;
                i += 2;
            }
            else if (c is '*' or '_')
            {
                Flush();
                style ^= InlineStyle.Italic;
                i += 1;
            }
            else if (c == '~' && Matches(source, i, '~', 2))
            {
                Flush();
                style ^= InlineStyle.Strikethrough;
                i += 2;
            }
            else if (c == '=' && Matches(source, i, '=', 2))
            {
                Flush();
                style ^= InlineStyle.Highlight;
                i += 2;
            }
            else if (c == '+' && Matches(source, i, '+', 2))
            {
                Flush();
                style ^= InlineStyle.Underline;
                i += 2;
            }
            else if (c == '~')
            {
                Flush();
                style ^= InlineStyle.Subscript;
                i += 1;
            }
            else if (c == '^')
            {
                Flush();
                style ^= InlineStyle.Superscript;
                i += 1;
            }
            else if (c == '`')
            {
                int close = source.IndexOf('`', i + 1);
                if (close > i)
                {
                    Flush();
                    string code = source[(i + 1)..close];
                    runs.Add(new InlineRun(visible.Length, code, style | InlineStyle.Code));
                    visible.Append(code);
                    i = close + 1;
                }
                else
                {
                    AppendChar(c);
                    i++;
                }
            }
            else if (c == '[' && TryReadLink(source, i, out string text, out string url, out int next))
            {
                Flush();
                var inner = Project(text);
                int baseOffset = visible.Length;
                foreach (var run in inner.Runs)
                    runs.Add(run with { VisibleStart = baseOffset + run.VisibleStart, Style = run.Style | style, LinkTarget = url });
                visible.Append(inner.VisibleText);
                i = next;
            }
            else
            {
                AppendChar(c);
                i++;
            }
        }

        Flush();
        return new InlineProjection(visible.ToString(), runs);
    }

    private static bool Matches(string s, int i, char c, int count)
    {
        if (i + count > s.Length)
            return false;
        for (int k = 0; k < count; k++)
            if (s[i + k] != c)
                return false;
        // For count<3 ensure it is not the start of a longer run that a higher tier should claim.
        return true;
    }

    private static bool TryReadLink(string s, int i, out string text, out string url, out int next)
    {
        text = string.Empty;
        url = string.Empty;
        next = i;
        int close = s.IndexOf(']', i + 1);
        if (close < 0 || close + 1 >= s.Length || s[close + 1] != '(')
            return false;
        int paren = s.IndexOf(')', close + 2);
        if (paren < 0)
            return false;
        text = s[(i + 1)..close];
        url = s[(close + 2)..paren];
        next = paren + 1;
        return true;
    }
}
