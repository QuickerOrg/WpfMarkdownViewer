using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Parsing;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

public class ImageTests
{
    [Fact]
    public void ImageSyntax_ParsesAltAndUrl()
    {
        Assert.True(ImageSyntax.TryParse("![a cat](http://x/c.png)", out var alt, out var url));
        Assert.Equal("a cat", alt);
        Assert.Equal("http://x/c.png", url);
    }

    [Theory]
    [InlineData("text ![a](u)")]   // not the whole line
    [InlineData("![a](u) trailing")]
    [InlineData("![a]")]            // no url
    public void ImageSyntax_RejectsNonBlockImages(string line) =>
        Assert.False(ImageSyntax.TryParse(line, out _, out _));

    [Fact]
    public void Streaming_RecognizesBlockImage()
    {
        var parser = new StreamingBlockParser();
        parser.Reparse("![cat](http://example.com/cat.png)", streamComplete: true);

        var img = Assert.IsType<ImageBlock>(Assert.Single(parser.Document.Blocks));
        Assert.Equal("http://example.com/cat.png", img.Url);
        Assert.Equal("cat", img.Alt);
    }

    [Fact]
    public void Markdig_ConvertsSoleImageParagraphToImageBlock()
    {
        var blocks = MarkdigBlockReader.Read("![cat](http://example.com/cat.png)");

        var img = Assert.IsType<ImageBlock>(Assert.Single(blocks));
        Assert.Equal("http://example.com/cat.png", img.Url);
    }

    [Fact]
    public void Markdig_ImageWithinText_StaysParagraph()
    {
        var blocks = MarkdigBlockReader.Read("look ![cat](http://x/c.png) here");

        Assert.Equal(BlockKind.Paragraph, Assert.Single(blocks).Kind);
    }
}
