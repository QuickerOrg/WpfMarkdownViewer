using WpfMarkdownViewer.Parsing;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

/// <summary>
/// Inline-level Converge (ADR-0002/0007): for closed inline input, the streaming projector must produce
/// the same Visible Space text and the same flat run shapes as Markdig's authoritative inline parse.
/// </summary>
public class InlineConvergeTests
{
    public static IEnumerable<object[]> Samples()
    {
        yield return ["plain text"];
        yield return ["**bold**"];
        yield return ["*italic*"];
        yield return ["***both***"];
        yield return ["a **b** c"];
        yield return ["lead `code` tail"];
        yield return ["[click](http://example.com)"];
        yield return ["[**bold link**](http://example.com)"];
        yield return ["mix **b** and *i* and `c` done"];
        yield return ["~~struck~~"];
        yield return ["a ==highlight== b"];
        yield return ["++inserted++"];
        yield return ["mix ~~s~~ and **b** and ==m== done"];
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void StreamingInline_Converges_ToMarkdig(string sample)
    {
        var streaming = InlineProjector.Project(sample);
        var markdig = MarkdigInlineProjector.Project(sample);

        Assert.Equal(markdig.VisibleText, streaming.VisibleText);
        Assert.Equal(markdig.Runs, streaming.Runs);
    }
}
