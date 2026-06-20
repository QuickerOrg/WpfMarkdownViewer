using System.Text;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Rendering;

public class SvgImageTests
{
    [Theory]
    [InlineData("https://example.com/icon.svg", true)]
    [InlineData("https://example.com/Icon.SVG?v=2", true)]
    [InlineData("https://example.com/photo.png", false)]
    [InlineData("file:///c:/art/logo.svg", true)]
    public void IsSvg_DetectsByExtension(string url, bool expected)
    {
        Assert.Equal(expected, SvgImage.IsSvg(new Uri(url)));
    }

    [WpfFact]
    public void Parse_ValidSvg_ProducesScalableDrawing()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'>" +
                           "<rect width='40' height='20' fill='red'/></svg>";

        var image = SvgImage.Parse(Encoding.UTF8.GetBytes(svg));

        Assert.NotNull(image);
        Assert.True(image!.Width > 0 && image.Height > 0);
        Assert.True(image.IsFrozen); // safe to hand to the UI thread
    }

    [WpfFact]
    public void Parse_Garbage_ReturnsNull()
    {
        Assert.Null(SvgImage.Parse(Encoding.UTF8.GetBytes("this is not svg")));
    }
}
