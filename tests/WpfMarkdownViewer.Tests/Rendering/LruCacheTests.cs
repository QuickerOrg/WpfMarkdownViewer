using WpfMarkdownViewer.Rendering;

namespace WpfMarkdownViewer.Tests.Rendering;

public class LruCacheTests
{
    [Fact]
    public void EvictsLeastRecentlyUsed_OverCapacity()
    {
        var cache = new LruCache<int, string>(capacity: 2);
        cache.Set(1, "a");
        cache.Set(2, "b");
        cache.TryGet(1, out _);   // touch 1 → 2 is now least-recently-used
        cache.Set(3, "c");        // evicts 2

        Assert.True(cache.TryGet(1, out _));
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(3, out _));
        Assert.Equal(2, cache.CountForTest);
    }

    [Fact]
    public void Set_ExistingKey_UpdatesWithoutGrowing()
    {
        var cache = new LruCache<int, string>(capacity: 2);
        cache.Set(1, "a");
        cache.Set(1, "b");

        Assert.Equal(1, cache.CountForTest);
        Assert.True(cache.TryGet(1, out var v));
        Assert.Equal("b", v);
    }
}
