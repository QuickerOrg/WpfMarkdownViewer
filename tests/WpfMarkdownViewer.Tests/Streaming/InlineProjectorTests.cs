using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

public class InlineProjectorTests
{
    [Fact]
    public void PlainText_IsOneUnstyledRun()
    {
        var p = InlineProjector.Project("plain text");

        Assert.Equal("plain text", p.VisibleText);
        Assert.Equal(new InlineRun(0, "plain text", InlineStyle.None), Assert.Single(p.Runs));
    }

    [Fact]
    public void Bold_HidesMarkers_AppliesStyle()
    {
        var p = InlineProjector.Project("**bold**");

        Assert.Equal("bold", p.VisibleText);
        Assert.Equal(new InlineRun(0, "bold", InlineStyle.Bold), Assert.Single(p.Runs));
    }

    [Fact]
    public void Italic_IsRecognized()
    {
        var p = InlineProjector.Project("*it*");
        Assert.Equal(new InlineRun(0, "it", InlineStyle.Italic), Assert.Single(p.Runs));
    }

    [Fact]
    public void TripleEmphasis_IsBoldItalic()
    {
        var p = InlineProjector.Project("***both***");
        Assert.Equal(new InlineRun(0, "both", InlineStyle.Bold | InlineStyle.Italic), Assert.Single(p.Runs));
    }

    [Fact]
    public void NestedEmphasis_FoldsToPerRunStyle()
    {
        var p = InlineProjector.Project("a **b** c");

        Assert.Equal("a b c", p.VisibleText);
        Assert.Equal(
            new[]
            {
                new InlineRun(0, "a ", InlineStyle.None),
                new InlineRun(2, "b", InlineStyle.Bold),
                new InlineRun(3, " c", InlineStyle.None),
            },
            p.Runs);
    }

    [Fact]
    public void InlineCode_IsLeaf_NoMarkers()
    {
        var p = InlineProjector.Project("text `x` end");

        Assert.Equal("text x end", p.VisibleText);
        Assert.Collection(p.Runs,
            r => Assert.Equal(new InlineRun(0, "text ", InlineStyle.None), r),
            r => Assert.Equal(new InlineRun(5, "x", InlineStyle.Code), r),
            r => Assert.Equal(new InlineRun(6, " end", InlineStyle.None), r));
    }

    [Fact]
    public void Link_CarriesTargetAndHidesSyntax()
    {
        var p = InlineProjector.Project("[click](http://example.com)");

        Assert.Equal("click", p.VisibleText);
        Assert.Equal(new InlineRun(0, "click", InlineStyle.None, "http://example.com"), Assert.Single(p.Runs));
    }

    [Fact]
    public void StyledLinkText_CombinesStyleAndTarget()
    {
        var p = InlineProjector.Project("[**bold**](u)");
        Assert.Equal(new InlineRun(0, "bold", InlineStyle.Bold, "u"), Assert.Single(p.Runs));
    }

    [Fact]
    public void UnclosedBold_OptimisticallyAppliesToRemainder()
    {
        var p = InlineProjector.Project("a **bold");

        Assert.Equal("a bold", p.VisibleText);
        Assert.Collection(p.Runs,
            r => Assert.Equal(new InlineRun(0, "a ", InlineStyle.None), r),
            r => Assert.Equal(new InlineRun(2, "bold", InlineStyle.Bold), r));
    }

    [Fact]
    public void UnclosedBacktick_IsLiteral()
    {
        var p = InlineProjector.Project("a `b");
        Assert.Equal("a `b", p.VisibleText);
    }

    [Fact]
    public void InlineMath_IsLeaf_CarriesLatex_NoDollars()
    {
        var p = InlineProjector.Project("cost $a+b$ end");

        Assert.Equal("cost a+b end", p.VisibleText);
        Assert.Collection(p.Runs,
            r => Assert.Equal(new InlineRun(0, "cost ", InlineStyle.None), r),
            r => Assert.Equal(new InlineRun(5, "a+b", InlineStyle.Math), r),
            r => Assert.Equal(new InlineRun(8, " end", InlineStyle.None), r));
    }

    [Fact]
    public void StrayDollars_AroundDigits_StayLiteral()
    {
        var p = InlineProjector.Project("price $5 and $10");
        Assert.Equal("price $5 and $10", p.VisibleText);
        Assert.Single(p.Runs);
    }

    [Fact]
    public void UnclosedDollar_IsLiteral()
    {
        var p = InlineProjector.Project("a $x");
        Assert.Equal("a $x", p.VisibleText);
    }
}
