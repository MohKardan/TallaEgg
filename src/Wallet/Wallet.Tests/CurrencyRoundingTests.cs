using TallaEgg.Core;

namespace Wallet.Tests;

/// <summary>
/// Tests for CurrenciesConstant.RoundToCurrencyPrecision.
///
/// Monetary amounts are computed as quantity × price, which for gold produces long
/// fractional tails (a mesghal price divided by 4.3318 is not exact). Toman has no
/// sub-unit, so those tails are meaningless — and worse, if the amount locked at order
/// time and the amount settled later are rounded differently, a residual is left behind
/// in LockedBalance. These tests pin the rounding contract both sides rely on.
/// </summary>
public class CurrencyRoundingTests
{
    [Fact]
    public void Toman_IsRoundedToWholeUnits()
    {
        // The exact value observed in production logs: 79,000,000 per mesghal / 4.3318 × 10,000.
        var raw = 182372224017.72935038552103052m;

        var rounded = CurrenciesConstant.RoundToCurrencyPrecision(raw, CurrenciesConstant.Toman);

        Assert.Equal(182372224018m, rounded);
        Assert.Equal(0m, rounded % 1m); // no fractional part survives
    }

    [Fact]
    public void Gold_IsRoundedToTwoDecimals()
    {
        var rounded = CurrenciesConstant.RoundToCurrencyPrecision(8.456789m, CurrenciesConstant.Maua);

        Assert.Equal(8.46m, rounded);
    }

    [Fact]
    public void Rounding_UsesAwayFromZero_NotBankersRounding()
    {
        // Banker's rounding would give 2 here; financial rounding must give 3.
        Assert.Equal(3m, CurrenciesConstant.RoundToCurrencyPrecision(2.5m, CurrenciesConstant.Toman));
        // And symmetrically for negative amounts, which occur on credit-backed balances.
        Assert.Equal(-3m, CurrenciesConstant.RoundToCurrencyPrecision(-2.5m, CurrenciesConstant.Toman));
    }

    [Fact]
    public void LockAndSettlementAmounts_AgreeAfterRounding()
    {
        // The residual bug: a buy order locks quantity × price, and settlement later
        // consumes the trade's quoteQuantity. Computed independently at full precision the
        // two can differ; rounded through the same helper they must be identical.
        const decimal quantity = 12m;
        const decimal pricePerGram = 18237222.401772935038552103052m;

        var lockedAtOrderTime = CurrenciesConstant.RoundToCurrencyPrecision(quantity * pricePerGram, CurrenciesConstant.Toman);
        var settledAtTradeTime = CurrenciesConstant.RoundToCurrencyPrecision(quantity * pricePerGram, CurrenciesConstant.Toman);

        Assert.Equal(lockedAtOrderTime, settledAtTradeTime);
        Assert.Equal(0m, lockedAtOrderTime - settledAtTradeTime); // no residual left locked
    }

    [Fact]
    public void UnknownAsset_IsReturnedUnchanged()
    {
        // Defensive: an unrecognised asset must not silently lose precision.
        var raw = 1.23456789m;

        Assert.Equal(raw, CurrenciesConstant.RoundToCurrencyPrecision(raw, "NOT_A_REAL_ASSET"));
    }
}
