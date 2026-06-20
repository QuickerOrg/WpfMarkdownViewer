using WpfMarkdownViewer.Model;
using WpfMarkdownViewer.Parsing;
using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

/// <summary>
/// The Converge guardrail (ADR-0002): for each sample, the streaming state machine's finalized block
/// shapes must equal Markdig's authoritative parse. A failure here is a real divergence to fix in the
/// streaming parser — it is exactly the "flicker on finalize" defect made measurable.
/// </summary>
public class ConvergeTests
{
    public static IEnumerable<object[]> Samples()
    {
        yield return ["hello world"];
        yield return ["# Heading\n\nbody paragraph"];
        yield return ["first paragraph\n\nsecond paragraph"];
        yield return ["```csharp\nvar x = 1;\n```"];
        yield return ["- one\n- two\n- three"];
        yield return ["1. one\n2. two"];
        yield return ["> a quote\n> spanning lines"];
        yield return ["# H1\n\nintro\n\n## H2\n\nmore text"];
        yield return ["# Title\n\nintro para\n\n```js\ncode();\n```\n\n- item one\n- item two\n\n> a note"];
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void StreamingPreview_Converges_ToMarkdig(string sample)
    {
        var streaming = new StreamingBlockParser();
        streaming.Reparse(sample, streamComplete: true);
        var streamingShapes = BlockShape.Of(streaming.Document.Blocks);

        var markdigShapes = BlockShape.Of(MarkdigBlockReader.Read(sample));

        Assert.Equal(markdigShapes, streamingShapes);
    }
}
