using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Parsing;

namespace WpfMarkdownViewer.Tests.Parsing;

public class MarkdigBlockReaderTests
{
    [Fact]
    public void Reads_HeadingLevel()
    {
        var h = Assert.IsType<HeadingBlock>(Assert.Single(MarkdigBlockReader.Read("### Hi")));
        Assert.Equal(3, h.Level);
        Assert.True(h.IsFinalized);
    }

    [Fact]
    public void Reads_FencedCodeLanguage()
    {
        var code = Assert.IsType<CodeBlock>(Assert.Single(MarkdigBlockReader.Read("```python\nx=1\n```")));
        Assert.Equal("python", code.Language);
    }

    [Fact]
    public void Reads_OrderedList()
    {
        var list = Assert.IsType<ListBlock>(Assert.Single(MarkdigBlockReader.Read("1. a\n2. b")));
        Assert.True(list.Ordered);
    }

    [Fact]
    public void Reads_BlockSequence()
    {
        var blocks = MarkdigBlockReader.Read("# T\n\npara\n\n> q");

        Assert.Equal(
            new[] { BlockKind.Heading, BlockKind.Paragraph, BlockKind.Quote },
            blocks.Select(b => b.Kind));
    }
}
