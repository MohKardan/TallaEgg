using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Issue #332: authentication is registered only in Production, and <c>dotnet run</c> forces
/// Development whatever the shell exported. Harmless on loopback; an open API on any address another
/// machine can reach. <see cref="UnauthenticatedExposure"/> picks out exactly that combination so the
/// three services can warn about it at startup.
/// </summary>
public class UnauthenticatedExposureTests
{
    [Theory]
    [InlineData("http://localhost:60933")]
    [InlineData("http://LOCALHOST:5136")]
    [InlineData("http://127.0.0.1:5140")]
    [InlineData("http://127.0.0.2:5140")]
    [InlineData("http://[::1]:5140")]
    [InlineData("https://localhost:7001/base/path")]
    public void ExposedUrls_LoopbackAddressInDevelopment_IsNotReported(string url)
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

    /// <summary>The shared config binds every service to loopback today; that must stay silent.</summary>
    [Fact]
    public void ExposedUrls_NoAddressesConfigured_ReportsNothing()
    {
        Assert.Empty(UnauthenticatedExposure.ExposedUrls(isProduction: false, []));
    }
}
