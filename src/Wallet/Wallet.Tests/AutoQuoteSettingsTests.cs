using Orders.Core;

namespace Wallet.Tests;

/// <summary>
/// The admin-controlled row that decides whether the auto-quote publisher is allowed to run at
/// all for a symbol, and what spread it applies (issue #90).
/// </summary>
public class AutoQuoteSettingsTests
{
    /// <summary>
    /// Starts disabled and at zero spread. An admin must explicitly turn it on and set a
    /// spread — the row's mere existence must never cause a quote to be published.
    /// </summary>
    [Fact]
    public void CreateDefault_StartsDisabledAtZeroSpread()
    {
        var settings = AutoQuoteSettings.CreateDefault("MAUA/IRT");

        Assert.False(settings.IsEnabled);
        Assert.Equal(0m, settings.SpreadPercent);
    }

    [Fact]
    public void CreateDefault_NormalizesTheSymbol()
    {
        var settings = AutoQuoteSettings.CreateDefault("maua/irt");

        Assert.Equal("MAUA/IRT", settings.Symbol);
    }

    [Fact]
    public void UpdateSpread_RejectsANegativeValue()
    {
        var settings = AutoQuoteSettings.CreateDefault("MAUA/IRT");

        Assert.Throws<ArgumentException>(() => settings.UpdateSpread(-0.1m, Guid.NewGuid()));
    }

    [Fact]
    public void UpdateSpread_RecordsWhoChangedItAndWhen()
    {
        var settings = AutoQuoteSettings.CreateDefault("MAUA/IRT");
        var admin = Guid.NewGuid();
        var before = DateTime.UtcNow;

        settings.UpdateSpread(0.5m, admin);

        Assert.Equal(0.5m, settings.SpreadPercent);
        Assert.Equal(admin, settings.UpdatedByUserId);
        Assert.True(settings.UpdatedAt >= before);
    }

    [Fact]
    public void SetEnabled_RecordsWhoChangedItAndWhen()
    {
        var settings = AutoQuoteSettings.CreateDefault("MAUA/IRT");
        var admin = Guid.NewGuid();

        settings.SetEnabled(true, admin);

        Assert.True(settings.IsEnabled);
        Assert.Equal(admin, settings.UpdatedByUserId);
    }
}
