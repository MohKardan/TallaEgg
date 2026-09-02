using System.Net;
using System.Net.Sockets;
using System.Text;
using TallaEgg.TelegramBot;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Pins what <see cref="ProxyBotClient"/> actually does, because it said one thing and did
/// another: <c>BOT_DIRECT_CONNECTION=1</c> printed "proxy bypassed" and then proxied anyway,
/// since <c>new HttpClient()</c> takes the stock handler — <c>UseProxy = true</c>,
/// <c>Proxy = null</c> — which on Windows resolves through <c>HttpClient.DefaultProxy</c> and
/// reads the same WinInet settings the proxy branch reads. An operator who trusted that message
/// struck a working workaround off the list on false evidence (issue #199).
///
/// <para>
/// The message was never checkable from outside: <c>CreateWithProxy</c> returns an
/// <c>ITelegramBotClient</c>, which exposes neither its handler nor its proxy settings. That is
/// why the choice now comes out of <c>ChooseConnection</c> as a value — a claim about proxying
/// should fail a test when it stops being true, not wait for someone to read the source.
/// </para>
/// </summary>
public class ProxyBotClientTests
{
    private static readonly Uri TelegramApiRoot = new("https://api.telegram.org");

    /// <summary>
    /// A destination that refuses immediately, so a request that goes direct fails fast and one
    /// that goes through the recording proxy is answered by the proxy without ever reaching here.
    /// Port 1 rather than a released ephemeral port: the ephemeral range is what the OS hands out
    /// next, so on a busy machine something can claim it between the probe and the request.
    /// </summary>
    private static readonly Uri Unreachable = new("http://127.0.0.1:1/");

    [Fact]
    public void ChooseConnection_BypassRequested_TurnsProxyingOff()
    {
        var (handler, _) = ProxyBotClient.ChooseConnection(bypassProxy: true, systemProxy: null);

        using var configured = Assert.IsType<HttpClientHandler>(handler);
        Assert.False(configured.UseProxy);
    }

    [Fact]
    public void StockHandler_LeftAlone_WouldStillProxy()
    {
        // The defaults the bug rode on. If .NET ever flipped these, the test above would keep
        // passing while meaning much less, so record what it is guarding against.
        using var stock = new HttpClientHandler();

        Assert.True(stock.UseProxy);
        Assert.Null(stock.Proxy);
    }

    [Fact]
    public void ChooseConnection_BypassRequested_NeverConsultsTheSystemProxy()
    {
        // A bypass that asked the system for its proxy first would fail exactly when it is needed
        // most — on a machine whose proxy configuration is the problem.
        var (handler, _) = ProxyBotClient.ChooseConnection(bypassProxy: true, new ThrowingProxy());

        handler?.Dispose();
    }

    [Fact]
    public void ChooseConnection_BypassRequested_SaysSo()
    {
        var (handler, message) = ProxyBotClient.ChooseConnection(bypassProxy: true, systemProxy: null);
        handler?.Dispose();

        Assert.Contains("BOT_DIRECT_CONNECTION=1", message);
        Assert.DoesNotContain("Using proxy", message);
    }

    [Theory]
    [InlineData(true, null, "🔗 Direct connection (proxy bypassed via BOT_DIRECT_CONNECTION=1)")]
    [InlineData(false, "http://proxy.invalid:8080", "🔧 Using proxy: http://proxy.invalid:8080/")]
    [InlineData(false, null, "🔧 No system proxy applies to api.telegram.org")]
    public void ChooseConnection_EachPath_UsesExactlyTheWordingTheRunbookQuotes(
        bool bypassProxy, string? proxyUri, string expected)
    {
        // NetworkTroubleshooting.md quotes these three lines and tells an operator which action
        // each one calls for, so the wording is an interface rather than an implementation
        // detail. Pinned exactly, not by substring: a reword should fail here and be made
        // deliberately alongside the doc. The lesson of #199 is that these sentences get trusted.
        var (handler, message) = ProxyBotClient.ChooseConnection(
            bypassProxy, new StubProxy(proxyUri is null ? null : new Uri(proxyUri)));
        handler?.Dispose();

        Assert.Equal(expected, message);
    }

    [Fact]
    public void ChooseConnection_ProxyApplies_UsesItAndNamesIt()
    {
        var systemProxy = new StubProxy(new Uri("http://proxy.invalid:8080"));

        var (handler, message) = ProxyBotClient.ChooseConnection(bypassProxy: false, systemProxy);

        using var configured = Assert.IsType<HttpClientHandler>(handler);
        Assert.True(configured.UseProxy);
        Assert.Same(systemProxy, configured.Proxy);
        Assert.Contains("http://proxy.invalid:8080", message);
    }

    [Fact]
    public void ChooseConnection_GetProxyReturnsTheDestination_ReportsNoProxy()
    {
        // What GetSystemWebProxy answers when no proxy applies to the destination. Printed
        // unconditionally, it used to read "Using proxy: https://api.telegram.org/" — which means
        // *no* proxy, and reads as the opposite.
        var (handler, message) = ProxyBotClient.ChooseConnection(
            bypassProxy: false, new StubProxy(TelegramApiRoot));

        Assert.Null(handler);
        Assert.DoesNotContain("Using proxy", message);
    }

    [Fact]
    public void ChooseConnection_GetProxyReturnsNull_ReportsNoProxy()
    {
        // null is not equal to the destination, so this used to take the proxy branch and print
        // "Using proxy: " with nothing after it.
        var (handler, message) = ProxyBotClient.ChooseConnection(bypassProxy: false, new StubProxy(null));

        Assert.Null(handler);
        Assert.DoesNotContain("Using proxy", message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateHttpClient_WithOrWithoutAHandler_KeepsTheLongPollTimeout(bool withHandler)
    {
        // The exception fallback supplies no handler. It must still get 120 s: getUpdates is a
        // long poll, and TelegramBotHostedService derives its down-alert threshold from this.
        using var client = ProxyBotClient.CreateHttpClient(
            withHandler ? new HttpClientHandler { UseProxy = false } : null);

        Assert.Equal(TimeSpan.FromSeconds(120), client.Timeout);
    }

    [Fact]
    public async Task ChooseConnection_BypassRequested_SendsNoTrafficToAConfiguredProxy()
    {
        // The assertions above describe the handler; this one watches the socket. A proxy is
        // attached to the bypass handler and must still be ignored, so anything arriving at the
        // listener means UseProxy = false did not take effect. Loopback only — no outbound
        // traffic and no state shared with other tests.
        using var proxy = new RecordingProxy();

        var (bypassHandler, _) = ProxyBotClient.ChooseConnection(bypassProxy: true, systemProxy: null);
        Assert.NotNull(bypassHandler);
        bypassHandler.Proxy = proxy.AsWebProxy();

        using (var direct = ProxyBotClient.CreateHttpClient(bypassHandler))
        {
            // The client's own timeout is the long-poll 120 s, which is far too long to wait on
            // should something ever answer at Unreachable. Cap it so an unexpected connection
            // fails the test in seconds with a wrong exception type rather than stalling a build.
            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<HttpRequestException>(
                () => direct.GetStringAsync(Unreachable, giveUp.Token));
        }

        Assert.Equal(0, proxy.RequestCount);

        // Control: the same request through a handler that does proxy lands on the listener, so a
        // count of zero above is evidence rather than a listener that never worked.
        using var proxyingHandler = new HttpClientHandler { Proxy = proxy.AsWebProxy(), UseProxy = true };
        using var proxied = ProxyBotClient.CreateHttpClient(proxyingHandler);
        Assert.Equal(RecordingProxy.ResponseBody, await proxied.GetStringAsync(Unreachable));
        Assert.Equal(1, proxy.RequestCount);
        Assert.Equal($"GET {Unreachable} HTTP/1.1", proxy.LastRequestLine);
    }

    private sealed class StubProxy : IWebProxy
    {
        private readonly Uri? _result;

        public StubProxy(Uri? result) => _result = result;

        public ICredentials? Credentials { get; set; }

        public Uri? GetProxy(Uri destination) => _result;

        public bool IsBypassed(Uri host) => _result is null;
    }

    private sealed class ThrowingProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri GetProxy(Uri destination) => throw new InvalidOperationException(NotHere);

        public bool IsBypassed(Uri host) => throw new InvalidOperationException(NotHere);

        private const string NotHere = "The system proxy must not be consulted on the bypass path.";
    }

    /// <summary>
    /// A loopback HTTP proxy that counts what reaches it and answers everything the same way.
    /// Enough to tell "the client proxied" from "the client did not".
    /// </summary>
    private sealed class RecordingProxy : IDisposable
    {
        internal const string ResponseBody = "proxied";

        private static readonly byte[] Response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " +
            ResponseBody.Length + "\r\nConnection: close\r\n\r\n" + ResponseBody);

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _accepting;
        private int _requestCount;
        private string? _lastRequestLine;

        public RecordingProxy()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _accepting = AcceptAsync(_cts.Token);
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public string? LastRequestLine => Volatile.Read(ref _lastRequestLine);

        public IWebProxy AsWebProxy() =>
            new WebProxy($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}");

        private async Task AcceptAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // The whole body, not just the accept: a client that disconnects mid-exchange
                // would otherwise fault this task, and Dispose would rethrow that at teardown as
                // an AggregateException on top of whatever the test was actually asserting.
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    Interlocked.Increment(ref _requestCount);
                    var stream = client.GetStream();
                    var buffer = new byte[1024];
                    var read = await stream.ReadAsync(buffer, cancellationToken);
                    // One read is enough: a proxied request puts the absolute URI on the first
                    // line, which is the whole point of looking.
                    Volatile.Write(ref _lastRequestLine, Encoding.ASCII.GetString(buffer, 0, read).Split("\r\n")[0]);
                    await stream.WriteAsync(Response, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException
                    or SocketException or ObjectDisposedException)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            // The loop swallows its own teardown exceptions, so this only ever waits.
            _accepting.Wait(TimeSpan.FromSeconds(5));
            _cts.Dispose();
        }
    }
}
