using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

public class MathDelimitersTests
{
    [Fact]
    public void InlineParens_BecomeDollar()
    {
        Assert.Equal("value $x^2$ here", MathDelimiters.Normalize(@"value \(x^2\) here"));
    }

    [Fact]
    public void DisplayBrackets_BecomeDoubleDollar()
    {
        Assert.Equal("$$x^2$$", MathDelimiters.Normalize(@"\[x^2\]"));
    }

    [Fact]
    public void DisplayMath_MaySpanLines()
    {
        Assert.Equal("$$\na+b\n$$", MathDelimiters.Normalize("\\[\na+b\n\\]"));
    }

    [Fact]
    public void UnclosedOpener_StaysLiteral()
    {
        Assert.Equal(@"a \(x and more", MathDelimiters.Normalize(@"a \(x and more"));
    }

    [Fact]
    public void FencedCode_IsNotTouched()
    {
        string input = "see $$ below\n```\nregex = \\(group\\)\n```\nand \\(y\\)";
        string expected = "see $$ below\n```\nregex = \\(group\\)\n```\nand $y$";
        Assert.Equal(expected, MathDelimiters.Normalize(input));
    }

    [Fact]
    public void NoBackslash_IsUnchanged()
    {
        const string s = "plain $a$ and $$b$$ text";
        Assert.Same(s, MathDelimiters.Normalize(s));
    }
}
