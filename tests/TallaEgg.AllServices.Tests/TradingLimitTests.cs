using System.Reflection;
using TallaEgg.Core;
using TallaEgg.Core.ErrorHandling;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A symbol's size limits have to be enforced, not merely configured.
///
/// <para>
/// Every trading pair has carried <c>MinQuantity</c>, <c>MaxQuantity</c> and <c>MinNotional</c>
/// since the pair table was written, and the only tests that touched them checked that the values
/// <i>load</i> correctly from configuration. Nothing checked that they <i>apply</i>, because
/// nothing applied them: the product's whole order-size rule was <c>Quantity &gt; 0</c> at the
/// endpoint. A customer could buy 0.00000001 BTC — worth about 160 rial — and the platform would
/// lock collateral, create an order, settle a trade and write transaction rows for it, at the same
/// cost as a trade a billion times larger. <c>MinNotional</c> exists to stop exactly that.
/// </para>
///
/// <para>
/// The failure mode is the one this codebase keeps hitting: configuration that reads as protection
/// and enforces nothing. #143 was the same shape — a handler for an exception that could not be
/// raised — and so was #150's sibling in the audit's own summary, "fixes applied at the call site
/// rather than at the invariant".
/// </para>
/// </summary>
public class TradingLimitTests
{
    /// <summary>
    /// Invokes <c>OrderService.ValidateTradingLimits</c>, which is private because it is an
    /// invariant of order creation rather than an API. Reflection keeps the test on the real
    /// method: a reimplementation here would pass while the product did something else.
    /// </summary>
    private static void Validate(string symbol, decimal quantity, decimal price)
    {
        var method = typeof(Orders.Application.OrderService)
            .GetMethod("ValidateTradingLimits", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        try
        {
            method!.Invoke(null, new object[] { symbol, quantity, price });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static TradingPairInfo Gold() =>
        CurrenciesConstant.GetTradingPairInfo("MAUA/IRT")!;

    // ── The three limits ────────────────────────────────────────────────────────

    /// <summary>Below the symbol's minimum quantity is refused, in the customer's language.</summary>
    [Fact]
    public void AQuantityBelowTheMinimum_IsRefused()
    {
        var pair = Gold();
        var tooSmall = pair.MinQuantity / 2m;

        var ex = Assert.Throws<BusinessRuleException>(
            () => Validate("MAUA/IRT", tooSmall, 20_000_000m));

        Assert.Contains("کمتر از", ex.Message);
        Assert.DoesNotContain("Exception", ex.Message);
    }

    /// <summary>
    /// Above the maximum is refused. This limit is a typing guard more than a risk control — the
    /// customer who meant 1 and entered 1000.
    /// </summary>
    [Fact]
    public void AQuantityAboveTheMaximum_IsRefused()
    {
        var pair = Gold();

        var ex = Assert.Throws<BusinessRuleException>(
            () => Validate("MAUA/IRT", pair.MaxQuantity + 1m, 20_000_000m));

        Assert.Contains("بیشتر از", ex.Message);
    }

    /// <summary>
    /// The limit that does the real work: a quantity can clear <c>MinQuantity</c> and still be
    /// worth less than it costs to settle. Priced at 1 rial, even a large quantity of gold is
    /// worth almost nothing, and quantity alone cannot express that.
    /// </summary>
    [Fact]
    public void AnOrderWorthLessThanTheMinimumNotional_IsRefused()
    {
        var pair = Gold();

        var ex = Assert.Throws<BusinessRuleException>(
            () => Validate("MAUA/IRT", pair.MinQuantity, price: 1m));

        Assert.Contains("ارزش سفارش", ex.Message);
    }

    /// <summary>An ordinary order passes all three.</summary>
    [Fact]
    public void AnOrdinaryOrder_IsAccepted()
    {
        Validate("MAUA/IRT", 1m, 20_000_000m);
    }

    /// <summary>
    /// Exactly at the boundary is allowed. A limit of "at least 0.1" that refuses 0.1 is a
    /// different limit from the one the configuration states.
    /// </summary>
    [Fact]
    public void ExactlyAtTheLimits_IsAccepted()
    {
        var pair = Gold();
        var priceClearingNotional = (pair.MinNotional / pair.MinQuantity) + 1m;

        Validate("MAUA/IRT", pair.MinQuantity, priceClearingNotional);
        Validate("MAUA/IRT", pair.MaxQuantity, priceClearingNotional);
    }

    /// <summary>
    /// A symbol with no entry in the pair table is not this method's business. Order creation
    /// already refuses a genuinely unknown asset further along, and refusing here would report an
    /// unfamiliar symbol as a size problem.
    /// </summary>
    [Fact]
    public void AnUnknownSymbol_IsLeftToTheAssetCheck()
    {
        Validate("NOSUCH/IRT", 0.000001m, 1m);
    }

    // ── The reason this file exists ─────────────────────────────────────────────

    /// <summary>
    /// Every limit the pair table defines must be enforced for every pair that defines it.
    ///
    /// <para>
    /// The explicit tests above all use gold. That is the shape of gap that produced #146: the
    /// simulator traded one symbol, a thousand times, and could not have found a defect that only
    /// appears on a symbol with different precision. This walks the table instead, so a pair added
    /// later is covered the day it is added rather than the day someone remembers to test it.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryPairsLimits_AreActuallyEnforced()
    {
        var unenforced = new List<string>();

        foreach (var pair in CurrenciesConstant.AllTradingPairs)
        {
            if (pair.MinQuantity > 0)
            {
                var below = pair.MinQuantity / 2m;
                if (!Refuses(pair.Symbol, below, HighEnoughPrice(pair)))
                    unenforced.Add($"{pair.Symbol}: MinQuantity {pair.MinQuantity} accepted {below}");
            }

            if (pair.MaxQuantity > 0)
            {
                var above = pair.MaxQuantity + 1m;
                if (!Refuses(pair.Symbol, above, HighEnoughPrice(pair)))
                    unenforced.Add($"{pair.Symbol}: MaxQuantity {pair.MaxQuantity} accepted {above}");
            }

            if (pair.MinNotional > 0 && pair.MinQuantity > 0)
            {
                if (!Refuses(pair.Symbol, pair.MinQuantity, price: 1m))
                    unenforced.Add($"{pair.Symbol}: MinNotional {pair.MinNotional} accepted an order worth ~{pair.MinQuantity}");
            }
        }

        Assert.True(unenforced.Count == 0,
            "These limits are configured but not applied, so they read as protection and give " +
            "none:" + Environment.NewLine +
            string.Join(Environment.NewLine, unenforced.Select(u => "  " + u)));
    }

    /// <summary>A price high enough that the notional rule cannot be what refuses an order.</summary>
    private static decimal HighEnoughPrice(TradingPairInfo pair) =>
        pair.MinQuantity > 0 ? (pair.MinNotional / pair.MinQuantity) + 1m : 1m;

    private static bool Refuses(string symbol, decimal quantity, decimal price)
    {
        try
        {
            Validate(symbol, quantity, price);
            return false;
        }
        catch (BusinessRuleException)
        {
            return true;
        }
    }
}
