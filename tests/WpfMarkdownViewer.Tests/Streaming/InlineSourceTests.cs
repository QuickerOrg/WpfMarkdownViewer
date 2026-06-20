using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

public class InlineSourceTests
{
    [Fact]
    public void Heading_StripsMarkersAndNewline()
    {
        var h = new HeadingBlock { Level = 2, RawText = "## Title\n" };
        Assert.Equal("Title", InlineSource.Extract(h));
    }

    [Fact]
    public void Heading_StripsClosingHashes()
    {
        var h = new HeadingBlock { Level = 3, RawText = "### Hi ###\n" };
        Assert.Equal("Hi", InlineSource.Extract(h));
    }

    [Fact]
    public void Paragraph_NormalizesSoftWrapsToSpaces()
    {
        var p = new ParagraphBlock { RawText = "line one\nline two\n" };
        Assert.Equal("line one line two", InlineSource.Extract(p));
    }

    [Fact]
    public void Paragraph_TwoTrailingSpaces_IsHardBreak()
    {
        var p = new ParagraphBlock { RawText = "line one  \nline two" };
        Assert.Equal("line one\nline two", InlineSource.Extract(p));
    }

    [Fact]
    public void Paragraph_TrailingBackslash_IsHardBreak()
    {
        var p = new ParagraphBlock { RawText = "line one\\\nline two" };
        Assert.Equal("line one\nline two", InlineSource.Extract(p));
    }
}
