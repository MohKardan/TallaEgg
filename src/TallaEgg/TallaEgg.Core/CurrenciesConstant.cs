using Microsoft.Extensions.Configuration;

namespace TallaEgg.Core
{
    public class CurrenciesConstant
    {
        // Convenience constants.
        public const string Maua = "MAUA";

        /// <summary>
        /// The system's currency unit: Toman (IRT).
        /// Deliberately not IRR (rial) — every amount stored in the database is in toman, prices are
        /// entered in toman, and no rial/toman conversion exists anywhere in the code. Using IRR in
        /// the past made it ambiguous whether a stored figure was rials or tomans.
        /// </summary>
        public const string Toman = "IRT";

        public const string Credit_MAUA = "CREDIT_MAUA";

        public const string SekeBahar = "SEKE_BAHAR";

        public const string Btc = "BTC";

        /// <summary>
        /// The weight of one mesghal in grams. Users and admins enter prices per mesghal, and they
        /// are converted to per gram for storage.
        /// </summary>
        public const decimal GramsPerMesghal = 4.3318m;

        // Trading-pair constants.
        public const string BTC_IRT = "BTC/IRT";
        public const string MAUA_IRT = "MAUA/IRT";
        public const string SEKE_BAHAR_IRT = "SEKE_BAHAR/IRT";

        /// <summary>
        /// Compiled defaults for every symbol this platform trades today. A process that never
        /// calls <see cref="Configure"/> sees exactly these — nothing about existing behaviour
        /// depends on a config file being present (the test suite's CI runner has none, by
        /// design — see build-and-test.yml).
        ///
        /// <para>
        /// <b>Adding a new symbol does not require editing this dictionary.</b> A block under
        /// <c>Symbols:{Base}/{Quote}</c> in <c>config/appsettings.global.json</c> — decimal
        /// precision, min/max quantity, Persian display name, and which external price provider
        /// instrument feeds it — is enough; see README's "Adding a trading symbol" section. This
        /// dictionary only needs a new entry if the symbol should also work with zero config
        /// present (i.e. it becomes one of the platform's built-in defaults).
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, TradingPairInfo> DefaultTradingPairs =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [BTC_IRT] = new TradingPairInfo
                {
                    Symbol = BTC_IRT, BaseAsset = Btc, QuoteAsset = Toman, PersianName = "بیت‌کوین/تومان",
                    MinQuantity = 0.0001m, MaxQuantity = 100m, PriceDecimalPlaces = 0, QuantityDecimalPlaces = 8,
                    MinNotional = 1000000m, BaseAssetPersianName = "بیت‌کوین", BaseUnit = "بیت‌کوین",
                    BaseDecimalPlaces = 8, Aliases = new List<string> { "بیت", "بیتکوین", "بیت‌کوین" }
                },
                [MAUA_IRT] = new TradingPairInfo
                {
                    Symbol = MAUA_IRT, BaseAsset = Maua, QuoteAsset = Toman, PersianName = "آبشده/تومان",
                    MinQuantity = 0.1m, MaxQuantity = 1000m, PriceDecimalPlaces = 0, QuantityDecimalPlaces = 3,
                    MinNotional = 100000m, BaseAssetPersianName = "آبشده", BaseUnit = "گرم", BaseDecimalPlaces = 2
                },
                [SEKE_BAHAR_IRT] = new TradingPairInfo
                {
                    Symbol = SEKE_BAHAR_IRT, BaseAsset = SekeBahar, QuoteAsset = Toman,
                    PersianName = "سکه تمام بهار آزادی/تومان", MinQuantity = 0.01m, MaxQuantity = 50m,
                    PriceDecimalPlaces = 0, QuantityDecimalPlaces = 2, MinNotional = 1000000m,
                    BaseAssetPersianName = "سکه تمام بهار آزادی", BaseUnit = "سکه", BaseDecimalPlaces = 2,
                    Aliases = new List<string> { "سکه" }
                }
            };

        private static Dictionary<string, TradingPairInfo> _pairs =
            new(DefaultTradingPairs, StringComparer.OrdinalIgnoreCase);

        // Toman is structural to the whole system — the one unit of account — rather than "a
        // symbol", so it is a fixed entry instead of being derived from a trading pair like
        // every base asset (and its CREDIT_ ledger) below is.
        private static readonly CurrencyInfo TomanInfo = new()
        { Code = Toman, PersianName = "تومان", Unit = "تومان", DecimalPlaces = 0, IsTradable = false };

        private static Dictionary<string, CurrencyInfo> _currencies = BuildCurrencies(_pairs);

        /// <summary>
        /// The credit-ledger asset code for a tradable base asset, e.g. <c>MAUA</c> →
        /// <c>CREDIT_MAUA</c>. Every tradable asset gets one (see <see cref="BuildCurrencies"/>) —
        /// credit was gold-only back when gold was the only tradable asset; it is a per-asset
        /// ceiling now that there is more than one.
        /// </summary>
        public static string CreditAssetFor(string baseAsset) => "CREDIT_" + baseAsset;

        /// <summary>
        /// Whether a code is already a credit ledger. Callers that are about to apply
        /// <see cref="CreditAssetFor"/> use it to refuse input that has been prefixed once already,
        /// rather than building "CREDIT_CREDIT_MAUA" and failing later with a wallet-not-found.
        /// </summary>
        public static bool IsCreditAsset(string? code) =>
            code is not null && code.StartsWith("CREDIT_", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Merges the "Symbols" section of the shared config file on top of the compiled
        /// defaults: an unrecognised key becomes a brand-new trading pair, a known one has only
        /// the fields the config block actually sets overridden. Call once at each service's
        /// startup (see each Program.cs). Safe to skip — every symbol traded today has a
        /// compiled default, so a process that never calls this behaves exactly as if it had; the
        /// call only matters for a symbol nobody has written a <see cref="TradingPairInfo"/> for.
        /// </summary>
        public static void Configure(IConfiguration configuration)
        {
            _pairs = MergeWithConfiguration(_pairs, configuration);
            _currencies = BuildCurrencies(_pairs);
        }

        /// <summary>
        /// The pure merge <see cref="Configure"/> applies — split out so it can be unit-tested
        /// against an arbitrary starting dictionary and configuration, without mutating this
        /// class's shared static state (which every test in the process reads).
        /// </summary>
        public static Dictionary<string, TradingPairInfo> MergeWithConfiguration(
            IReadOnlyDictionary<string, TradingPairInfo> current, IConfiguration configuration)
        {
            var merged = new Dictionary<string, TradingPairInfo>(current, StringComparer.OrdinalIgnoreCase);

            foreach (var child in configuration.GetSection("Symbols").GetChildren())
            {
                var info = merged.TryGetValue(child.Key, out var existing) ? Clone(existing) : new TradingPairInfo();
                child.Bind(info);
                info.Symbol = child.Key;

                if (string.IsNullOrWhiteSpace(info.BaseAsset) || string.IsNullOrWhiteSpace(info.QuoteAsset))
                {
                    var parts = child.Key.Split('/');
                    if (parts.Length == 2)
                    {
                        if (string.IsNullOrWhiteSpace(info.BaseAsset)) info.BaseAsset = parts[0];
                        if (string.IsNullOrWhiteSpace(info.QuoteAsset)) info.QuoteAsset = parts[1];
                    }
                }

                merged[child.Key] = info;
            }

            return merged;
        }

        private static TradingPairInfo Clone(TradingPairInfo source) => new()
        {
            Symbol = source.Symbol,
            BaseAsset = source.BaseAsset,
            QuoteAsset = source.QuoteAsset,
            PersianName = source.PersianName,
            MinQuantity = source.MinQuantity,
            MaxQuantity = source.MaxQuantity,
            PriceDecimalPlaces = source.PriceDecimalPlaces,
            QuantityDecimalPlaces = source.QuantityDecimalPlaces,
            MinNotional = source.MinNotional,
            BaseAssetPersianName = source.BaseAssetPersianName,
            BaseUnit = source.BaseUnit,
            BaseDecimalPlaces = source.BaseDecimalPlaces,
            Aliases = new List<string>(source.Aliases)
        };

        private static Dictionary<string, CurrencyInfo> BuildCurrencies(Dictionary<string, TradingPairInfo> pairs)
        {
            var map = new Dictionary<string, CurrencyInfo>(StringComparer.OrdinalIgnoreCase)
            {
                [Toman] = TomanInfo
            };

            foreach (var pair in pairs.Values)
            {
                if (string.IsNullOrWhiteSpace(pair.BaseAsset)) continue;

                var baseName = string.IsNullOrWhiteSpace(pair.BaseAssetPersianName) ? pair.BaseAsset : pair.BaseAssetPersianName;

                map[pair.BaseAsset] = new CurrencyInfo
                {
                    Code = pair.BaseAsset,
                    PersianName = baseName,
                    Unit = pair.BaseUnit,
                    DecimalPlaces = pair.BaseDecimalPlaces,
                    IsTradable = true
                };

                // One credit ledger per tradable asset (issue: multi-symbol top-up, see the
                // conversation that added this) — same ceiling concept CREDIT_MAUA already was,
                // generalized instead of staying a MAUA-only special case.
                var creditCode = CreditAssetFor(pair.BaseAsset);
                map[creditCode] = new CurrencyInfo
                {
                    Code = creditCode,
                    PersianName = "اعتبار " + baseName,
                    Unit = pair.BaseUnit,
                    DecimalPlaces = pair.BaseDecimalPlaces,
                    IsTradable = false
                };
            }

            return map;
        }

        // The currency catalogue.
        public static List<CurrencyInfo> AllCurrencies => _currencies.Values.ToList();

        // The trading-pair catalogue.
        public static List<TradingPairInfo> AllTradingPairs => _pairs.Values.ToList();

        // All currency codes, in their canonical form.
        public static List<string> GetAllCodes() =>
            _currencies.Values.Select(c => c.Code).ToList();

        // Look up a currency, case-insensitively. Null when the code is not one we trade.
        public static CurrencyInfo? GetCurrencyInfo(string code) =>
            _currencies.TryGetValue(code, out var info) ? info : null;

        // Whether a currency code is valid, case-insensitively.
        public static bool IsValidCurrency(string code) =>
            _currencies.ContainsKey(code);

        /// <summary>
        /// Rounds an amount to that asset's real precision — toman has no decimals, melted gold has two.
        ///
        /// Call this wherever an amount is computed — above all for quantity x price, whose result
        /// runs to 18 decimal places while the database columns are decimal(28,8). If the locked
        /// amount and the settled amount are computed at different precisions, a small residue is
        /// left in LockedBalance once the order ends, which in a financial system is a slow leak.
        ///
        /// MidpointRounding.ToEven is not used; AwayFromZero is the expected behaviour in financial
        /// arithmetic.
        /// </summary>
        public static decimal RoundToCurrencyPrecision(decimal amount, string assetCode)
        {
            var info = GetCurrencyInfo(assetCode);
            // An unknown asset returns the amount untouched, so existing behaviour is not broken.
            if (info is null)
                return amount;

            return Math.Round(amount, info.DecimalPlaces, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Rounds an amount to the asset's precision, always <b>up</b>.
        ///
        /// Used for the amount locked as collateral. Rounding up means the locked amount is never
        /// less than the order's real value.
        /// </summary>
        public static decimal CeilingToCurrencyPrecision(decimal amount, string assetCode)
        {
            var info = GetCurrencyInfo(assetCode);
            if (info is null) return amount;

            var factor = Pow10(info.DecimalPlaces);
            return Math.Ceiling(amount * factor) / factor;
        }

        /// <summary>
        /// Rounds an amount to the asset's precision, always <b>down</b>.
        ///
        /// Used for the amount each trade consumes from the collateral.
        ///
        /// Why the directions are deliberately opposite (issue #52): the lock is computed once for
        /// the whole order while consumption is computed per fill. If both rounded AwayFromZero, the
        /// consumptions could sum to more than the locked amount and the "insufficient collateral"
        /// guard would refuse a perfectly valid trade.
        ///
        /// With "lock up, consume down", this inequality always holds:
        ///
        ///     sum Floor(qi x pi) <= sum qi x pi <= sum qi x p_max <= Q x p_max <= Ceiling(Q x p_max)
        ///
        /// So "total consumed <= amount locked" is a mathematical guarantee rather than a
        /// probability, and it does not depend on the fills sharing a price, since a buyer never pays
        /// more than their own order's limit. The leftover difference is released on completion or
        /// cancellation.
        /// </summary>
        public static decimal FloorToCurrencyPrecision(decimal amount, string assetCode)
        {
            var info = GetCurrencyInfo(assetCode);
            if (info is null) return amount;

            var factor = Pow10(info.DecimalPlaces);
            return Math.Floor(amount * factor) / factor;
        }

        private static decimal Pow10(int exponent)
        {
            decimal result = 1m;
            for (var i = 0; i < exponent; i++) result *= 10m;
            return result;
        }

        /// <summary>
        /// The storage precision of an order price. Independent of the Orders.Price column
        /// itself, which #146 widened to decimal(28,8); this is an application-level cap, not a
        /// mirror of the column.
        /// </summary>
        public const int OrderPriceDecimalPlaces = 2;

        /// <summary>
        /// Rounds a price to the precision the database column can actually hold.
        ///
        /// Why it is needed: a mesghal price is divided by 4.3318 and the result runs to 28 decimal
        /// places — 79,000,000 / 4.3318 = 18237222.4017729..., for example. The locked amount was
        /// computed from that full in-memory value, while the price itself was rounded to two
        /// decimals on the way into the column, and settlement later read the rounded value back.
        /// Two sides of one equation were computed from two different prices, and the difference
        /// stayed in LockedBalance forever (issue #52).
        ///
        /// Rounding on the way in means the lock and settlement use the same number, and the locked
        /// amount becomes recomputable from the stored order alone.
        ///
        /// Deliberately not keyed off the pair's PriceDecimalPlaces: for MAUA/IRT that is zero while
        /// the column holds two decimals. Rounding to zero would change price precision from 0.01 to
        /// 1 toman per gram, which is a business decision rather than a bug fix. The column is the
        /// authority here, because it is what actually constrains the value.
        /// </summary>
        public static decimal RoundOrderPrice(decimal price) =>
            Math.Round(price, OrderPriceDecimalPlaces, MidpointRounding.AwayFromZero);

        /// <summary>Looks up a trading pair, case-insensitively. Returns null if there is no match.</summary>
        public static TradingPairInfo? GetTradingPairInfo(string symbol) =>
            symbol is not null && _pairs.TryGetValue(symbol, out var info) ? info : null;

        /// <summary>
        /// The currency's Persian display name. Unknown currencies fall back to the code itself.
        /// </summary>
        public static string GetPersianName(string code) =>
            GetCurrencyInfo(code)?.PersianName ?? code;

        /// <summary>
        /// Converts user input into a currency code. Accepts both the Latin code ("IRT") and the
        /// Persian name ("تومان"), so an admin need not memorise the English code. Returns null if
        /// the input matches no currency.
        /// </summary>
        public static string? ResolveCurrencyCode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var trimmed = input.Trim();

            // First, match against the currency code.
            if (_currencies.TryGetValue(trimmed, out var byCode))
                return byCode.Code;

            // Then against the full Persian name.
            var byName = _currencies.Values.FirstOrDefault(c =>
                string.Equals(c.PersianName, trimmed, StringComparison.OrdinalIgnoreCase));

            if (byName is not null)
                return byName.Code;

            // Finally the short keyword ("سکه", "بیت") — the same list the quote commands use,
            // TradingPairInfo.Aliases, so an admin does not have to remember a different name per
            // command.
            var bySymbolAlias = ResolveSymbolByAlias(trimmed);
            if (bySymbolAlias is not null)
                return GetTradingPairInfo(bySymbolAlias)?.BaseAsset;

            return null;
        }

        /// <summary>
        /// The typeable Persian currency names, for use in an error message instead of Latin codes.
        ///
        /// <para>
        /// Each asset's CREDIT_ variant is excluded, because the top-up and deduction commands both
        /// add the CREDIT_ prefix themselves: an admin typing the credit name would produce a
        /// double-prefixed code ("CREDIT_CREDIT_MAUA"), which is not an asset. This list exists to
        /// prevent that input, not to invite it — and <see cref="IsCreditAsset"/> now refuses it
        /// outright rather than leaving the omission from a help message as the only defence.
        /// </para>
        ///
        /// <para>
        /// The reason used to be true of the top-up command only. The deduction command passed the
        /// resolved code through unprefixed, so it read the credit name correctly while the list
        /// hid that from the admin — the one form that reduced a credit line was the one form
        /// nobody was told about. Both commands act on the credit ledger now, so the exclusion is
        /// finally true of both.
        /// </para>
        /// </summary>
        public static string GetPersianNamesList() =>
            string.Join("، ", _currencies.Values
                .Where(c => !c.Code.StartsWith("CREDIT_", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.PersianName));

        /// <summary>
        /// The trading pair's Persian display name. An unknown pair is composed from both sides'
        /// Persian names, so a Latin symbol is never shown inside Persian text.
        /// </summary>
        public static string GetPersianSymbolName(string symbol)
        {
            var pair = GetTradingPairInfo(symbol);
            if (pair is not null && !string.IsNullOrWhiteSpace(pair.PersianName))
                return pair.PersianName;

            var parts = symbol?.Split('/');
            if (parts is { Length: 2 })
                return $"{GetPersianName(parts[0])}/{GetPersianName(parts[1])}";

            return symbol ?? string.Empty;
        }

        /// <summary>
        /// Resolves a symbol from the Persian keyword used in admin commands ("سکه", "بیت"), reading
        /// each symbol's <see cref="TradingPairInfo.Aliases"/>, so adding a new symbol with its own
        /// keyword needs no change here. An empty keyword means melted gold — the admin habit from
        /// before these other symbols existed. A keyword matching no symbol returns null, so the
        /// caller can tell "no keyword given" apart from "unknown keyword".
        /// </summary>
        public static string? ResolveSymbolByAlias(string? keyword)
        {
            var trimmed = keyword?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return MAUA_IRT;

            foreach (var pair in _pairs.Values)
            {
                if (pair.Aliases.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                    return pair.Symbol;
            }

            return null;
        }
    }

    public class CurrencyInfo
    {
        public string Code { get; set; } = string.Empty;          // مثل "MAUA" یا "IRT"
        public string PersianName { get; set; } = string.Empty;   // نام فارسی ارز
        public string Unit { get; set; } = string.Empty;          // واحد نمایش
        public int DecimalPlaces { get; set; }    // تعداد اعشار
        public bool IsTradable { get; set; }      // قابل معامله بودن
    }

    public class TradingPairInfo
    {
        /// <summary>Pair symbol, for example MAUA/IRT.</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>Base asset, for example MAUA.</summary>
        public string BaseAsset { get; set; } = string.Empty;

        /// <summary>Quote asset, for example IRT.</summary>
        public string QuoteAsset { get; set; } = string.Empty;

        /// <summary>Persian display name.</summary>
        public string PersianName { get; set; } = string.Empty;

        /// <summary>Minimum tradable quantity.</summary>
        public decimal MinQuantity { get; set; }

        /// <summary>Maximum tradable quantity.</summary>
        public decimal MaxQuantity { get; set; }

        /// <summary>Price decimal places.</summary>
        public int PriceDecimalPlaces { get; set; }

        /// <summary>Quantity decimal places.</summary>
        public int QuantityDecimalPlaces { get; set; }

        /// <summary>Minimum trade value.</summary>
        public decimal MinNotional { get; set; }

        /// <summary>Persian name of the base asset on its own, for example "آبشده".</summary>
        public string BaseAssetPersianName { get; set; } = string.Empty;

        /// <summary>Display unit for the base asset, for example "گرم" or "سکه".</summary>
        public string BaseUnit { get; set; } = string.Empty;

        /// <summary>Base-asset decimal places, used when rounding amounts via CurrenciesConstant.RoundToCurrencyPrecision.</summary>
        public int BaseDecimalPlaces { get; set; }

        /// <summary>Persian keywords the bot's admin commands accept for this symbol, for example "سکه".</summary>
        public List<string> Aliases { get; set; } = new();
    }
}
