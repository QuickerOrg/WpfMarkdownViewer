using WpfMarkdownViewer.Controls;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Rendering;

public class MarkdownStyleTests
{
    [Fact]
    public void With_OverridesOnlyTheGivenProperty()
    {
        var tweaked = MarkdownStyle.Light with { EmSize = 20 };

        Assert.Equal(20, tweaked.EmSize);
        Assert.Same(MarkdownStyle.Light.Foreground, tweaked.Foreground); // others unchanged
        Assert.Same(MarkdownStyle.Light.HeadingScales, tweaked.HeadingScales);
    }

    [Fact]
    public void HeadingEm_FollowsScales_AndClampsLevel()
    {
        var s = MarkdownStyle.Light;

        Assert.Equal(s.EmSize * 1.8, s.HeadingEm(1));
        Assert.Equal(s.EmSize, s.HeadingEm(6));
        Assert.Equal(s.EmSize, s.HeadingEm(99)); // clamped
    }

    [Fact]
    public void LightAndDark_PairDifferentCodeThemes()
    {
        Assert.NotEqual(MarkdownStyle.Light.CodeTheme, MarkdownStyle.Dark.CodeTheme);
    }

    [WpfFact]
    public void SettingMarkdownStyleProperty_UpdatesBackground()
    {
        var view = new MarkdownDocumentView();

        view.MarkdownStyle = MarkdownStyle.Dark;

        Assert.Same(MarkdownStyle.Dark.Background, view.Background);
        Assert.Same(MarkdownStyle.Dark, view.MarkdownStyle);
    }
}
