using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

public class RunSerializerTests
{
    [Fact]
    public void ToMarkdown_WrapsPerRunStyles()
    {
        var runs = new[]
        {
            new InlineRun(0, "bold", InlineStyle.Bold),
            new InlineRun(4, " ", InlineStyle.None),
            new InlineRun(5, "code", InlineStyle.Code),
            new InlineRun(9, " ", InlineStyle.None),
            new InlineRun(10, "link", InlineStyle.None, "http://x"),
        };

        Assert.Equal("**bold** `code` [link](http://x)", RunSerializer.ToMarkdown(runs));
    }

    [Fact]
    public void ToMarkdown_StrikeAndHighlight()
    {
        var runs = new[]
        {
            new InlineRun(0, "s", InlineStyle.Strikethrough),
            new InlineRun(1, "h", InlineStyle.Highlight),
        };
        Assert.Equal("~~s~~==h==", RunSerializer.ToMarkdown(runs));
    }

    [Fact]
    public void ToHtml_WrapsAndEscapes()
    {
        var runs = new[]
        {
            new InlineRun(0, "a<b", InlineStyle.Bold),
            new InlineRun(3, "x", InlineStyle.Italic),
            new InlineRun(4, "c", InlineStyle.Code),
        };

        Assert.Equal("<b>a&lt;b</b><i>x</i><code>c</code>", RunSerializer.ToHtml(runs));
    }

    [Fact]
    public void ToHtml_Link()
    {
        var runs = new[] { new InlineRun(0, "t", InlineStyle.None, "http://x?a=1&b=2") };
        Assert.Equal("<a href=\"http://x?a=1&amp;b=2\">t</a>", RunSerializer.ToHtml(runs));
    }

    [Fact]
    public void InlineMath_RoundTripsWithDollars()
    {
        var runs = new[] { new InlineRun(0, "x^2", InlineStyle.Math) };
        Assert.Equal("$x^2$", RunSerializer.ToMarkdown(runs));
        Assert.Equal("<code>$x^2$</code>", RunSerializer.ToHtml(runs));
    }
}
