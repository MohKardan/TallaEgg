using Orders.Core;

namespace Wallet.Tests;

/// <summary>
/// The admin-controlled row that decides whether a symbol is tradable at all — shown in the
/// bot's symbol picker, eligible for auto-quote, usable for a manual quote. Everything else
/// about a symbol lives in configuration (<c>TallaEgg.Core.CurrenciesConstant</c>); only this
/// on/off switch needs to be a database row an admin can flip from inside the bot.
/// </summary>
public class SymbolSettingsTests
{
    /// <summary>
    /// Starts inactive. A symbol newly defined in config must not become tradable the moment
    /// someone asks about it — an admin has to explicitly turn it on.
    /// </summary>
    [Fact]
    public void CreateDefault_StartsInactive()
    {
        var settings = SymbolSettings.CreateDefault("SEKE_BAHAR/IRT");

        Assert.False(settings.IsActive);
    }

    [Fact]
    public void CreateDefault_NormalizesTheSymbol()
    {
        var settings = SymbolSettings.CreateDefault("maua/irt");

        Assert.Equal("MAUA/IRT", settings.Symbol);
    }

    [Fact]
    public void SetActive_RecordsWhoChangedItAndWhen()
    {
        var settings = SymbolSettings.CreateDefault("BTC/IRT");
        var admin = Guid.NewGuid();
        var before = DateTime.UtcNow;

        settings.SetActive(true, admin);

        Assert.True(settings.IsActive);
        Assert.Equal(admin, settings.UpdatedByUserId);
        Assert.True(settings.UpdatedAt >= before);
    }

    [Fact]
    public void SetActive_CanTurnItBackOff()
    {
        var settings = SymbolSettings.CreateDefault("BTC/IRT");
        settings.SetActive(true, Guid.NewGuid());

        settings.SetActive(false, Guid.NewGuid());

        Assert.False(settings.IsActive);
    }
}
