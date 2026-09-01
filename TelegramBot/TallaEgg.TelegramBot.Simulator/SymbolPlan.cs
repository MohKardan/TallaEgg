using TallaEgg.Core;

namespace TallaEgg.TelegramBot.Simulator;

/// <summary>
/// One trading pair as a run will trade it: the pair's own configuration, the price its quotes
/// walk around, and the quantity band derived from the two.
///
/// <para>
/// It exists because a single shared trade size across every symbol is exactly what hid #146. The
/// simulator only traded MAUA/IRT, whose precision is two decimal places — the same two the
/// <c>Orders.Amount</c> column held — so every quantity round-tripped unchanged and a thousand
/// clean trades proved nothing about a symbol needing eight. Sizes are therefore derived per
/// symbol, from that symbol's own <see cref="TradingPairInfo.MinQuantity"/>,
/// <see cref="TradingPairInfo.MinNotional"/> and <see cref="TradingPairInfo.BaseDecimalPlaces"/>,
/// so one pass produces 0.00000001-scale quantities on Bitcoin and 0.1-scale on gold (#147).
/// </para>
/// </summary>
internal sealed record SymbolPlan(
    TradingPairInfo Pair,
    decimal ReferenceUnitPrice,
    decimal MinTradeQuantity,
    decimal MaxTradeQuantity)
{
    /// <summary>
    /// How many times the symbol's smallest tradable size the largest simulated trade may be.
    ///
    /// A multiple rather than a per-symbol number: because each symbol's own minimum is already
    /// scaled to its price, thirty of them lands every symbol's trades within the same order of
    /// magnitude in toman — which is what lets one wallet-funding formula serve prices five
    /// orders of magnitude apart.
    /// </summary>
    internal const int TradeSizeSpread = 30;

    public string Symbol => Pair.Symbol;

    public string BaseAsset => Pair.BaseAsset;

    /// <summary>The credit ledger backing this symbol's base asset, e.g. <c>CREDIT_BTC</c>.</summary>
    public string CreditAsset => CurrenciesConstant.CreditAssetFor(Pair.BaseAsset);

    /// <summary>
    /// Builds the plan for a pair, given the price its quotes will be published around
    /// (per base unit, in the quote currency — per gram for gold, not per mesghal).
    /// </summary>
    public static SymbolPlan For(TradingPairInfo pair, decimal referenceUnitPrice)
    {
        // Both configured floors bind, and the larger one wins: a quantity above MinQuantity can
        // still be worth less than MinNotional, and OrderService.ValidateTradingLimits refuses
        // either. Deriving the second from the price means a symbol whose market has moved since
        // its limits were written still produces sizes the product accepts.
        var notionalFloor = referenceUnitPrice > 0 ? pair.MinNotional / referenceUnitPrice : 0m;

        // Rounded up, never down: rounding the floor down at the asset's precision would put it
        // back under the minimum it was computed from.
        var minQuantity = CurrenciesConstant.CeilingToCurrencyPrecision(
            Math.Max(pair.MinQuantity, notionalFloor), pair.BaseAsset);

        var maxQuantity = CurrenciesConstant.RoundToCurrencyPrecision(
            minQuantity * TradeSizeSpread, pair.BaseAsset);

        if (pair.MaxQuantity > 0 && maxQuantity > pair.MaxQuantity)
            maxQuantity = pair.MaxQuantity;

        return new SymbolPlan(pair, referenceUnitPrice, minQuantity, maxQuantity);
    }

    /// <summary>
    /// Whether any quantity satisfies this pair's limits at this price.
    ///
    /// <para>
    /// It is false only for a pair whose <see cref="TradingPairInfo.MaxQuantity"/> sits below the
    /// floor its own <see cref="TradingPairInfo.MinNotional"/> implies — a mis-configured pair, or
    /// one whose price has moved far enough that its limits no longer describe a tradable size.
    /// Squeezing a size out of that range anyway would produce orders
    /// <c>OrderService.ValidateTradingLimits</c> refuses, one per trade, which reads as a broken
    /// simulator rather than a broken symbol. The caller drops the symbol and says why.
    /// </para>
    /// </summary>
    public bool HasTradableBand => MaxTradeQuantity >= MinTradeQuantity;

    /// <summary>
    /// A trade size for this symbol, at this symbol's own precision — the whole point of the
    /// plan. Eight decimal places on Bitcoin, two on gold, from the same call.
    /// </summary>
    public decimal RandomQuantity(Random random)
    {
        var raw = MinTradeQuantity + (decimal)random.NextDouble() * (MaxTradeQuantity - MinTradeQuantity);
        var quantity = CurrenciesConstant.RoundToCurrencyPrecision(raw, BaseAsset);

        // Only reachable when the pair's own minimum carries more decimal places than the asset
        // does, which no symbol configured today does — but rounding a draw down through the
        // floor would produce an order the product rejects, and that is a confusing way to find
        // out about a mis-configured pair.
        return quantity < MinTradeQuantity
            ? CurrenciesConstant.CeilingToCurrencyPrecision(MinTradeQuantity, BaseAsset)
            : quantity;
    }

    /// <summary>
    /// The keyword the admin's <c>buyPrice-sellPrice</c> command needs to mean this symbol: the
    /// pair's first configured alias, or the empty string for the symbol an absent keyword
    /// already means. Null when neither applies — a pair added purely by configuration with no
    /// <see cref="TradingPairInfo.Aliases"/> entry cannot be quoted from the bot at all, and the
    /// caller publishes it through the API client instead.
    /// </summary>
    public string? QuoteKeyword
    {
        get
        {
            var alias = Pair.Aliases.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            if (alias is not null)
                return alias;

            return CurrenciesConstant.ResolveSymbolByAlias(null) == Symbol ? string.Empty : null;
        }
    }
}
