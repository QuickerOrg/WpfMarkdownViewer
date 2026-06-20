using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Rendering;

public class InlineTextSourceTests
{
    private const double EmSize = 16;

    // The vertical translate (in WPF down-positive coordinates) applied to a run, or 0 if none.
    private static double ScriptOffsetOf(InlineStyle style)
    {
        var projection = new InlineProjection("x", new[] { new InlineRun(0, "x", style) });
        var source = new InlineTextSource(projection, MarkdownStyle.Light, EmSize, baseWeight: FontWeights.Normal);

        var run = Assert.IsType<TextCharacters>(source.GetTextRun(0));
        var effects = run.Properties.TextEffects;
        if (effects is null || effects.Count == 0)
            return 0;
        var translate = Assert.IsType<TranslateTransform>(effects[0].Transform);
        return translate.Y;
    }

    [Fact]
    public void Superscript_RaisesGlyphs() // up = negative Y
    {
        Assert.True(ScriptOffsetOf(InlineStyle.Superscript) < 0);
    }

    [Fact]
    public void Subscript_LowersGlyphs() // down = positive Y
    {
        Assert.True(ScriptOffsetOf(InlineStyle.Subscript) > 0);
    }

    [Fact]
    public void Superscript_RisesMoreThanSubscriptDrops()
    {
        double rise = -ScriptOffsetOf(InlineStyle.Superscript);
        double drop = ScriptOffsetOf(InlineStyle.Subscript);
        Assert.True(rise > drop);
    }

    [Fact]
    public void PlainRun_HasNoShift()
    {
        Assert.Equal(0, ScriptOffsetOf(InlineStyle.None));
    }
}
