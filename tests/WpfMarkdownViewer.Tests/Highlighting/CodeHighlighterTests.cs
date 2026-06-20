using WpfMarkdownViewer.Highlighting;

namespace WpfMarkdownViewer.Tests.Highlighting;

public class CodeHighlighterTests
{
    private readonly CodeHighlighter _highlighter = new();

    [Fact]
    public void CSharp_ColorsTokens_AndSpansCoverTheLine()
    {
        var lines = _highlighter.Highlight("var x = 1;", "csharp");

        var line = Assert.Single(lines);
        Assert.Equal("var x = 1;", string.Concat(line.Select(s => s.Text)));
        Assert.Contains(line, s => s.ColorHex is not null); // at least one token got a theme color
    }

    [Fact]
    public void UnknownLanguage_FallsBackToPlain()
    {
        var lines = _highlighter.Highlight("hello\nworld", "no-such-lang");

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Null(Assert.Single(l).ColorHex));
    }

    [Fact]
    public void MultiLine_PreservesLineCount()
    {
        var lines = _highlighter.Highlight("a\nb\nc", "csharp");
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void AliasedLanguage_IsResolved()
    {
        // "cs" should alias to csharp and still color tokens.
        var lines = _highlighter.Highlight("int n = 0;", "cs");
        Assert.Contains(Assert.Single(lines), s => s.ColorHex is not null);
    }
}
