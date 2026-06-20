using System.IO;
using System.Text;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Rendering;

public class ImageLoaderTests
{
    [Fact]
    public async Task DataUri_Base64_Decodes()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg'/>";
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        var uri = new Uri($"data:image/svg+xml;base64,{b64}");

        byte[]? bytes = await ImageLoader.LoadBytesAsync(uri);

        Assert.NotNull(bytes);
        Assert.Equal(svg, Encoding.UTF8.GetString(bytes!));
    }

    [Fact]
    public async Task DataUri_PercentEncoded_Decodes()
    {
        var uri = new Uri("data:image/svg+xml,%3Csvg%3E%3C%2Fsvg%3E");

        byte[]? bytes = await ImageLoader.LoadBytesAsync(uri);

        Assert.Equal("<svg></svg>", Encoding.UTF8.GetString(bytes!));
    }

    [Fact]
    public async Task LocalFile_IsRead()
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "hello");
        try
        {
            byte[]? bytes = await ImageLoader.LoadBytesAsync(new Uri(path));
            Assert.Equal("hello", Encoding.UTF8.GetString(bytes!));
        }
        finally { File.Delete(path); }
    }
}
