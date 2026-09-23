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
    /// A fetch that has started and not finished, with a second caller arriving while it runs.
    ///
    /// <para>
    /// The ordering is forced rather than hoped for: the fetch announces that it has begun and
    /// then waits to be released, so the second call provably happens <i>during</i> it. Counting
    /// callers into a barrier instead would prove less than it looks — a caller can announce
    /// itself and then be descheduled until after the fetch has finished, at which point it is a
    /// later call rather than a concurrent one, and a later call is supposed to fetch again.
    /// </para>
    /// </summary>
    private sealed class HeldFetch
    {
        private readonly TaskCompletionSource _started = new();
        private readonly TaskCompletionSource _release = new();
        private readonly Func<string?> _result;

        public HeldFetch(Func<string?> result) => _result = result;

        public int Attempts;

        public Task Started => _started.Task;

        public void Release() => _release.SetResult();

        public async Task<string?> RunAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Attempts);
            _started.TrySetResult();
            await _release.Task;

            return _result();
        }
    }

    private static async Task<(string? First, string? Second, int Attempts)> TwoCallersDuringOneFetchAsync(
        ReferencePriceDocumentCache cache, Func<string?> result)
    {
        var fetch = new HeldFetch(result);

        var first = cache.GetOrFetchAsync("tgju.org", fetch.RunAsync);
        await fetch.Started.WaitAsync(TimeSpan.FromSeconds(10));

        // Started and not finished: whatever this second call does, it does while the first fetch
        // is still running.
        var second = cache.GetOrFetchAsync("tgju.org", fetch.RunAsync);

        fetch.Release();

        var bodies = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        return (bodies[0], bodies[1], fetch.Attempts);
    }

    [Fact]
    public async Task ASecondCallerDuringAFetch_SharesIt()
    {
        var (first, second, attempts) = await TwoCallersDuringOneFetchAsync(Cache(), () => "the document");

        Assert.Equal(1, attempts);
        Assert.Equal("the document", first);
        Assert.Equal("the document", second);
    }

    /// <summary>
    /// The finding that mattered from the review of PR #323: waiting for a fetch and then
    /// repeating it when it fails is not single-flight. A shared source that hangs would hold each
    /// symbol in turn — fifteen seconds each for tgju, thirty for bonbast's two requests — and a
    /// tick would grow per symbol again, which is the property this change exists to remove.
    /// </summary>
    [Fact]
    public async Task ASecondCallerDuringAFailingFetch_SharesTheFailureRatherThanRepeatingIt()
    {
        var cache = Cache();

        var (first, second, attempts) = await TwoCallersDuringOneFetchAsync(cache, () => null);

        Assert.Equal(1, attempts);
        Assert.Null(first);
        Assert.Null(second);

        // Shared, then forgotten: a later call has to be free to try again.
        Assert.Equal("the document", await cache.GetOrFetchAsync("tgju.org", _ => Task.FromResult<string?>("the document")));
    }

    /// <summary>
    /// A fetch that throws is shared the same way. Each provider catches this itself, so what
    /// reaches the chain is still a null rather than an exception.
    /// </summary>
    [Fact]
    public async Task ASecondCallerDuringAThrowingFetch_SharesTheFailure()
    {
        var cache = Cache();
        var fetch = new HeldFetch(() => throw new HttpRequestException("connection reset"));

        var first = cache.GetOrFetchAsync("tgju.org", fetch.RunAsync);
        await fetch.Started.WaitAsync(TimeSpan.FromSeconds(10));
        var second = cache.GetOrFetchAsync("tgju.org", fetch.RunAsync);

        fetch.Release();

        await Assert.ThrowsAsync<HttpRequestException>(() => first);
        await Assert.ThrowsAsync<HttpRequestException>(() => second);
        Assert.Equal(1, fetch.Attempts);
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
