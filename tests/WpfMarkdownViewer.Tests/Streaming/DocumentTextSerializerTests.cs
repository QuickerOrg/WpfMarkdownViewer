using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

public class DocumentTextSerializerTests
{
    private static string Plain(string markdown)
    {
        var parser = new StreamingBlockParser();
        parser.Reparse(markdown, streamComplete: true);
        return DocumentTextSerializer.ToPlainText(parser.Document);
    }

    [Fact]
    public void Heading_DropsHashes() => Assert.Equal("Title", Plain("## Title"));

    [Fact]
    public void InlineMarkup_IsStripped() =>
        Assert.Equal("bold and code and link", Plain("**bold** and `code` and [link](http://u)"));

    [Fact]
    public void List_KeepsBulletPrefixes() => Assert.Equal("• a\n• b", Plain("- a\n- b"));

    [Fact]
    public void OrderedList_KeepsNumbers() => Assert.Equal("1. a\n2. b", Plain("1. a\n2. b"));

    [Fact]
    public void Code_IsVerbatim() => Assert.Equal("x = 1;", Plain("```\nx = 1;\n```"));

    [Fact]
    public void Quote_DropsMarker() => Assert.Equal("quoted text", Plain("> quoted text"));

    [Fact]
    public void Table_IsTabSeparated_SkippingDelimiterRow() =>
        Assert.Equal("a\tb\n1\t2", Plain("| a | b |\n| - | - |\n| 1 | 2 |"));

    [Fact]
    public void Blocks_AreNewlineSeparated() =>
        Assert.Equal("Title\nbody", Plain("# Title\n\nbody"));
}
