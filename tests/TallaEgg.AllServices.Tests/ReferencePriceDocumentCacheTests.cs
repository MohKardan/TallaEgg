using Orders.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// One fetch per source, however many symbols ask and whenever they ask (issue #317).
///
/// <para>
/// The cache held the last body and nothing more, which was enough while a tick worked through its
/// symbols in sequence: the first stored, the rest read. Now that a tick runs its symbols together,
/// they reach an empty cache at the same instant — so without holding the source, four symbols
/// would pull the same ~180 KB document four times and repeat bonbast's two-request handshake four
/// times, which is the waste the cache was added for.
/// </para>
/// </summary>
public class ReferencePriceDocumentCacheTests
{
    private static ReferencePriceDocumentCache Cache(TimeSpan? duration = null) =>
        new(duration ?? TimeSpan.FromSeconds(90));

    /// <summary>
    /// Deterministic rather than timing-dependent: the fetch does not return until every caller
    /// has arrived, so a cache that let them through one at a time could never finish this test
    /// rather than finishing it slowly.
    /// </summary>
    [Fact]
    public async Task ConcurrentCallers_ShareOneFetch()
    {
        const int callers = 4;
        var cache = Cache();
        var arrived = 0;
        var allArrived = new TaskCompletionSource();
        var fetches = 0;

        async Task<string?> Fetch(CancellationToken ct)
        {
            Interlocked.Increment(ref fetches);
            await allArrived.Task;
            return "the document";
        }

        var waiting = Enumerable.Range(0, callers)
            .Select(_ => Task.Run(async () =>
            {
                // Every caller announces itself before asking, so "all four are in flight" is a
                // fact rather than an assumption about scheduling.
                if (Interlocked.Increment(ref arrived) == callers) allArrived.SetResult();
                return await cache.GetOrFetchAsync("tgju.org", Fetch);
            }))
            .ToList();

        var bodies = await Task.WhenAll(waiting).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, fetches);
        Assert.All(bodies, body => Assert.Equal("the document", body));
    }

    [Fact]
    public async Task ASecondCallAfterTheFirst_ReadsTheCacheInsteadOfFetching()
    {
        var cache = Cache();
        var fetches = 0;

        Task<string?> Fetch(CancellationToken ct)
        {
            Interlocked.Increment(ref fetches);
            return Task.FromResult<string?>("the document");
        }

        await cache.GetOrFetchAsync("tgju.org", Fetch);
        var second = await cache.GetOrFetchAsync("tgju.org", Fetch);

        Assert.Equal(1, fetches);
        Assert.Equal("the document", second);
    }

    /// <summary>
    /// Two sources are two queues. A slow one must not hold up a symbol that does not need it —
    /// which is the whole point of a gate per source rather than one gate for the cache.
    /// </summary>
    [Fact]
    public async Task ASlowSource_DoesNotHoldUpAnother()
    {
        var cache = Cache();
        var slowStarted = new TaskCompletionSource();
        var releaseSlow = new TaskCompletionSource();

        var slow = cache.GetOrFetchAsync("bonbast.com", async _ =>
        {
            slowStarted.SetResult();
            await releaseSlow.Task;
            return "slow";
        });

        await slowStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var fast = await cache.GetOrFetchAsync("tgju.org", _ => Task.FromResult<string?>("fast"))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("fast", fast);

        releaseSlow.SetResult();
        Assert.Equal("slow", await slow);
    }

    /// <summary>
    /// A failure is not stored. Caching one would turn a single transient error into a whole
    /// tick's worth, and the retry costs one request on a source that is already answering badly.
    /// </summary>
    [Fact]
    public async Task AFailedFetch_IsNotCached()
    {
        var cache = Cache();
        var fetches = 0;

        Task<string?> FailThenSucceed(CancellationToken ct) =>
            Task.FromResult(Interlocked.Increment(ref fetches) == 1 ? null : "the document");

        Assert.Null(await cache.GetOrFetchAsync("tgju.org", FailThenSucceed));
        Assert.Equal("the document", await cache.GetOrFetchAsync("tgju.org", FailThenSucceed));
        Assert.Equal(2, fetches);
    }

    /// <summary>
    /// A throwing fetch leaves nothing behind either — neither a cached value nor a gate that
    /// stays shut, which would strand every later caller for that source.
    /// </summary>
    [Fact]
    public async Task AThrowingFetch_ReleasesTheSourceForTheNextCaller()
    {
        var cache = Cache();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            cache.GetOrFetchAsync("tgju.org", _ => throw new HttpRequestException("connection reset")));

        var recovered = await cache
            .GetOrFetchAsync("tgju.org", _ => Task.FromResult<string?>("the document"))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("the document", recovered);
    }

    [Fact]
    public async Task AnExpiredEntry_IsFetchedAgain()
    {
        var cache = Cache(TimeSpan.Zero);
        var fetches = 0;

        Task<string?> Fetch(CancellationToken ct)
        {
            Interlocked.Increment(ref fetches);
            return Task.FromResult<string?>("the document");
        }

        await cache.GetOrFetchAsync("tgju.org", Fetch);
        await cache.GetOrFetchAsync("tgju.org", Fetch);

        Assert.Equal(2, fetches);
    }
}
