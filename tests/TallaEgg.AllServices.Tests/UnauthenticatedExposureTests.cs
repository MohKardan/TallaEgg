using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Issue #332: authentication is registered only in Production, and <c>dotnet run</c> forces
/// Development whatever the shell exported. Harmless on loopback; an open API on any address another
/// machine can reach. <see cref="UnauthenticatedExposure"/> picks out exactly that combination so the
/// services can warn about it once they have started.
/// </summary>
public class UnauthenticatedExposureTests
{
    [Theory]
    [InlineData("http://localhost:60933")]
    [InlineData("http://LOCALHOST:5136")]
    [InlineData("http://127.0.0.1:5140")]
    [InlineData("http://127.0.0.2:5140")]
    [InlineData("http://[::1]:5140")]
    [InlineData("http://[::ffff:127.0.0.1]:5140")]
    [InlineData("https://localhost:7001/base/path")]
    [InlineData("http://unix:/tmp/wallet.sock")]
    [InlineData("http://pipe:/wallet")]
    public void ExposedUrls_LocalOnlyAddressInDevelopment_IsNotReported(string url)
    {
        Assert.Empty(UnauthenticatedExposure.ExposedUrls(isProduction: false, [url]));
    }

    /// <summary>
    /// The wildcards first: Kestrel accepts <c>*</c> and <c>+</c>, which <see cref="Uri"/> rejects,
    /// so a parser built on <see cref="Uri"/> would have silently missed the commonest way to
    /// listen on every interface.
    /// </summary>
    [Theory]
    [InlineData("http://*:5000")]
    [InlineData("http://+:5000")]
    [InlineData("http://0.0.0.0:60933")]
    [InlineData("http://[::]:60933")]
    [InlineData("http://192.168.1.20:5140")]
    [InlineData("http://tallaegg.internal:5136")]
    public void ExposedUrls_ReachableAddressInDevelopment_IsReported(string url)
    {
        Assert.Equal([url], UnauthenticatedExposure.ExposedUrls(isProduction: false, [url]));
    }

    /// <summary>Production registers the API key, so a reachable address is the point, not a risk.</summary>
    [Fact]
    public void ExposedUrls_InProduction_ReportsNothingWhateverTheAddress()
    {
        Assert.Empty(UnauthenticatedExposure.ExposedUrls(isProduction: true, ["http://0.0.0.0:60933", "http://*:5140"]));
    }

    [Fact]
    public void ExposedUrls_MixedList_ReportsOnlyTheReachableOnes()
    {
        var exposed = UnauthenticatedExposure.ExposedUrls(
            isProduction: false,
            ["http://localhost:60933", "http://0.0.0.0:60934", "", "http://127.0.0.1:60935"]);

        Assert.Equal(["http://0.0.0.0:60934"], exposed);
    }

    /// <summary>
    /// The services check the addresses Kestrel reports once it has bound, not the configuration, so
    /// the classifier has to understand Kestrel's own spelling of them. A real server on a free port,
    /// with <b>no endpoints mapped</b> — bound to every interface for a moment, it offers nothing.
    /// (<c>localhost</c> is covered above only: Kestrel refuses a dynamic port on it.)
    /// </summary>
    [Theory]
    [InlineData("http://0.0.0.0:0", true)]
    [InlineData("http://127.0.0.1:0", false)]
    public async Task ExposedUrls_OnTheAddressesKestrelReportsAfterBinding_ClassifiesThem(string listen, bool exposed)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls(listen);
        await using var app = builder.Build();

        await app.StartAsync();
        var bound = app.Urls.ToList();
        await app.StopAsync();

        Assert.NotEmpty(bound);
        Assert.Equal(exposed, UnauthenticatedExposure.ExposedUrls(app.Environment.IsProduction(), bound).Count > 0);
    }

    /// <summary>
    /// The wiring, end to end: the warning fires once the server has started, from the addresses it
    /// bound — and stays silent in Production on the same address. The server maps no endpoints.
    /// </summary>
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task WarnOnceStartedIfExposed_ServerOnEveryInterface_WarnsOnlyOutsideProduction(string environment, bool warns)
    {
        var sink = new CapturingSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.WebHost.UseUrls("http://0.0.0.0:0");
            await using var app = builder.Build();

            UnauthenticatedExposure.WarnOnceStartedIfExposed(app);

            await app.StartAsync();
            await app.StopAsync();
        }
        finally
        {
            Log.Logger = previous;
        }

        Assert.Equal(warns, sink.Events.Any(e =>
            e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.Contains("issue #332")));
    }

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> Events
        {
            get { lock (_events) return _events.ToList(); }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_events) _events.Add(logEvent);
        }
    }
}
