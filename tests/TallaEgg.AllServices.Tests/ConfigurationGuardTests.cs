using Microsoft.Extensions.Configuration;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Required configuration must be present, not defaulted (issues #68 and #190).
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
/// Service addresses were defaulted the same way: <c>Users.Api</c> fell back to a wallet URL
/// on a port this system has never used, which issue #190 found only because nothing had ever
/// exercised the missing-key path.
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

    /// <summary>A correctly configured address comes back parsed, ready to be a BaseAddress.</summary>
    [Fact]
    public void RequireUri_WhenTheKeyIsPresent_ReturnsTheParsedUri()
    {
        var configuration = Configuration(("WalletApiUrl", "http://localhost:60933/"));

        var uri = ConfigurationGuard.RequireUri(configuration, "WalletApiUrl");

        Assert.Equal(new Uri("http://localhost:60933/"), uri);
    }

    /// <summary>
    /// The case issue #190 found: Users.Api fell back to a hardcoded wallet address on a port
    /// this system has never used, so a missing key would have started the service pointed at
    /// nothing instead of refusing to run.
    /// </summary>
    [Fact]
    public void RequireUri_WhenTheKeyIsAbsent_Throws()
    {
        var configuration = Configuration(("UsersApiUrl", "http://localhost:5136/"));

        Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireUri(configuration, "WalletApiUrl"));
    }

    /// <summary>A present-but-blank key is a half-finished edit, not a configured value.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireUri_WhenTheValueIsBlank_Throws(string value)
    {
        var configuration = Configuration(("WalletApiUrl", value));

        Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireUri(configuration, "WalletApiUrl"));
    }

    /// <summary>
    /// Present but unusable is a configuration mistake too, and the reason this guard returns a
    /// parsed <see cref="Uri"/> instead of the string. "localhost:60933" is the trap: it parses
    /// as an absolute URI whose scheme is "localhost", so only a scheme check catches it, and
    /// without one it would surface as a failed registration long after startup.
    /// </summary>
    [Theory]
    [InlineData("60933")]
    [InlineData("localhost:60933")]
    [InlineData("/api/wallet")]
    [InlineData("REPLACE_WITH_WALLET_URL")]
    [InlineData("ftp://localhost:60933/")]
    public void RequireUri_WhenTheValueIsNotAnAbsoluteHttpUrl_Throws(string value)
    {
        var configuration = Configuration(("WalletApiUrl", value));

        Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireUri(configuration, "WalletApiUrl"));
    }

    /// <summary>
    /// The message has to be actionable on a server with no debugger attached: which key, which
    /// section, and which file. The file defines WalletApiUrl in three sections with two
    /// incompatible shapes, so naming the key alone would not be enough to act on.
    /// </summary>
    [Fact]
    public void RequireUri_WhenTheKeyIsAbsent_TheMessageNamesTheKeyTheSectionAndTheFile()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireUri(Configuration(), "WalletApiUrl"));

        Assert.Contains("WalletApiUrl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Services:", exception.Message, StringComparison.Ordinal);
        Assert.Contains("appsettings.global.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A rejected value has to appear in the message, or there is nothing to correct.</summary>
    [Fact]
    public void RequireUri_WhenTheValueIsNotAUrl_TheMessageQuotesTheValue()
    {
        var configuration = Configuration(("WalletApiUrl", "localhost:60933"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireUri(configuration, "WalletApiUrl"));

        Assert.Contains("localhost:60933", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The overload for a value that reached the caller by some other route — the bot and the
    /// simulator bind their section to <c>TelegramBotOptions</c> and hand the string to a
    /// constructor, which never sees an <see cref="IConfiguration"/> (issue #205).
    /// </summary>
    [Fact]
    public void RequireAbsoluteHttpUri_WhenTheValueIsUsable_ReturnsTheParsedUri()
    {
        var uri = ConfigurationGuard.RequireAbsoluteHttpUri("http://localhost:60933/api", "WalletApiUrl");

        Assert.Equal(new Uri("http://localhost:60933/api"), uri);
    }

    /// <summary>
    /// An absent key arrives at that constructor as null. It has to be rejected there, or the
    /// bot starts with a wallet client pointed at a compiled-in address.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireAbsoluteHttpUri_WhenTheValueIsMissingOrBlank_Throws(string? value)
    {
        Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireAbsoluteHttpUri(value, "WalletApiUrl"));
    }

    /// <summary>Same rejection as the configuration overload — one implementation, one behaviour.</summary>
    [Theory]
    [InlineData("60933")]
    [InlineData("localhost:60933")]
    [InlineData("REPLACE_WITH_WALLET_URL")]
    public void RequireAbsoluteHttpUri_WhenTheValueIsNotAnAbsoluteHttpUrl_Throws(string value)
    {
        Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireAbsoluteHttpUri(value, "WalletApiUrl"));
    }

    /// <summary>
    /// Both routes to the same guard have to produce the same words. An operator reading a log
    /// line cannot be expected to know whether the value arrived through IConfiguration or
    /// through a bound options object.
    /// </summary>
    [Fact]
    public void RequireAbsoluteHttpUri_AndRequireUri_ReportAMissingValueIdentically()
    {
        var throughConfiguration = Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireUri(Configuration(), "WalletApiUrl"));
        var throughTheString = Assert.Throws<InvalidOperationException>(
            () => ConfigurationGuard.RequireAbsoluteHttpUri(null, "WalletApiUrl"));

        Assert.Equal(throughConfiguration.Message, throughTheString.Message);
    }
}
