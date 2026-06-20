using WpfMarkdownViewer.Streaming;

namespace WpfMarkdownViewer.Tests.Streaming;

public class AdaptiveThrottlePolicyTests
{
    private readonly AdaptiveThrottlePolicy _policy = new();

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public void SparseRate_UsesSlowInterval(double rate) =>
        Assert.Equal(_policy.SlowInterval, _policy.NextInterval(rate));

    [Theory]
    [InlineData(16)]
    [InlineData(60)]
    public void MidRate_UsesMidInterval(double rate) =>
        Assert.Equal(_policy.MidInterval, _policy.NextInterval(rate));

    [Theory]
    [InlineData(61)]
    [InlineData(500)]
    public void FastRate_UsesFastInterval(double rate) =>
        Assert.Equal(_policy.FastInterval, _policy.NextInterval(rate));

    [Fact]
    public void IsIdle_TrueOnlyPastThreshold()
    {
        Assert.False(_policy.IsIdle(TimeSpan.FromMilliseconds(100)));
        Assert.True(_policy.IsIdle(TimeSpan.FromMilliseconds(200)));
    }
}
