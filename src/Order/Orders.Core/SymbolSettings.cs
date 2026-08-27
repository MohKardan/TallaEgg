using TallaEgg.Core.ErrorHandling;

namespace Orders.Core;

/// <summary>
/// Whether a trading pair is currently tradable — shown in the bot's symbol picker, eligible
/// for automatic quoting, and (via <see cref="Quote"/>/<see cref="QuoteFillService"/>) able to
/// be traded at all. A database row rather than a compiled constant, the same reasoning as
/// <see cref="AutoQuoteSettings"/>: activating or deactivating a symbol needs to be a bot
/// command an admin can run, not a code change and a redeploy.
///
/// <para>
/// Everything else about a symbol — decimal precision, min/max quantity, which external price
/// providers know it and under what instrument key — lives in configuration
/// (<c>Symbols:{symbol}</c> in <c>appsettings.global.json</c>, read by
/// <c>TallaEgg.Core.CurrenciesConstant</c>). Only the on/off switch is here, because it is the
/// one property an admin needs to flip at runtime without touching a file or a deploy.
/// </para>
/// </summary>
public class SymbolSettings
{
    public Guid Id { get; private set; }

    /// <summary>Symbol this row governs, e.g. <c>MAUA/IRT</c>.</summary>
    public string Symbol { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>EF Core needs a parameterless constructor.</summary>
    private SymbolSettings() { }

    /// <summary>
    /// The row a symbol gets the first time it is asked about. Starts <b>inactive</b> — a newly
    /// configured symbol must be explicitly turned on by an admin before customers see it,
    /// rather than becoming tradable the moment someone adds a config block for it.
    /// </summary>
    public static SymbolSettings CreateDefault(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new BusinessRuleException("نماد نمی‌تواند خالی باشد.");

        return new SymbolSettings
        {
            Id = Guid.NewGuid(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            IsActive = false,
            UpdatedByUserId = Guid.Empty,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void SetActive(bool isActive, Guid updatedByUserId)
    {
        IsActive = isActive;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}
