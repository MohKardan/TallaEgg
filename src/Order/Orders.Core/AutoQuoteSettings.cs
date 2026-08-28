using TallaEgg.Core.ErrorHandling;

namespace Orders.Core;

/// <summary>
/// The admin-controlled settings for automatic quote publishing on one symbol: the spread
/// applied to the fetched reference price, and whether the feature is on at all.
///
/// <para>
/// <b>Why a table and not configuration:</b> the spread has to be changeable from inside the
/// bot, at runtime, without a redeploy — a configuration-file value is only read at startup
/// (see <c>Program.cs</c>'s <c>ResolveSharedConfigPath</c>). <see cref="IsEnabled"/> doubles as
/// the feature's on/off switch, so a bad reading or a misbehaving provider can be turned off
/// from inside the product rather than by editing a file and restarting five services.
/// </para>
/// </summary>
public class AutoQuoteSettings
{
    public Guid Id { get; private set; }

    /// <summary>Symbol this row governs — mirrors <see cref="Quote"/>'s per-symbol shape.</summary>
    public string Symbol { get; private set; } = string.Empty;

    /// <summary>
    /// Percentage spread applied around the fetched reference price to derive buy/sell — e.g.
    /// 0.5 means the shop buys 0.5% below and sells 0.5% above the reference.
    /// </summary>
    public decimal SpreadPercent { get; private set; }

    /// <summary>Whether the background publisher is allowed to publish for this symbol at all.</summary>
    public bool IsEnabled { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>EF Core needs a parameterless constructor.</summary>
    private AutoQuoteSettings() { }

    /// <summary>
    /// The row a symbol gets the first time it is asked about. Starts disabled and at zero
    /// spread on purpose — an admin must explicitly opt in and set a spread before the
    /// background publisher ever posts a price, rather than the feature silently activating the
    /// moment the row is created.
    /// </summary>
    public static AutoQuoteSettings CreateDefault(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new BusinessRuleException("نماد نمی‌تواند خالی باشد.");

        return new AutoQuoteSettings
        {
            Id = Guid.NewGuid(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            SpreadPercent = 0m,
            IsEnabled = false,
            UpdatedByUserId = Guid.Empty,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateSpread(decimal spreadPercent, Guid updatedByUserId)
    {
        if (spreadPercent < 0)
            throw new BusinessRuleException("اسپرد نمی‌تواند منفی باشد.");

        SpreadPercent = spreadPercent;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEnabled(bool isEnabled, Guid updatedByUserId)
    {
        IsEnabled = isEnabled;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}
