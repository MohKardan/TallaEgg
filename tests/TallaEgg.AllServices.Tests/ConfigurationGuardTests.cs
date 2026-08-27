using Microsoft.Extensions.Configuration;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Connection strings must be present, not defaulted (issue #68).
///
/// <para>
/// All five services used to read the connection string with
/// <c>GetConnectionString("…") ?? "Server=localhost;…"</c>. On a server where the shared
/// configuration file was missing the key, every service silently started against a local
/// SQL Server instead of refusing to run — so the failure surfaced far from its cause,
/// as an empty or wrong database rather than as a configuration error.
/// </para>
///
/// <para>
/// These tests pin the replacement: a missing or blank value stops startup with a message
/// that names both the key and the file to edit.
/// </para>
/// </summary>
public class ConfigurationGuardTests
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))
            .Build();

    /// <summary>The behaviour when configuration is correct is unchanged: the value comes back as-is.</summary>
    [Fact]
    public void RequireConnectionString_WhenTheKeyIsPresent_ReturnsTheValue()
    {
        var configuration = Configuration(("ConnectionStrings:OrdersDb", "Server=db;Database=X;"));

        var value = ConfigurationGuard.RequireConnectionString(configuration, "OrdersDb");

        Assert.Equal("Server=db;Database=X;", value);
    }

    /// <summary>The case that used to fall back to localhost.</summary>
    [Fact]
    public void RequireConnectionString_WhenTheKeyIsAbsent_Throws()
    {
        var configuration = Configuration(("ConnectionStrings:WalletDb", "Server=db;Database=X;"));

        Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireConnectionString(configuration, "OrdersDb"));
    }

    /// <summary>
    /// A present-but-blank key is a half-finished edit, not a configured value. Letting it
    /// through would only move the failure to the first query.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireConnectionString_WhenTheValueIsBlank_Throws(string value)
    {
        var configuration = Configuration(("ConnectionStrings:OrdersDb", value));

        Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireConnectionString(configuration, "OrdersDb"));
    }

    /// <summary>
    /// The message has to be actionable on a server with no debugger attached: which key,
    /// and which file it belongs in.
    /// </summary>
    [Fact]
    public void RequireConnectionString_WhenTheKeyIsAbsent_TheMessageNamesTheKeyAndTheFile()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireConnectionString(Configuration(), "OrdersDb"));

        Assert.Contains("OrdersDb", exception.Message, StringComparison.Ordinal);
        Assert.Contains("appsettings.global.json", exception.Message, StringComparison.Ordinal);
    }
}
